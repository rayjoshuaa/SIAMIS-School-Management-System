using System.Data;
using Microsoft.EntityFrameworkCore;
using SIAMIS.Application.Employees;
using SIAMIS.Application.Payroll;
using SIAMIS.Domain.Entities.Payroll;
using SIAMIS.Infrastructure.Data;

namespace SIAMIS.Infrastructure.Services;

public sealed class PitPaymentScheduleService(SIAMISDbContext db) : IPitPaymentScheduleService
{
    public async Task<ServiceResult<PitPaymentScheduleDto>> CreateAsync(Guid employeeId, PitPaymentScheduleRequest r, CancellationToken ct)
    {
        var error = PitPaymentScheduleValidation.Validate(r.TaxYear, r.Evidence, r.Entries, false);
        if (error is not null) return Fail("validation", error);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (await EmploymentIntegrity.LockAsync(db, employeeId, ct) is null) return Fail("not_found", "Employee was not found.");
        var revisions = await db.EmployeePitPaymentSchedules.Where(x => x.EmployeeId == employeeId && x.TaxYear == r.TaxYear).ToListAsync(ct);
        if (revisions.Any(x => x.Status == "Draft")) return Fail("conflict", "A Draft revision already exists for this employee/year.");
        var selected = await db.EmployeePitPaymentScheduleSelections.SingleOrDefaultAsync(x => x.EmployeeId == employeeId && x.TaxYear == r.TaxYear, ct);
        var row = new EmployeePitPaymentSchedule { EmployeeId = employeeId, TaxYear = r.TaxYear,
            RevisionNumber = revisions.Select(x => x.RevisionNumber).DefaultIfEmpty(0).Max() + 1,
            ReplacesScheduleId = selected?.CurrentScheduleId, Evidence = r.Evidence.Trim() };
        row.Entries = r.Entries.Select(x => new EmployeePitPaymentScheduleEntry { EmployeePitPaymentScheduleId = row.EmployeePitPaymentScheduleId,
            PaymentOrdinal = x.PaymentOrdinal, PlannedPayDate = x.PlannedPayDate }).ToList();
        db.EmployeePitPaymentSchedules.Add(row);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return ServiceResult<PitPaymentScheduleDto>.Success(ToDto(row, false));
    }

    public async Task<ServiceResult<PitPaymentScheduleDto>> GetAsync(Guid employeeId, Guid id, CancellationToken ct)
    {
        var row = await db.EmployeePitPaymentSchedules.AsNoTracking().Include(x => x.Entries)
            .SingleOrDefaultAsync(x => x.EmployeeId == employeeId && x.EmployeePitPaymentScheduleId == id, ct);
        if (row is null) return Fail("not_found", "Schedule was not found for this employee.");
        var selected = await db.EmployeePitPaymentScheduleSelections.AsNoTracking().AnyAsync(x => x.EmployeeId == employeeId && x.TaxYear == row.TaxYear && x.CurrentScheduleId == id, ct);
        return ServiceResult<PitPaymentScheduleDto>.Success(ToDto(row, selected));
    }

    public async Task<ServiceResult<PitPaymentScheduleDto>> UpdateDraftAsync(Guid employeeId, Guid id, PitPaymentScheduleRequest r, CancellationToken ct)
    {
        var error = PitPaymentScheduleValidation.Validate(r.TaxYear, r.Evidence, r.Entries, false);
        if (error is not null) return Fail("validation", error);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (await EmploymentIntegrity.LockAsync(db, employeeId, ct) is null) return Fail("not_found", "Employee was not found.");
        var row = await db.EmployeePitPaymentSchedules.Include(x => x.Entries).SingleOrDefaultAsync(x => x.EmployeeId == employeeId && x.EmployeePitPaymentScheduleId == id, ct);
        if (row is null) return Fail("not_found", "Schedule was not found for this employee.");
        if (row.Status != "Draft") return Fail("conflict", "Verified schedules are immutable; create a replacement Draft.");
        if (r.TaxYear != row.TaxYear) return Fail("validation", "A revision's TaxYear cannot be reassigned.");
        db.EmployeePitPaymentScheduleEntries.RemoveRange(row.Entries);
        await db.SaveChangesAsync(ct);
        row.Entries = r.Entries.Select(x => new EmployeePitPaymentScheduleEntry { EmployeePitPaymentScheduleId = id,
            PaymentOrdinal = x.PaymentOrdinal, PlannedPayDate = x.PlannedPayDate }).ToList();
        // Explicit Added state: entry GUIDs are assigned by the domain before EF observes the collection.
        db.EmployeePitPaymentScheduleEntries.AddRange(row.Entries);
        row.Evidence = r.Evidence.Trim(); row.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return ServiceResult<PitPaymentScheduleDto>.Success(ToDto(row, false));
    }

    public async Task<ServiceResult<PitPaymentScheduleDto>> VerifyAsync(Guid employeeId, Guid id, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (await EmploymentIntegrity.LockAsync(db, employeeId, ct) is null) return Fail("not_found", "Employee was not found.");
        var row = await db.EmployeePitPaymentSchedules.Include(x => x.Entries).SingleOrDefaultAsync(x => x.EmployeeId == employeeId && x.EmployeePitPaymentScheduleId == id, ct);
        if (row is null) return Fail("not_found", "Schedule was not found for this employee.");
        if (row.Status != "Draft") return Fail("conflict", "Verified schedules are immutable; create a replacement Draft.");
        var error = PitPaymentScheduleValidation.Validate(row.TaxYear, row.Evidence, Entries(row), true);
        if (error is not null) return Fail("validation", error);
        var selection = await db.EmployeePitPaymentScheduleSelections.SingleOrDefaultAsync(x => x.EmployeeId == employeeId && x.TaxYear == row.TaxYear, ct);
        if (row.ReplacesScheduleId != selection?.CurrentScheduleId) return Fail("conflict", "The selected predecessor changed; this Draft cannot replace it.");
        row.Status = "Verified"; row.VerifiedAt = DateTime.UtcNow;
        if (selection is null) db.EmployeePitPaymentScheduleSelections.Add(new() { EmployeeId = employeeId, TaxYear = row.TaxYear, CurrentScheduleId = id });
        else selection.CurrentScheduleId = id;
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return ServiceResult<PitPaymentScheduleDto>.Success(ToDto(row, true));
    }

    public async Task<ServiceResult<PitPaymentScheduleDto?>> CurrentAsync(Guid employeeId, int taxYear, CancellationToken ct)
    {
        if (!await db.Employees.AsNoTracking().AnyAsync(x => x.EmployeeId == employeeId, ct))
            return ServiceResult<PitPaymentScheduleDto?>.Fail("not_found", "Employee was not found.");
        var id = await db.EmployeePitPaymentScheduleSelections.AsNoTracking().Where(x => x.EmployeeId == employeeId && x.TaxYear == taxYear)
            .Select(x => (Guid?)x.CurrentScheduleId).SingleOrDefaultAsync(ct);
        if (id is null) return ServiceResult<PitPaymentScheduleDto?>.Success(null);
        var row = await db.EmployeePitPaymentSchedules.AsNoTracking().Include(x => x.Entries).SingleAsync(x => x.EmployeePitPaymentScheduleId == id, ct);
        return ServiceResult<PitPaymentScheduleDto?>.Success(ToDto(row, true));
    }

    public async Task<PitScheduleResolution> ResolveAsync(Guid employeeId, DateOnly payDate, CancellationToken ct)
    {
        var current = (await CurrentAsync(employeeId, payDate.Year, ct)).Value;
        if (current is null || current.Status != "Verified") return new("RequiresReview", "No selected Verified PIT payment schedule covers this tax year.", current, null);
        var error = PitPaymentScheduleValidation.Validate(current.TaxYear, current.Evidence, current.Entries, true);
        var matches = current.Entries.Where(x => x.PlannedPayDate == payDate).ToArray();
        if (error is not null || matches.Length != 1) return new("RequiresReview", error ?? "PayDate requires exactly one matching verified schedule entry.", current, null);
        return new("Resolved", "Exact selected Verified schedule entry.", current, new(current.Entries.Count, matches[0].PaymentOrdinal, current.Evidence));
    }
    private static IReadOnlyList<PitPaymentScheduleEntryRequest> Entries(EmployeePitPaymentSchedule row)
        => row.Entries.OrderBy(x => x.PaymentOrdinal).Select(x => new PitPaymentScheduleEntryRequest(x.PaymentOrdinal, x.PlannedPayDate)).ToArray();
    private static PitPaymentScheduleDto ToDto(EmployeePitPaymentSchedule row, bool selected)
        => new(row.EmployeePitPaymentScheduleId, row.EmployeeId, row.TaxYear, row.RevisionNumber, row.ReplacesScheduleId,
            row.Status, row.Evidence, row.CreatedAt, row.UpdatedAt, row.VerifiedAt, selected, Entries(row));
    private static ServiceResult<PitPaymentScheduleDto> Fail(string code, string message) => ServiceResult<PitPaymentScheduleDto>.Fail(code, message);
}
