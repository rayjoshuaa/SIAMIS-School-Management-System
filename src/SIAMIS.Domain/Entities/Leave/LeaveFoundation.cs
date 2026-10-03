using SIAMIS.Domain.Common;

namespace SIAMIS.Domain.Entities.Leave;

public abstract class LeaveFoundationRecord : IHasTimestamps
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class WorkCalendar : LeaveFoundationRecord
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    // Administrative suggestion only. Never used by date resolution.
    public bool IsDefault { get; set; }
}

public sealed class WorkCalendarWeeklyInterval : LeaveFoundationRecord
{
    public Guid WorkCalendarId { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
}

public sealed class WorkCalendarDateOverride : LeaveFoundationRecord
{
    public Guid WorkCalendarId { get; set; }
    public DateOnly Date { get; set; }
    public string OverrideType { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Description { get; set; }
}

public sealed class WorkCalendarOverrideInterval : LeaveFoundationRecord
{
    public Guid WorkCalendarDateOverrideId { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
}

public sealed class EmployeeWorkCalendarAssignment : LeaveFoundationRecord
{
    public Guid EmployeeId { get; set; }
    public Guid WorkCalendarId { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
}

public sealed class LeavePolicy : LeaveFoundationRecord
{
    public Guid LeaveTypeId { get; set; }
    public string Version { get; set; } = string.Empty;
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public string Status { get; set; } = "Draft";
    public DateTime? PublishedAt { get; set; }
    public bool BalanceTracked { get; set; }
    public int? ForeseeableNoticeHours { get; set; }
    public bool AllowsSuddenRequest { get; set; }
    public string SupportingDocumentPolicy { get; set; } = "None";
    public Guid? DocumentTypeId { get; set; }
    public int? CertificateAfterConsecutiveDays { get; set; }
    public bool CertificateOnMondayWorkingDate { get; set; }
    public bool CertificateOnFridayWorkingDate { get; set; }
    public bool SandwichParticipation { get; set; }
    public int? SandwichEquivalentDayMinutes { get; set; }
}

public sealed class EmployeeLeaveEntitlement : LeaveFoundationRecord
{
    public Guid EmployeeId { get; set; }
    public Guid LeaveTypeId { get; set; }
    public int LeaveYear { get; set; }
    public int EntitledMinutes { get; set; }
}

public sealed class EmployeeLeaveEntitlementAdjustment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EmployeeLeaveEntitlementId { get; set; }
    public int AdjustmentMinutes { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
