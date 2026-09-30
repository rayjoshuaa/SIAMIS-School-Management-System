using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SIAMIS.Application.Employees;
using SIAMIS.Application.Payroll;
using SIAMIS.Domain.Entities.Payroll;
using SIAMIS.Infrastructure.Data;

namespace SIAMIS.Infrastructure.Services;

public sealed class EmployeePayrollService(SIAMISDbContext db) : IEmployeePayrollService
{
    private const decimal MaximumAmount = 999_999_999_999_999.9999m;
    private static readonly string[] PayrollStatuses = ["Draft", "Calculated", "Approved", "Paid", "Cancelled"];
    private static readonly string[] ActivePeriodStatuses = ["Open", "Processing"];

    public async Task<ServiceResult<PagedResult<EmployeePayrollListItemDto>>> GetPayrollsAsync(EmployeePayrollListQuery query, CancellationToken ct)
    {
        if (query.Page < 1 || query.PageSize is < 1 or > 100)
            return Invalid<PagedResult<EmployeePayrollListItemDto>>("Page must be positive and PageSize must be between 1 and 100.");
        var status = query.Status is null ? null : NormalizeStatus(query.Status);
        if (query.Status is not null && status is null)
            return Invalid<PagedResult<EmployeePayrollListItemDto>>("Status must be Draft, Calculated, Approved, Paid, or Cancelled.");

        var source = db.EmployeePayrolls.AsNoTracking().AsQueryable();
        if (query.PayrollPeriodId.HasValue) source = source.Where(item => item.PayrollPeriodId == query.PayrollPeriodId.Value);
        if (query.EmployeeId.HasValue) source = source.Where(item => item.EmployeeId == query.EmployeeId.Value);
        if (status is not null) source = source.Where(item => item.Status == status);
        var total = await source.CountAsync(ct);
        var items = await source.OrderByDescending(item => item.PayrollPeriod.StartDate)
            .ThenBy(item => item.Employee.EmployeeNumber).ThenBy(item => item.EmployeePayrollId)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(item => new EmployeePayrollListItemDto(
                new EmployeePayrollDto(item.EmployeePayrollId, item.PayrollPeriodId, item.EmployeeId, item.BasicSalary,
                    item.GrossPay, item.TotalDeductions, item.NetPay, item.TaxableEarnings, item.Status, item.Remarks, item.CreatedAt, item.UpdatedAt,
                    item.ApprovedAt, item.PaidAt, item.CancelledAt, item.CancellationReason),
                item.Employee.EmployeeNumber,
                (item.Employee.PreferredName ?? item.Employee.FirstName) + " " + item.Employee.LastName,
                item.PayrollPeriod.Code,
                item.PayrollPeriod.Name))
            .ToListAsync(ct);
        return ServiceResult<PagedResult<EmployeePayrollListItemDto>>.Success(new(items, query.Page, query.PageSize, total));
    }

    public async Task<ServiceResult<EmployeePayrollDetailDto>> GetPayrollAsync(Guid id, CancellationToken ct)
    {
        var payroll = await db.EmployeePayrolls.AsNoTracking()
            .Include(item => item.Employee).Include(item => item.PayrollPeriod)
            .SingleOrDefaultAsync(item => item.EmployeePayrollId == id, ct);
        if (payroll is null) return NotFound<EmployeePayrollDetailDto>("Employee payroll was not found.");
        var lines = await db.EmployeePayrollLines.AsNoTracking().Where(item => item.EmployeePayrollId == id)
            .OrderBy(item => item.ComponentName).ThenBy(item => item.EmployeePayrollLineId)
            .Select(item => new EmployeePayrollLineDto(item.EmployeePayrollLineId, item.EmployeePayrollId, item.PayrollComponentId,
                item.ComponentCode, item.ComponentName, item.ComponentType, item.Amount, item.Quantity, item.Rate, item.Remarks,
                item.SourceType, item.SourceId, item.CalculationMethodSnapshot, item.RuleCode, item.RuleName,
                item.ApplicationMode, item.BaseType, item.BaseAmount, item.MinimumBase, item.MaximumBase, item.CalculationRate,
                item.IsTaxableSnapshot, item.IsStatutorySnapshot, item.ContributionSideSnapshot))
            .ToListAsync(ct);
        var employee = payroll.Employee;
        var period = payroll.PayrollPeriod;
        return ServiceResult<EmployeePayrollDetailDto>.Success(new(
            ToDto(payroll),
            new(employee.EmployeeId, employee.EmployeeNumber, EmployeeName(employee), employee.IsActive),
            new(period.PayrollPeriodId, period.Code, period.Name, period.StartDate, period.EndDate, period.PayDate, period.Status),
            lines));
    }

    public async Task<ServiceResult<EmployeePayrollDetailDto>> CreatePayrollAsync(EmployeePayrollCreateRequest request, CancellationToken ct)
    {
        var validation = ValidateHeader(request.PayrollPeriodId, request.EmployeeId, request.Remarks);
        if (validation is not null) return Invalid<EmployeePayrollDetailDto>(validation);
        if (!await db.Employees.AsNoTracking().AnyAsync(item => item.EmployeeId == request.EmployeeId!.Value, ct))
            return NotFound<EmployeePayrollDetailDto>("Employee was not found.");
        var period = await db.PayrollPeriods.AsNoTracking().SingleOrDefaultAsync(item => item.PayrollPeriodId == request.PayrollPeriodId!.Value, ct);
        if (period is null) return NotFound<EmployeePayrollDetailDto>("Payroll period was not found.");
        if (!IsActivePeriod(period.Status)) return Invalid<EmployeePayrollDetailDto>("Payroll period must be Open or Processing to create employee payroll.");
        if (await db.EmployeePayrolls.AnyAsync(item => item.EmployeeId == request.EmployeeId && item.PayrollPeriodId == request.PayrollPeriodId, ct))
            return Conflict<EmployeePayrollDetailDto>("An employee payroll already exists for this employee and payroll period.");

        var payroll = new EmployeePayroll
        {
            PayrollPeriodId = period.PayrollPeriodId,
            EmployeeId = request.EmployeeId!.Value,
            BasicSalary = 0m,
            GrossPay = 0m,
            TaxableEarnings = 0m,
            TotalDeductions = 0m,
            NetPay = 0m,
            Status = "Draft",
            Remarks = Clean(request.Remarks)
        };
        db.EmployeePayrolls.Add(payroll);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return Conflict<EmployeePayrollDetailDto>("An employee payroll already exists for this employee and payroll period.");
        }
        return await GetPayrollAsync(payroll.EmployeePayrollId, ct);
    }

    public async Task<ServiceResult<EmployeePayrollDetailDto>> UpdatePayrollAsync(Guid id, EmployeePayrollUpdateRequest request, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var payroll = await GetPayrollForMutationAsync(id, ct);
        if (payroll is null) return NotFound<EmployeePayrollDetailDto>("Employee payroll was not found.");
        if (!IsEditable(payroll.Status)) return Conflict<EmployeePayrollDetailDto>($"{payroll.Status} payroll is finalized and cannot be updated.");
        var validation = ValidateHeader(request.PayrollPeriodId, request.EmployeeId, request.Remarks);
        if (validation is not null) return Invalid<EmployeePayrollDetailDto>(validation);
        var identityChanged = payroll.EmployeeId != request.EmployeeId || payroll.PayrollPeriodId != request.PayrollPeriodId;
        if (identityChanged && (payroll.Status != "Draft" || await db.EmployeePayrollLines.AnyAsync(item => item.EmployeePayrollId == id, ct)))
            return Conflict<EmployeePayrollDetailDto>("EmployeeId and PayrollPeriodId can only change on a Draft payroll with no lines.");
        if (!await db.Employees.AsNoTracking().AnyAsync(item => item.EmployeeId == request.EmployeeId!.Value, ct))
            return NotFound<EmployeePayrollDetailDto>("Employee was not found.");
        var period = await db.PayrollPeriods.AsNoTracking().SingleOrDefaultAsync(item => item.PayrollPeriodId == request.PayrollPeriodId!.Value, ct);
        if (period is null) return NotFound<EmployeePayrollDetailDto>("Payroll period was not found.");
        var periodChanged = payroll.PayrollPeriodId != request.PayrollPeriodId!.Value;
        if (periodChanged && !IsActivePeriod(period.Status))
            return Invalid<EmployeePayrollDetailDto>("Payroll period must be Open or Processing when assigning employee payroll to it.");
        if (await db.EmployeePayrolls.AnyAsync(item => item.EmployeePayrollId != id
            && item.EmployeeId == request.EmployeeId && item.PayrollPeriodId == request.PayrollPeriodId, ct))
            return Conflict<EmployeePayrollDetailDto>("An employee payroll already exists for this employee and payroll period.");

        payroll.EmployeeId = request.EmployeeId!.Value;
        payroll.PayrollPeriodId = request.PayrollPeriodId!.Value;
        payroll.Remarks = Clean(request.Remarks);
        try
        {
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return Conflict<EmployeePayrollDetailDto>("An employee payroll already exists for this employee and payroll period.");
        }
        return await GetPayrollAsync(id, ct);
    }

    // Authorization and authenticated actor attribution must be added when SIAMIS authentication exists.
    public async Task<ServiceResult<bool>> ApprovePayrollAsync(Guid id, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var payroll = await GetPayrollForMutationAsync(id, ct);
        if (payroll is null) return NotFound<bool>("Employee payroll was not found.");
        if (payroll.Status != "Calculated") return Conflict<bool>($"Only Calculated payroll can be approved. Current status: {payroll.Status}.");
        var integrityError = await ValidateStoredSnapshotAsync(payroll, ct);
        if (integrityError is not null) return Conflict<bool>(integrityError);
        payroll.Status = "Approved";
        payroll.ApprovedAt = DateTime.UtcNow;
        payroll.PaidAt = null;
        payroll.CancelledAt = null;
        payroll.CancellationReason = null;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<bool>> MarkPayrollPaidAsync(Guid id, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var payroll = await GetPayrollForMutationAsync(id, ct);
        if (payroll is null) return NotFound<bool>("Employee payroll was not found.");
        if (payroll.Status != "Approved") return Conflict<bool>($"Only Approved payroll can be marked paid. Current status: {payroll.Status}.");
        if (!payroll.ApprovedAt.HasValue) return Conflict<bool>("ApprovedAt is missing. Historical approval must be reviewed before this payroll can be marked paid.");
        var integrityError = await ValidateStoredSnapshotAsync(payroll, ct);
        if (integrityError is not null) return Conflict<bool>(integrityError);
        payroll.Status = "Paid";
        payroll.PaidAt = DateTime.UtcNow;
        payroll.CancelledAt = null;
        payroll.CancellationReason = null;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<bool>> CancelPayrollAsync(Guid id, EmployeePayrollCancelRequest request, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var payroll = await GetPayrollForMutationAsync(id, ct);
        if (payroll is null) return NotFound<bool>("Employee payroll was not found.");
        if (!IsEditable(payroll.Status)) return Conflict<bool>($"Only Draft or Calculated payroll can be cancelled. Current status: {payroll.Status}.");
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 1000)
            return Invalid<bool>("Cancellation reason is required and cannot exceed 1000 characters.");
        payroll.Status = "Cancelled";
        payroll.CancelledAt = DateTime.UtcNow;
        payroll.CancellationReason = request.Reason.Trim();
        payroll.ApprovedAt = null;
        payroll.PaidAt = null;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<bool>> DeletePayrollAsync(Guid id, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var payroll = await GetPayrollForMutationAsync(id, ct);
        if (payroll is null) return NotFound<bool>("Employee payroll was not found.");
        if (payroll.Status != "Draft") return Conflict<bool>("Only an empty Draft payroll can be deleted.");
        if (await db.EmployeePayrollLines.AnyAsync(item => item.EmployeePayrollId == id, ct))
            return Conflict<bool>("Employee payroll cannot be deleted while payroll lines exist.");
        db.EmployeePayrolls.Remove(payroll);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<IReadOnlyList<EmployeePayrollLineDto>>> GetLinesAsync(Guid payrollId, CancellationToken ct)
    {
        if (!await db.EmployeePayrolls.AsNoTracking().AnyAsync(item => item.EmployeePayrollId == payrollId, ct))
            return NotFound<IReadOnlyList<EmployeePayrollLineDto>>("Employee payroll was not found.");
        var lines = await db.EmployeePayrollLines.AsNoTracking().Where(item => item.EmployeePayrollId == payrollId)
            .OrderBy(item => item.ComponentName).ThenBy(item => item.EmployeePayrollLineId)
            .Select(item => new EmployeePayrollLineDto(item.EmployeePayrollLineId, item.EmployeePayrollId, item.PayrollComponentId,
                item.ComponentCode, item.ComponentName, item.ComponentType, item.Amount, item.Quantity, item.Rate, item.Remarks,
                item.SourceType, item.SourceId, item.CalculationMethodSnapshot, item.RuleCode, item.RuleName,
                item.ApplicationMode, item.BaseType, item.BaseAmount, item.MinimumBase, item.MaximumBase, item.CalculationRate,
                item.IsTaxableSnapshot, item.IsStatutorySnapshot, item.ContributionSideSnapshot))
            .ToListAsync(ct);
        return ServiceResult<IReadOnlyList<EmployeePayrollLineDto>>.Success(lines);
    }

    public async Task<ServiceResult<EmployeePayrollLineDto>> GetLineAsync(Guid payrollId, Guid lineId, CancellationToken ct)
    {
        if (!await db.EmployeePayrolls.AsNoTracking().AnyAsync(item => item.EmployeePayrollId == payrollId, ct))
            return NotFound<EmployeePayrollLineDto>("Employee payroll was not found.");
        var line = await db.EmployeePayrollLines.AsNoTracking().Where(item => item.EmployeePayrollId == payrollId
            && item.EmployeePayrollLineId == lineId)
            .Select(item => new EmployeePayrollLineDto(item.EmployeePayrollLineId, item.EmployeePayrollId, item.PayrollComponentId,
                item.ComponentCode, item.ComponentName, item.ComponentType, item.Amount, item.Quantity, item.Rate, item.Remarks,
                item.SourceType, item.SourceId, item.CalculationMethodSnapshot, item.RuleCode, item.RuleName,
                item.ApplicationMode, item.BaseType, item.BaseAmount, item.MinimumBase, item.MaximumBase, item.CalculationRate,
                item.IsTaxableSnapshot, item.IsStatutorySnapshot, item.ContributionSideSnapshot))
            .SingleOrDefaultAsync(ct);
        return line is null ? NotFound<EmployeePayrollLineDto>("Payroll line was not found for this payroll.")
            : ServiceResult<EmployeePayrollLineDto>.Success(line);
    }

    public async Task<ServiceResult<EmployeePayrollLineDto>> CreateLineAsync(Guid payrollId, EmployeePayrollLineRequest request, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var payroll = await GetPayrollForMutationAsync(payrollId, ct);
        if (payroll is null) return NotFound<EmployeePayrollLineDto>("Employee payroll was not found.");
        if (!IsEditable(payroll.Status)) return Conflict<EmployeePayrollLineDto>($"{payroll.Status} payroll cannot have lines added.");
        var validation = ValidateLine(request);
        if (validation is not null) return Invalid<EmployeePayrollLineDto>(validation);
        var component = await db.PayrollComponents.AsNoTracking().SingleOrDefaultAsync(item => item.Id == request.PayrollComponentId!.Value, ct);
        if (component is null) return NotFound<EmployeePayrollLineDto>("Payroll component was not found.");
        if (!component.IsActive) return Invalid<EmployeePayrollLineDto>("Payroll component must be active.");
        if (component.Code is null) return Invalid<EmployeePayrollLineDto>("Payroll component must have a code before it can be added to payroll.");

        var line = new EmployeePayrollLine { EmployeePayrollId = payrollId, SourceType = "Manual", SourceId = null };
        ApplyLine(line, request, component);
        db.EmployeePayrollLines.Add(line);
        await db.SaveChangesAsync(ct);
        var reconciliationError = await ReconcileTotalsAsync(payroll, ct);
        if (reconciliationError is not null) return Invalid<EmployeePayrollLineDto>(reconciliationError);
        await transaction.CommitAsync(ct);
        return ServiceResult<EmployeePayrollLineDto>.Success(ToDto(line));
    }

    public async Task<ServiceResult<EmployeePayrollLineDto>> UpdateLineAsync(Guid payrollId, Guid lineId, EmployeePayrollLineRequest request, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var payroll = await GetPayrollForMutationAsync(payrollId, ct);
        if (payroll is null) return NotFound<EmployeePayrollLineDto>("Employee payroll was not found.");
        var line = await db.EmployeePayrollLines.SingleOrDefaultAsync(item => item.EmployeePayrollId == payrollId
            && item.EmployeePayrollLineId == lineId, ct);
        if (line is null) return NotFound<EmployeePayrollLineDto>("Payroll line was not found for this payroll.");
        if (!IsEditable(payroll.Status)) return Conflict<EmployeePayrollLineDto>($"{payroll.Status} payroll lines cannot be updated.");
        if (line.SourceType != "Manual")
            return Conflict<EmployeePayrollLineDto>("Generated payroll lines cannot be manually updated. Use a separate Manual adjustment or payroll regeneration.");
        var validation = ValidateLine(request);
        if (validation is not null) return Invalid<EmployeePayrollLineDto>(validation);
        var component = await db.PayrollComponents.AsNoTracking().SingleOrDefaultAsync(item => item.Id == request.PayrollComponentId!.Value, ct);
        if (component is null) return NotFound<EmployeePayrollLineDto>("Payroll component was not found.");
        if (!component.IsActive) return Invalid<EmployeePayrollLineDto>("Payroll component must be active.");
        if (component.Code is null) return Invalid<EmployeePayrollLineDto>("Payroll component must have a code before it can be added to payroll.");
        ApplyLine(line, request, component);
        await db.SaveChangesAsync(ct);
        var reconciliationError = await ReconcileTotalsAsync(payroll, ct);
        if (reconciliationError is not null) return Invalid<EmployeePayrollLineDto>(reconciliationError);
        await transaction.CommitAsync(ct);
        return ServiceResult<EmployeePayrollLineDto>.Success(ToDto(line));
    }

    public async Task<ServiceResult<bool>> DeleteLineAsync(Guid payrollId, Guid lineId, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var payroll = await GetPayrollForMutationAsync(payrollId, ct);
        if (payroll is null) return NotFound<bool>("Employee payroll was not found.");
        var line = await db.EmployeePayrollLines.SingleOrDefaultAsync(item => item.EmployeePayrollId == payrollId
            && item.EmployeePayrollLineId == lineId, ct);
        if (line is null) return NotFound<bool>("Payroll line was not found for this payroll.");
        if (!IsEditable(payroll.Status)) return Conflict<bool>($"{payroll.Status} payroll lines cannot be deleted.");
        if (line.SourceType != "Manual")
            return Conflict<bool>("Generated payroll lines cannot be manually deleted. Use a separate Manual adjustment or payroll regeneration.");
        db.EmployeePayrollLines.Remove(line);
        await db.SaveChangesAsync(ct);
        var reconciliationError = await ReconcileTotalsAsync(payroll, ct);
        if (reconciliationError is not null) return Invalid<bool>(reconciliationError);
        await transaction.CommitAsync(ct);
        return ServiceResult<bool>.Success(true);
    }

    private async Task<string?> ReconcileTotalsAsync(EmployeePayroll payroll, CancellationToken ct)
    {
        // The saved mutation and reconciliation share a transaction; only stored line snapshots determine totals.
        var totals = await GetStoredTotalsAsync(payroll.EmployeePayrollId, ct);
        var grossPay = totals?.GrossPay ?? 0m;
        var taxableEarnings = totals?.TaxableEarnings ?? 0m;
        var totalDeductions = totals?.TotalDeductions ?? 0m;
        if (!ValidAmount(grossPay) || !ValidAmount(taxableEarnings) || !ValidAmount(totalDeductions))
            return "Payroll totals exceed the supported decimal(19,4) range.";
        if (totalDeductions > grossPay)
            return "Total deductions exceed GrossPay; payroll line mutation was not saved.";

        payroll.GrossPay = grossPay;
        payroll.TaxableEarnings = taxableEarnings;
        payroll.TotalDeductions = totalDeductions;
        payroll.NetPay = grossPay - totalDeductions;
        await db.SaveChangesAsync(ct);
        return null;
    }

    private Task<EmployeePayroll?> GetPayrollForMutationAsync(Guid id, CancellationToken ct)
        // UPDLOCK serializes competing transitions before they read status; callers hold a transaction.
        => db.EmployeePayrolls.FromSqlInterpolated($"SELECT * FROM [EmployeePayrolls] WITH (UPDLOCK) WHERE [EmployeePayrollId] = {id}")
            .SingleOrDefaultAsync(ct);

    private Task<StoredTotals?> GetStoredTotalsAsync(Guid id, CancellationToken ct)
        => db.EmployeePayrollLines.AsNoTracking().Where(line => line.EmployeePayrollId == id)
            .GroupBy(line => line.EmployeePayrollId)
            .Select(lines => new StoredTotals(
                lines.Sum(line => line.ComponentType == "Earning" ? line.Amount : 0m),
                lines.Sum(line => line.ComponentType == "Earning" && line.IsTaxableSnapshot ? line.Amount : 0m),
                lines.Sum(line => line.ComponentType == "Deduction" ? line.Amount : 0m)))
            .SingleOrDefaultAsync(ct);

    private async Task<string?> ValidateStoredSnapshotAsync(EmployeePayroll payroll, CancellationToken ct)
    {
        var totals = await GetStoredTotalsAsync(payroll.EmployeePayrollId, ct);
        if (totals is null) return "Stored payroll integrity failed: payroll has no lines.";
        if (!ValidAmount(totals.GrossPay) || !ValidAmount(totals.TaxableEarnings) || !ValidAmount(totals.TotalDeductions))
            return "Stored payroll integrity failed: line totals exceed the supported monetary range.";
        if (payroll.GrossPay != totals.GrossPay || payroll.TaxableEarnings != totals.TaxableEarnings
            || payroll.TotalDeductions != totals.TotalDeductions || payroll.NetPay != totals.GrossPay - totals.TotalDeductions
            || totals.TotalDeductions > totals.GrossPay || payroll.NetPay < 0)
            return "Stored payroll integrity failed: header totals do not match stored lines or deductions exceed GrossPay. No values were changed.";
        return null;
    }

    private sealed record StoredTotals(decimal GrossPay, decimal TaxableEarnings, decimal TotalDeductions);
    private static bool IsEditable(string status) => status is "Draft" or "Calculated";

    private static string? ValidateLine(EmployeePayrollLineRequest request)
    {
        if (!request.PayrollComponentId.HasValue) return "PayrollComponentId is required.";
        if (string.IsNullOrWhiteSpace(request.Remarks)) return "Remarks are required for manual payroll lines.";
        if (!request.Amount.HasValue || request.Amount.Value <= 0 || request.Amount.Value > MaximumAmount || !HasScaleFour(request.Amount.Value))
            return "Amount must be greater than zero and fit decimal(19,4).";
        if (request.Quantity.HasValue && (request.Quantity.Value < 0 || request.Quantity.Value > MaximumAmount || !HasScaleFour(request.Quantity.Value)))
            return "Quantity must be non-negative and fit decimal(19,4).";
        if (request.Rate.HasValue && (request.Rate.Value < 0 || request.Rate.Value > MaximumAmount || !HasScaleFour(request.Rate.Value)))
            return "Rate must be non-negative and fit decimal(19,4).";
        if (request.Remarks?.Length > 1000) return "Remarks cannot exceed 1000 characters.";
        return null;
    }

    private static string? ValidateHeader(Guid? payrollPeriodId, Guid? employeeId, string? remarks)
    {
        if (!payrollPeriodId.HasValue) return "PayrollPeriodId is required.";
        if (!employeeId.HasValue) return "EmployeeId is required.";
        if (remarks?.Length > 2000) return "Remarks cannot exceed 2000 characters.";
        return null;
    }

    private static void ApplyLine(EmployeePayrollLine line, EmployeePayrollLineRequest request, Domain.Entities.MasterData.PayrollComponent component)
    {
        if (line.PayrollComponentId != component.Id)
        {
            line.IsTaxableSnapshot = component.IsTaxable;
            line.IsStatutorySnapshot = component.IsStatutory;
            line.ContributionSideSnapshot = component.ContributionSide;
        }
        line.PayrollComponentId = component.Id;
        line.ComponentCode = component.Code!;
        line.ComponentName = component.Name;
        line.ComponentType = component.Category;
        line.Amount = request.Amount!.Value;
        line.Quantity = request.Quantity;
        line.Rate = request.Rate;
        line.Remarks = Clean(request.Remarks);
    }

    private static bool ValidAmount(decimal value) => value >= 0 && value <= MaximumAmount && HasScaleFour(value);
    private static bool HasScaleFour(decimal value) => decimal.Round(value, 4) == value;
    private static bool IsActivePeriod(string status) => ActivePeriodStatuses.Contains(status, StringComparer.Ordinal);
    private static string? NormalizeStatus(string? status) => PayrollStatuses.FirstOrDefault(value => value.Equals(status?.Trim(), StringComparison.OrdinalIgnoreCase));
    private static string EmployeeName(Domain.Entities.Employees.Employee employee) => $"{employee.PreferredName ?? employee.FirstName} {employee.LastName}";
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static EmployeePayrollDto ToDto(EmployeePayroll item) => new(item.EmployeePayrollId, item.PayrollPeriodId, item.EmployeeId,
        item.BasicSalary, item.GrossPay, item.TotalDeductions, item.NetPay, item.TaxableEarnings, item.Status, item.Remarks, item.CreatedAt, item.UpdatedAt,
        item.ApprovedAt, item.PaidAt, item.CancelledAt, item.CancellationReason);

    private static EmployeePayrollLineDto ToDto(EmployeePayrollLine item) => new(item.EmployeePayrollLineId, item.EmployeePayrollId,
        item.PayrollComponentId, item.ComponentCode, item.ComponentName, item.ComponentType, item.Amount, item.Quantity, item.Rate,
        item.Remarks, item.SourceType, item.SourceId, item.CalculationMethodSnapshot, item.RuleCode, item.RuleName,
        item.ApplicationMode, item.BaseType, item.BaseAmount, item.MinimumBase, item.MaximumBase, item.CalculationRate,
        item.IsTaxableSnapshot, item.IsStatutorySnapshot, item.ContributionSideSnapshot);

    private static bool IsUniqueViolation(DbUpdateException ex) => ex.InnerException is SqlException { Number: 2601 or 2627 };
    private static ServiceResult<T> Invalid<T>(string message) => ServiceResult<T>.Fail("validation", message);
    private static ServiceResult<T> NotFound<T>(string message) => ServiceResult<T>.Fail("not_found", message);
    private static ServiceResult<T> Conflict<T>(string message) => ServiceResult<T>.Fail("conflict", message);
}
