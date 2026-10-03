using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using SIAMIS.Application.Leave;
using SIAMIS.Domain.Entities.Leave;
using SIAMIS.Domain.Entities.Employees;

internal static class D8BFoundationContractTests
{
    public static void Run(Action<bool, string> check, IModel model)
    {
        foreach (var type in new[] { typeof(WorkCalendar), typeof(WorkCalendarWeeklyInterval), typeof(WorkCalendarDateOverride), typeof(WorkCalendarOverrideInterval), typeof(EmployeeWorkCalendarAssignment), typeof(LeavePolicy), typeof(EmployeeLeaveEntitlement), typeof(EmployeeLeaveEntitlementAdjustment) })
        {
            var entity = model.FindEntityType(type)!;
            check(entity is not null && entity.BaseType is null, "D8B focused table " + type.Name);
            check(entity!.GetForeignKeys().All(x => x.DeleteBehavior == DeleteBehavior.NoAction), "D8B NoAction " + type.Name);
            check(entity.FindProperty("CreatedAt")!.GetColumnType() == "datetime2", "D8B datetime2 " + type.Name);
        }
        var calendar = model.FindEntityType(typeof(WorkCalendar))!;
        check(calendar.GetIndexes().Any(x => x.IsUnique && x.GetFilter() == "[IsDefault] = 1 AND [IsActive] = 1"), "D8B default suggestion unique");
        var entitlement = model.FindEntityType(typeof(EmployeeLeaveEntitlement))!;
        check(entitlement.GetIndexes().Any(x => x.IsUnique && x.Properties.Select(p => p.Name).SequenceEqual(new[] { "EmployeeId", "LeaveTypeId", "LeaveYear" })), "D8B employee/type/year unique");
        foreach (var field in new[] { "AdjustmentMinutes", "UsedMinutes", "PendingMinutes", "AvailableMinutes", "CarryForwardMinutes" })
            check(entitlement.FindProperty(field) is null, "D8B no mutable total " + field);
        var policy = model.FindEntityType(typeof(LeavePolicy))!;
        check(policy.GetIndexes().Any(x => x.IsUnique && x.Properties.Select(p => p.Name).SequenceEqual(new[] { "LeaveTypeId", "Version" })), "D8B policy revision unique");
        check(new LeavePolicy().Status == "Draft", "D8B draft default");
        check(typeof(LeavePolicyRequest).GetProperty("Status") is null && typeof(LeavePolicyRequest).GetProperty("PublishedAt") is null, "D8B caller cannot forge publication");
        check(typeof(EntitlementAdjustmentRequest).GetProperty("CreatedAt") is null, "D8B adjustment timestamp server controlled");
        check(typeof(EntitlementDto).GetProperty("AvailableMinutes") is null, "D8B does not invent calculated availability");
        var leave = model.FindEntityType(typeof(EmployeeLeave))!;
        check(leave.FindProperty("RequestedStartTime")!.IsNullable && leave.FindProperty("RequestedEndTime")!.GetColumnType() == "time(0)", "D8B minute boundary preparation");
        check(leave.GetCheckConstraints().Any(x => x.Name == "CK_EmployeeLeave_CalculationSnapshot" && x.Sql.Contains("ISJSON")), "D8B versioned calculation snapshot preparation");
        check(Enum.GetNames<LeaveNoticeCategory>().SequenceEqual(new[] { "Foreseeable", "SuddenIllness" }), "D8B notice contract");
        check(typeof(LeavePolicyDto).GetProperty("StorageKey") is null && typeof(CalendarResolutionDto).GetProperty("ScheduledMinutes") is not null, "D8B safe document reference and minute resolution DTOs");
    }
}
