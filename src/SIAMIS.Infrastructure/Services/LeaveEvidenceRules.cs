using SIAMIS.Application.Employees;

namespace SIAMIS.Infrastructure.Services;

/// <summary>Uses only frozen D8C facts. Certificate eligibility is unchanged.</summary>
public static class LeaveEvidenceRules
{
    public static ServiceResult<IReadOnlyList<Guid>> RequiredTypes(LeaveCalculationSnapshot s)
    {
        var ids = new HashSet<Guid>();
        foreach (var d in s.Dates.Where(x => x.ChargeableMinutes > 0))
        {
            var p = d.Policy;
            bool required = p.SupportingDocumentPolicy == "AlwaysRequired" || p.SupportingDocumentPolicy == "Conditional" &&
                (p.CertificateAfterConsecutiveDays.HasValue && s.LongestConsecutiveQualifyingDays > p.CertificateAfterConsecutiveDays
                || p.CertificateOnMondayWorkingDate && d.Date.DayOfWeek == DayOfWeek.Monday
                || p.CertificateOnFridayWorkingDate && d.Date.DayOfWeek == DayOfWeek.Friday);
            if (!required) continue;
            if (p.DocumentTypeId is null || p.DocumentTypeId == Guid.Empty)
                return ServiceResult<IReadOnlyList<Guid>>.Fail("conflict", "Frozen required document type is missing; integrity review is required.");
            ids.Add(p.DocumentTypeId.Value);
        }
        return s.SupportingDocumentRequired == (ids.Count > 0)
            ? ServiceResult<IReadOnlyList<Guid>>.Success(ids.Order().ToArray())
            : ServiceResult<IReadOnlyList<Guid>>.Fail("conflict", "Frozen evidence requirement is inconsistent; integrity review is required.");
    }
    public static bool FullBoundary(LeaveDateCalculation d)
        => d.ScheduledIntervals.Count > 0 && d.ScheduledIntervals.Select(x => new SIAMIS.Application.Leave.WorkIntervalDto(x.StartTime, x.EndTime)).SequenceEqual(d.ChargedIntervals);
}
