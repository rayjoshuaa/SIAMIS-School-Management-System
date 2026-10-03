using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SIAMIS.Application.Employees;
using SIAMIS.Application.Payroll;
using SIAMIS.Domain.Entities.Employees;
using SIAMIS.Domain.Entities.Payroll;
using SIAMIS.Infrastructure.Data;

namespace SIAMIS.Infrastructure.Services;

/// <summary>Stored-fact reads and presentation snapshots. No calculator is a dependency.</summary>
public sealed class PayrollOperationsService(SIAMISDbContext db) : IPayrollOperationsService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    internal async Task<IDbContextTransaction> BeginPayrollReadAsync(Guid id, CancellationToken ct)
    {
        // Discover the parent before the transaction, then reuse the writers' parent-first lock order.
        // This prevents a reader holding an old header while waiting for replaced lines/payslip.
        var periodId = await db.EmployeePayrolls.AsNoTracking().Where(x => x.EmployeePayrollId == id)
            .Select(x => (Guid?)x.PayrollPeriodId).SingleOrDefaultAsync(ct);
        var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            if (periodId.HasValue) await PayrollPeriodLock.GetAsync(db, periodId.Value, ct);
            return tx;
        }
        catch { await tx.DisposeAsync(); throw; }
    }

    internal async Task CaptureAsync(EmployeePayroll payroll, Employee employee, PayrollPeriod period,
        PayrollEmploymentContextDto context, string currency, CancellationToken ct)
    {
        // The caller owns the generation transaction. Missing configuration never blocks calculation.
        var organization = await db.OrganizationProfiles.AsNoTracking().SingleOrDefaultAsync(ct);
        if (organization is null) return;
        var financial = await FinancialAsync(payroll, ct);
        var snapshot = new PayslipSnapshot(1, payroll.EmployeePayrollId, DateTime.UtcNow,
            new(organization.OrganizationProfileId, organization.DisplayName, organization.AddressLine1, organization.AddressLine2),
            new(employee.EmployeeId, employee.EmployeeNumber, PayrollDisplayName.Format(employee.PreferredName, employee.FirstName, employee.MiddleName, employee.LastName)),
            context, new(period.PayrollPeriodId, period.Code, period.Name, period.StartDate, period.EndDate, period.PayDate),
            currency, financial.Earnings, financial.Deductions, financial.Statutory, Totals(payroll));
        db.EmployeePayslips.Add(new() { EmployeePayrollId = payroll.EmployeePayrollId, SnapshotJson = Serialize(snapshot) });
        await db.SaveChangesAsync(ct);
    }

    internal async Task<string?> RefreshAsync(EmployeePayroll payroll, CancellationToken ct)
    {
        if (payroll.Status != "Calculated") return null;
        var stored = await db.EmployeePayslips.SingleOrDefaultAsync(x => x.EmployeePayrollId == payroll.EmployeePayrollId, ct);
        // Never manufacture missing historical identity from mutable data during a manual adjustment.
        if (stored is null) return null;
        var previous = Parse(stored);
        if (previous is null) return "Payslip snapshot is invalid; manual adjustment was not saved.";
        var financial = await FinancialAsync(payroll, ct);
        stored.SnapshotJson = Serialize(previous with { Earnings = financial.Earnings, Deductions = financial.Deductions,
            Statutory = financial.Statutory, Totals = Totals(payroll) });
        await db.SaveChangesAsync(ct);
        return null;
    }

    public async Task<ServiceResult<PayslipDto>> GetPayslipAsync(Guid id, CancellationToken ct)
    {
        await using var tx = await BeginPayrollReadAsync(id, ct);
        var payroll = await db.EmployeePayrolls.AsNoTracking().SingleOrDefaultAsync(x => x.EmployeePayrollId == id, ct);
        if (payroll is null) return ServiceResult<PayslipDto>.Fail("not_found", "Employee payroll was not found.");
        if (payroll.Status == "Draft") return ServiceResult<PayslipDto>.Fail("conflict", "Draft payroll is not eligible for a formal payslip.");
        var findings = await InspectAsync(payroll, true, ct);
        if (findings.Count != 0) return ServiceResult<PayslipDto>.Fail("conflict", findings[0].Message);
        var stored = await db.EmployeePayslips.AsNoTracking().SingleAsync(x => x.EmployeePayrollId == id, ct);
        var response = new PayslipDto(stored.EmployeePayslipId, payroll.Status, payroll.ApprovedAt, payroll.PaidAt,
            payroll.CancelledAt, stored.SnapshotVersion, DateTime.SpecifyKind(stored.CreatedAt, DateTimeKind.Utc),
            DateTime.SpecifyKind(stored.UpdatedAt, DateTimeKind.Utc), Parse(stored)!);
        await tx.CommitAsync(ct);
        return ServiceResult<PayslipDto>.Success(response);
    }

    public async Task<ServiceResult<PayrollReviewDto>> ReviewAsync(Guid id, CancellationToken ct)
    {
        await using var tx = await BeginPayrollReadAsync(id, ct);
        var payroll = await db.EmployeePayrolls.AsNoTracking().SingleOrDefaultAsync(x => x.EmployeePayrollId == id, ct);
        if (payroll is null) return ServiceResult<PayrollReviewDto>.Fail("not_found", "Employee payroll was not found.");
        var review = await ReviewWithinTransactionAsync(payroll, ct);
        await tx.CommitAsync(ct);
        return ServiceResult<PayrollReviewDto>.Success(review);
    }

    internal async Task<PayrollReviewDto> ReviewWithinTransactionAsync(EmployeePayroll payroll, CancellationToken ct)
    {
        var findings = await InspectAsync(payroll, true, ct);
        var ready = payroll.Status != "Draft" && findings.Count == 0;
        var periodStatus = await db.PayrollPeriods.Where(x => x.PayrollPeriodId == payroll.PayrollPeriodId).Select(x => x.Status).SingleAsync(ct);
        var canApprove = ready && payroll.Status == "Calculated" && PayrollPeriodLock.AllowsMutation(periodStatus);
        if (payroll.Status != "Calculated") findings.Add(new("status_not_approvable", $"Only Calculated payroll can be approved. Current status: {payroll.Status}."));
        if (!PayrollPeriodLock.AllowsMutation(periodStatus)) findings.Add(new("period_not_editable", PayrollPeriodLock.ConflictMessage(periodStatus)));
        return new(payroll.EmployeePayrollId, payroll.Status, canApprove, ready, findings);
    }

    internal async Task<PayrollOperationalDto> OperationalAsync(EmployeePayroll payroll, CancellationToken ct)
    {
        var stored = await db.EmployeePayslips.AsNoTracking().SingleOrDefaultAsync(x => x.EmployeePayrollId == payroll.EmployeePayrollId, ct);
        var snapshot = stored is null ? null : Parse(stored);
        var financial = await FinancialAsync(payroll, ct);
        return new(snapshot is not null, snapshot, await CurrencyAsync(payroll.EmployeePayrollId, ct), financial.Statutory,
            financial.Earnings, financial.Deductions, await ReviewWithinTransactionAsync(payroll, ct));
    }

    internal async Task<List<PayrollReviewFinding>> InspectAsync(EmployeePayroll payroll, bool requirePayslip, CancellationToken ct)
    {
        var findings = new List<PayrollReviewFinding>();
        void Fail(string code, string message) => findings.Add(new(code, message));
        var lines = await db.EmployeePayrollLines.AsNoTracking().Where(x => x.EmployeePayrollId == payroll.EmployeePayrollId).ToListAsync(ct);
        if (lines.Count == 0) Fail("missing_lines", "Stored payroll has no calculation lines.");
        if (lines.Any(x => x.Amount <= 0 || x.ComponentType is not ("Earning" or "Deduction") ||
            (x.SourceType switch { "BasicSalary" or "Manual" => x.SourceId is not null,
                "Assignment" or "PayrollRule" or "Statutory" => x.SourceId is null, _ => true })))
            Fail("line_provenance", "Stored line type, amount or source identity is inconsistent.");
        var earnings = lines.Where(x => x.ComponentType == "Earning").Sum(x => x.Amount);
        var deductions = lines.Where(x => x.ComponentType == "Deduction").Sum(x => x.Amount);
        if (payroll.GrossPay != earnings || payroll.TotalDeductions != deductions || payroll.NetPay != earnings - deductions
            || payroll.TaxableEarnings != lines.Where(x => x.ComponentType == "Earning" && x.IsTaxableSnapshot).Sum(x => x.Amount)
            || payroll.NetPay < 0)
            Fail("totals_mismatch", "Stored payroll totals do not reconcile with stored lines. No amounts were recalculated or changed.");
        var basic = lines.Where(x => x.SourceType == "BasicSalary").ToArray();
        var salary = basic.Length == 1 ? ParseSalary(basic[0].BasicSalaryCalculationSnapshotJson) : null;
        if (payroll.Status != "Draft" && (salary is null || basic[0].ComponentType != "Earning" || basic[0].Amount != payroll.BasicSalary
            || salary.Amount != payroll.BasicSalary))
            Fail("salary_snapshot", "Generated Basic Salary entitlement evidence is missing or inconsistent.");
        var currency = await CurrencyAsync(payroll.EmployeePayrollId, ct);
        if (currency is null) Fail("currency_unresolved", "Authoritative D3 payroll currency is missing or invalid.");

        var pit = await db.EmployeePayrollPitResults.AsNoTracking().SingleOrDefaultAsync(x => x.EmployeePayrollId == payroll.EmployeePayrollId, ct);
        var pitLines = lines.Where(x => x.PayrollComponentId == PitPayrollService.ComponentId || x.ComponentCode == "DEDUCT-002").ToArray();
        if (pit is null ? pitLines.Length != 0 : pit.EmployeeId != payroll.EmployeeId || pit.PayrollPeriodId != payroll.PayrollPeriodId
            || pit.Currency != currency || !MatchesStatutory(pitLines, pit.EmployeePayrollPitResultId, pit.CurrentWithholding))
            Fail("pit_integrity", "Stored PIT result owner, currency, deduction amount or provenance is inconsistent.");

        var sso = await db.EmployeePayrollStatutoryResults.AsNoTracking().Include(x => x.SocialSecurity)
            .Where(x => x.EmployeePayrollId == payroll.EmployeePayrollId).ToListAsync(ct);
        var ssoLines = lines.Where(x => x.ComponentCode == "DEDUCT-001").ToArray();
        if (sso.Count == 0)
        {
            // D5 permits ordinary/manual DEDUCT-001 when no Applicable SSO result exists.
            if (ssoLines.Any(x => x.SourceType == "Statutory")) Fail("sso_integrity", "Statutory SSO deduction has no authoritative stored result.");
        }
        else
        {
            var result = sso[0];
            var evidence = ParseSso(result.CalculationSnapshotJson);
            var owner = await db.EmployeeStatutoryEnrollments.AsNoTracking().Where(x => x.EmployeeStatutoryEnrollmentId == result.EmployeeStatutoryEnrollmentId)
                .Select(x => x.EmployeeId).SingleOrDefaultAsync(ct);
            if (sso.Count != 1 || result.SocialSecurity is null || owner != payroll.EmployeeId || evidence is null
                || evidence.StatutorySchemeId != result.StatutorySchemeId || evidence.StatutoryPolicyVersionId != result.StatutoryPolicyVersionId
                || evidence.EmployeeStatutoryEnrollmentId != result.EmployeeStatutoryEnrollmentId
                || evidence.CalculationMethodVersion != result.CalculationMethodVersion || evidence.Currency != result.Currency
                || evidence.ContributionMonth != result.ContributionMonth || evidence.GoverningDate != result.GoverningDate
                || evidence.EmployeeAmount != result.SocialSecurity.EmployeeAmount || evidence.EmployerAmount != result.SocialSecurity.EmployerAmount
                || result.Currency != currency || salary is null || result.GoverningDate != salary.PayrollPeriodEnd
                || !MatchesStatutory(ssoLines, result.EmployeePayrollStatutoryResultId, result.SocialSecurity.EmployeeAmount)
                || lines.Any(x => x.SourceId == result.EmployeePayrollStatutoryResultId && x.ComponentCode != "DEDUCT-001"))
                Fail("sso_integrity", "Stored SSO owner, currency, contribution period, employee deduction or provenance is inconsistent. Employer SSO must remain separate.");
        }
        if (requirePayslip)
        {
            var stored = await db.EmployeePayslips.AsNoTracking().SingleOrDefaultAsync(x => x.EmployeePayrollId == payroll.EmployeePayrollId, ct);
            var snapshot = stored is null ? null : Parse(stored);
            if (snapshot is null)
            {
                if (!await db.OrganizationProfiles.AsNoTracking().AnyAsync(ct))
                    Fail("organization_unconfigured", "Employer organization profile is not configured.");
                Fail("payslip_missing", "A complete stored payslip is unavailable. Configure the organization and deliberately regenerate editable payroll; historical backfill is prohibited.");
            }
            else
            {
                var financial = await FinancialAsync(payroll, ct);
                if (snapshot.EmployeePayrollId != payroll.EmployeePayrollId || snapshot.Employee.EmployeeId != payroll.EmployeeId
                    || snapshot.Period.PayrollPeriodId != payroll.PayrollPeriodId || snapshot.Currency != currency
                    || snapshot.Totals != Totals(payroll) || snapshot.Statutory != financial.Statutory
                    || Serialize(snapshot.Earnings) != Serialize(financial.Earnings) || Serialize(snapshot.Deductions) != Serialize(financial.Deductions))
                    Fail("payslip_inconsistent", "Stored payslip does not match its owning payroll, currency, lines or totals.");
            }
        }
        return findings;
    }

    public async Task<ServiceResult<PayrollPeriodOperationsDto>> GetPeriodSummaryAsync(Guid id, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var period = await PayrollPeriodLock.GetAsync(db, id, ct);
        if (period is null) return ServiceResult<PayrollPeriodOperationsDto>.Fail("not_found", "Payroll period was not found.");
        var headers = await db.EmployeePayrolls.AsNoTracking().Where(x => x.PayrollPeriodId == id).ToListAsync(ct);
        var ids = headers.Select(x => x.EmployeePayrollId).ToArray();
        var salaryRows = await db.EmployeePayrollLines.AsNoTracking().Where(x => ids.Contains(x.EmployeePayrollId) && x.SourceType == "BasicSalary")
            .Select(x => new { x.EmployeePayrollId, x.BasicSalaryCalculationSnapshotJson }).ToListAsync(ct);
        var ssoRows = await db.EmployeePayrollStatutoryResults.AsNoTracking().Where(x => ids.Contains(x.EmployeePayrollId))
            .Select(x => new { x.EmployeePayrollId, x.SocialSecurity.EmployeeAmount, x.SocialSecurity.EmployerAmount }).ToListAsync(ct);
        var pitRows = await db.EmployeePayrollPitResults.AsNoTracking().Where(x => ids.Contains(x.EmployeePayrollId))
            .Select(x => new { x.EmployeePayrollId, x.CurrentWithholding }).ToListAsync(ct);
        var resolved = new List<(EmployeePayroll Payroll, string Currency, PayslipStatutorySummary Statutory)>();
        foreach (var header in headers)
        {
            var currency = ResolveCurrency(salaryRows.Where(x => x.EmployeePayrollId == header.EmployeePayrollId)
                .Select(x => x.BasicSalaryCalculationSnapshotJson).ToArray());
            var sso = ssoRows.Where(x => x.EmployeePayrollId == header.EmployeePayrollId).ToArray();
            var pit = pitRows.SingleOrDefault(x => x.EmployeePayrollId == header.EmployeePayrollId);
            if (currency is not null) resolved.Add((header, currency,
                new(sso.Length == 0 ? null : sso.Sum(x => x.EmployeeAmount), sso.Length == 0 ? null : sso.Sum(x => x.EmployerAmount), pit?.CurrentWithholding)));
        }
        var groups = resolved.GroupBy(x => x.Currency).OrderBy(x => x.Key).Select(g => new PayrollCurrencySummary(g.Key,
            g.Select(x => x.Payroll.EmployeeId).Distinct().Count(), g.Sum(x => x.Payroll.BasicSalary), g.Sum(x => x.Payroll.GrossPay),
            g.Sum(x => x.Payroll.TaxableEarnings), g.Sum(x => x.Payroll.TotalDeductions), g.Sum(x => x.Payroll.NetPay),
            g.Sum(x => x.Statutory.EmployeeSso ?? 0m), g.Sum(x => x.Statutory.EmployerSso ?? 0m), g.Sum(x => x.Statutory.PitWithholding ?? 0m))).ToArray();
        var response = new PayrollPeriodOperationsDto(new(period.PayrollPeriodId, period.Code, period.Name, period.StartDate,
            period.EndDate, period.PayDate, period.Status), headers.Count, headers.Select(x => x.EmployeeId).Distinct().Count(),
            headers.GroupBy(x => x.Status).OrderBy(g => g.Key).Select(g => new PayrollStatusCount(g.Key, g.Count())).ToArray(), groups,
            headers.Count - resolved.Count);
        await tx.CommitAsync(ct);
        return ServiceResult<PayrollPeriodOperationsDto>.Success(response);
    }

    internal async Task<string?> CurrencyAsync(Guid id, CancellationToken ct)
    {
        var json = await db.EmployeePayrollLines.AsNoTracking().Where(x => x.EmployeePayrollId == id && x.SourceType == "BasicSalary")
            .Select(x => x.BasicSalaryCalculationSnapshotJson).ToListAsync(ct);
        return ResolveCurrency(json);
    }

    private static string? ResolveCurrency(IReadOnlyList<string?> json)
    {
        var salary = json.Count == 1 ? ParseSalary(json[0]) : null;
        return salary is { Version: 1 } && salary.Currency is { Length: 3 } currency
            && currency.All(c => c is >= 'A' and <= 'Z') ? currency : null;
    }

    internal async Task<PayslipSnapshot?> FrozenAsync(Guid id, CancellationToken ct)
    {
        var stored = await db.EmployeePayslips.AsNoTracking().SingleOrDefaultAsync(x => x.EmployeePayrollId == id, ct);
        return stored is null ? null : Parse(stored);
    }
    private async Task<(IReadOnlyList<PayslipLine> Earnings, IReadOnlyList<PayslipLine> Deductions, PayslipStatutorySummary Statutory)> FinancialAsync(EmployeePayroll payroll, CancellationToken ct)
    {
        var lines = await db.EmployeePayrollLines.AsNoTracking().Where(x => x.EmployeePayrollId == payroll.EmployeePayrollId)
            .OrderBy(x => x.ComponentCode).ThenBy(x => x.EmployeePayrollLineId)
            .Select(x => new PayslipLine(x.EmployeePayrollLineId, x.PayrollComponentId, x.ComponentCode, x.ComponentName, x.ComponentType,
                x.Amount, x.SourceType, x.SourceId, x.Remarks)).ToListAsync(ct);
        var ssoRows = await db.EmployeePayrollStatutoryResults.AsNoTracking().Where(x => x.EmployeePayrollId == payroll.EmployeePayrollId)
            .Select(x => new { x.SocialSecurity.EmployeeAmount, x.SocialSecurity.EmployerAmount }).ToListAsync(ct);
        var sso = ssoRows.Count == 1 ? ssoRows[0] : null;
        var pit = await db.EmployeePayrollPitResults.AsNoTracking().Where(x => x.EmployeePayrollId == payroll.EmployeePayrollId)
            .Select(x => (decimal?)x.CurrentWithholding).SingleOrDefaultAsync(ct);
        return (lines.Where(x => x.ComponentType == "Earning").ToArray(), lines.Where(x => x.ComponentType == "Deduction").ToArray(),
            new(sso?.EmployeeAmount, sso?.EmployerAmount, pit));
    }
    private static bool MatchesStatutory(EmployeePayrollLine[] lines, Guid id, decimal amount)
        => amount == 0 ? lines.Length == 0 : amount > 0 && lines.Length == 1 && lines[0].ComponentType == "Deduction"
            && lines[0].SourceType == "Statutory" && lines[0].SourceId == id && lines[0].Amount == amount;
    private static PayslipTotals Totals(EmployeePayroll x) => new(x.BasicSalary, x.GrossPay, x.TaxableEarnings, x.TotalDeductions, x.NetPay);
    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Json);
    private static BasicSalaryCalculationSnapshot? ParseSalary(string? json)
    {
        try { return json is null ? null : JsonSerializer.Deserialize<BasicSalaryCalculationSnapshot>(json, Json); }
        catch (JsonException) { return null; }
    }
    private static Section33CalculationSnapshot? ParseSso(string json)
    {
        try { return JsonSerializer.Deserialize<Section33CalculationSnapshot>(json, Json); }
        catch (JsonException) { return null; }
    }
    private static PayslipSnapshot? Parse(EmployeePayslip stored)
    {
        try
        {
            var snapshot = JsonSerializer.Deserialize<PayslipSnapshot>(stored.SnapshotJson, Json);
            return stored.SnapshotVersion == 1 && snapshot is { Version: 1, Employee: not null, Employer: not null,
                EmploymentContext: not null, Period: not null, Totals: not null, Statutory: not null, Earnings: not null, Deductions: not null }
                && !string.IsNullOrWhiteSpace(snapshot.Employer.DisplayName) && !string.IsNullOrWhiteSpace(snapshot.Employee.EmployeeCode)
                && !string.IsNullOrWhiteSpace(snapshot.Employee.DisplayName) ? snapshot : null;
        }
        catch (JsonException) { return null; }
    }
}
