using Microsoft.EntityFrameworkCore;
using SIAMIS.Application.Payroll;
using SIAMIS.Domain.Entities.Employees;
using SIAMIS.Infrastructure.Data;

namespace SIAMIS.Infrastructure.Services;

/// <summary>Approved ThirtyDay policy for Monthly compensation, independent of attendance and leave.</summary>
public sealed class BasicSalaryEntitlementService(SIAMISDbContext db) : IBasicSalaryEntitlementService
{
    public async Task<BasicSalaryEntitlementResult> CalculateAsync(Guid employeeId, DateOnly start, DateOnly end, CancellationToken ct)
    {
        if (!BasicSalaryPeriod.IsSupported(start, end)) return Failed(BasicSalaryPeriod.ValidationMessage);
        var methods = await db.PayrollSettings.AsNoTracking().Where(x => x.IsActive)
            .Select(x => x.BasicSalaryProrationMethod).Take(2).ToListAsync(ct);
        if (methods.Count > 1 || (methods.Count == 1 && methods[0] != "ThirtyDay"))
            return Failed("Active PayrollSettings has an invalid or unsupported BasicSalaryProrationMethod; only ThirtyDay is supported.");
        var source = methods.Count == 0 ? "SystemFallback" : "Configured";
        var employment = await db.EmploymentRecords.AsNoTracking().Where(x => x.EmployeeId == employeeId)
            .Where(EmploymentIntegrity.Overlapping(start, end)).OrderBy(x => x.StartDate ?? x.HireDate)
            .ThenBy(x => x.EmploymentRecordId).ToListAsync(ct);
        var compensations = await db.EmployeeCompensations.AsNoTracking().Where(x => x.EmployeeId == employeeId
            && x.EffectiveFrom <= end && (!x.EffectiveTo.HasValue || x.EffectiveTo >= start))
            .Select(x => new Compensation(x.EmployeeCompensationId, x.EffectiveFrom, x.EffectiveTo,
                x.BasicSalary, x.Currency, x.PayType == null ? null : x.PayType.Code)).ToListAsync(ct);

        // At most 31 dates, evaluated in memory after two interval queries, never a query per date.
        var dates = new List<PayableDate>();
        var uncovered = new List<DateOnly>();
        var payableDays = 0;
        for (var offset = 0; offset <= end.DayNumber - start.DayNumber; offset++)
        {
            var date = start.AddDays(offset);
            var record = employment.FirstOrDefault(x => EmploymentIntegrity.Start(x) <= date && (!x.EndDate.HasValue || x.EndDate >= date));
            if (record is null) continue;
            payableDays++;
            var applicable = compensations.Where(x => x.From <= date && (!x.To.HasValue || x.To >= date)).ToArray();
            if (applicable.Length > 1) return Failed($"Ambiguous compensation coverage on {date:yyyy-MM-dd}: multiple compensations apply.");
            if (applicable.Length == 0) { uncovered.Add(date); continue; }
            if (applicable[0].PayTypeCode != "PAY-001")
                return Failed($"Unsupported compensation PayType '{applicable[0].PayTypeCode ?? "missing"}' on {date:yyyy-MM-dd}. Basic Salary entitlement supports Monthly (PAY-001) only.");
            dates.Add(new PayableDate(date, record, applicable[0]));
        }
        if (dates.Count == 0) return new("Skipped", "No applicable compensation covers any payable employment date.");
        if (uncovered.Count > 0) return Failed($"Incomplete compensation coverage: {uncovered.Count} payable employment dates are uncovered, starting {uncovered[0]:yyyy-MM-dd}.");
        var currencies = dates.Select(x => x.Compensation.Currency.Trim().ToUpperInvariant()).Distinct().ToArray();
        if (currencies.Length != 1 || currencies[0].Length != 3)
            return Failed("Mixed or invalid compensation currencies; all payable compensation segments must use the same three-character currency. Currency conversion is unsupported.");

        var fullMonth = payableDays == end.DayNumber - start.DayNumber + 1
            && dates.Select(x => x.Compensation.Salary).Distinct().Count() == 1;
        var groups = new List<List<PayableDate>>();
        foreach (var date in dates)
        {
            var previous = groups.LastOrDefault()?.Last();
            if (previous is null || previous.Date.AddDays(1) != date.Date
                || previous.Employment.EmploymentRecordId != date.Employment.EmploymentRecordId
                || previous.Compensation.Id != date.Compensation.Id) groups.Add([]);
            groups[^1].Add(date);
        }
        var segments = new List<BasicSalaryCalculationSegment>();
        decimal rawTotal = 0;
        foreach (var group in groups)
        {
            var first = group[0];
            var compensation = first.Compensation;
            var raw = compensation.Salary / 30m * group.Count;
            // A full unchanged salary is allocated across explanatory segments by calendar days.
            // This preserves the exact monthly entitlement even when employment/context IDs change.
            var amount = fullMonth ? compensation.Salary * group.Count / payableDays : Math.Min(compensation.Salary, raw);
            rawTotal += amount;
            segments.Add(new(first.Date, group[^1].Date, first.Employment.EmploymentRecordId,
                EmploymentIntegrity.Start(first.Employment), first.Employment.EndDate, compensation.Id,
                compensation.From, compensation.To, compensation.PayTypeCode!, currencies[0], compensation.Salary,
                group.Count, compensation.Salary / 30m, raw, Round(amount), fullMonth, !fullMonth && raw > compensation.Salary));
        }
        var total = fullMonth ? Round(dates[0].Compensation.Salary) : Round(rawTotal);
        // Keep displayed segment amounts reconcilable to the final rounded monetary line.
        segments[^1] = segments[^1] with { SegmentAmount = segments[^1].SegmentAmount + total - segments.Sum(x => x.SegmentAmount) };
        return new("Calculated", "Basic Salary entitlement calculated.", new(1, "BasicSalary", "ThirtyDay", source,
            start, end, currencies[0], payableDays, !fullMonth, fullMonth, total, segments));
    }

    private static decimal Round(decimal amount) => decimal.Round(amount, 4, MidpointRounding.AwayFromZero);
    private static BasicSalaryEntitlementResult Failed(string message) => new("Failed", message);
    private sealed record Compensation(Guid Id, DateOnly From, DateOnly? To, decimal Salary, string Currency, string? PayTypeCode);
    private sealed record PayableDate(DateOnly Date, EmploymentRecord Employment, Compensation Compensation);
}
