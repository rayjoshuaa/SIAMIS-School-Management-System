namespace SIAMIS.Application.Payroll;

/// <summary>Version 1 historical salary explanation; identifiers are snapshots, not foreign keys.</summary>
public sealed record BasicSalaryCalculationSnapshot(
    int Version, string CalculationType, string ProrationMethod, string ConfigurationSource,
    DateOnly PayrollPeriodStart, DateOnly PayrollPeriodEnd, string Currency,
    int PayableEmploymentDays, bool WasProrated, bool FullMonthEntitlement,
    decimal Amount, IReadOnlyList<BasicSalaryCalculationSegment> Segments);

public sealed record BasicSalaryCalculationSegment(
    DateOnly StartDate, DateOnly EndDate, Guid EmploymentRecordId,
    DateOnly EmploymentStart, DateOnly? EmploymentEnd,
    Guid CompensationId, DateOnly CompensationEffectiveFrom, DateOnly? CompensationEffectiveTo,
    string PayTypeCode, string Currency, decimal MonthlySalary, int PayableDays,
    decimal DailyRate, decimal RawThirtyDayAmount, decimal SegmentAmount,
    bool FullMonthAllocation, bool WasCapped);

public sealed record BasicSalaryEntitlementResult(
    string Status, string Message, BasicSalaryCalculationSnapshot? Snapshot = null);

public interface IBasicSalaryEntitlementService
{
    Task<BasicSalaryEntitlementResult> CalculateAsync(Guid employeeId, DateOnly start, DateOnly end, CancellationToken ct);
}

public static class BasicSalaryPeriod
{
    public static bool IsSupported(DateOnly start, DateOnly end)
        => start.Day == 1 && end == new DateOnly(start.Year, start.Month, DateTime.DaysInMonth(start.Year, start.Month));

    public const string ValidationMessage = "Basic Salary calculation supports only one complete calendar month (first through last day of the same month).";
}
