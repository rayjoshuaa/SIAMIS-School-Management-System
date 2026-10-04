using SIAMIS.Application.Employees;

namespace SIAMIS.Infrastructure.Services;

/// <summary>Prospective whole-minute classification; calendar-year budgets never cross years.</summary>
public static class LeavePaymentAllocation
{
    public static LeaveCalculationSnapshot Classify(LeaveCalculationSnapshot source, IReadOnlyDictionary<int, long> available)
    {
        var remaining = source.Allocations.ToDictionary(a => a.LeaveYear, a => source.IsPaid == true && source.BalanceTracked
            ? Math.Max(0L, available[a.LeaveYear]) : 0L);
        var dates = source.Dates.Select(d =>
        {
            var segments = new List<ClassifiedLeaveInterval>();
            foreach (var interval in d.ChargedIntervals)
            {
                int minutes = LeaveRequestCalculator.Minutes(interval);
                int paid = source.IsPaid != true ? 0 : !source.BalanceTracked ? minutes : (int)Math.Min(minutes, remaining[d.Date.Year]);
                var boundary = interval.StartTime.AddMinutes(paid);
                if (paid > 0) segments.Add(new(interval.StartTime, boundary, true));
                if (paid < minutes) segments.Add(new(boundary, interval.EndTime, false));
                remaining[d.Date.Year] -= paid;
            }
            return d with { PaymentIntervals = segments };
        }).ToArray();
        var allocations = source.Allocations.Select(a =>
        {
            int paid = dates.Where(d => d.Date.Year == a.LeaveYear).SelectMany(d => d.PaymentIntervals!)
                .Where(i => i.IsPaid).Sum(i => LeaveRequestCalculator.Minutes(new(i.StartTime, i.EndTime)));
            return a with { PaidMinutes = paid, UnpaidMinutes = a.ChargeableMinutes - paid };
        }).ToArray();
        return source with { Version = 2, Dates = dates, Allocations = allocations };
    }
}
