using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIAMIS.Domain.Entities.Employees;
using SIAMIS.Domain.Entities.Leave;

namespace SIAMIS.Infrastructure.Configurations;

internal sealed class LeaveRequestConfiguration : IEntityTypeConfiguration<EmployeeLeave>
{
    public void Configure(EntityTypeBuilder<EmployeeLeave> b)
    {
        b.Property(x => x.RequestMode).HasMaxLength(20); b.Property(x => x.NoticeCategory).HasMaxLength(20);
        b.Property(x => x.RequestedAt).HasColumnType("datetime2"); b.Property(x => x.ReviewedAt).HasColumnType("datetime2"); b.Property(x => x.CancelledAt).HasColumnType("datetime2");
        b.Property(x => x.ReviewRemarks).HasMaxLength(2000); b.Property(x => x.CancellationRemarks).HasMaxLength(2000);
        b.HasAlternateKey(x => new { x.EmployeeId, x.LeaveId });
        b.HasIndex(x => new { x.EmployeeId, x.Status, x.StartDate }); b.HasIndex(x => new { x.Status, x.RequestedAt });
        b.ToTable("EmployeeLeave", t =>
        {
            t.HasCheckConstraint("CK_EmployeeLeave_Status", "[Status] IN ('Pending','Approved','Rejected','Cancelled')");
            t.HasCheckConstraint("CK_EmployeeLeave_Authoritative", "[RequestMode] IS NULL OR ([RequestMode] IN ('FullDay','Timed') AND [NoticeCategory] IS NOT NULL AND [NoticeCategory] IN ('Foreseeable','SuddenIllness') AND [BalanceTracked] IS NOT NULL AND [RequestedAt] IS NOT NULL AND [ChargeableMinutes] IS NOT NULL AND [ChargeableMinutes] > 0 AND [CalculationSnapshotVersion] IN (1,2) AND [CalculationSnapshotJson] IS NOT NULL AND (([RequestMode] = 'FullDay' AND [RequestedStartTime] IS NULL AND [RequestedEndTime] IS NULL) OR ([RequestMode] = 'Timed' AND [RequestedStartTime] IS NOT NULL AND [RequestedEndTime] IS NOT NULL)))");
            t.HasCheckConstraint("CK_EmployeeLeave_Lifecycle", "[RequestMode] IS NULL OR (([Status] = 'Pending' AND [ReviewedAt] IS NULL AND [CancelledAt] IS NULL) OR ([Status] IN ('Approved','Rejected') AND [ReviewedAt] IS NOT NULL AND [CancelledAt] IS NULL) OR ([Status] = 'Cancelled' AND [CancelledAt] IS NOT NULL AND ([ReviewedAt] IS NULL OR ([CancellationRemarks] IS NOT NULL AND LEN(LTRIM(RTRIM([CancellationRemarks]))) > 0))))");
        });
    }
}
internal sealed class LeaveAllocationConfiguration : IEntityTypeConfiguration<EmployeeLeaveAllocation>
{
    public void Configure(EntityTypeBuilder<EmployeeLeaveAllocation> b)
    {
        b.ToTable("EmployeeLeaveAllocations", t => { t.HasCheckConstraint("CK_LeaveAllocation_Year", "[LeaveYear] BETWEEN 1 AND 9999"); t.HasCheckConstraint("CK_LeaveAllocation_Minutes", "[ChargeableMinutes] > 0");
            t.HasCheckConstraint("CK_LeaveAllocation_Payment", "([PaidMinutes] IS NULL AND [UnpaidMinutes] IS NULL) OR ([PaidMinutes] IS NOT NULL AND [UnpaidMinutes] IS NOT NULL AND [PaidMinutes] >= 0 AND [UnpaidMinutes] >= 0 AND CONVERT(bigint,[PaidMinutes]) + [UnpaidMinutes] = [ChargeableMinutes])"); });
        b.HasKey(x => x.Id); b.HasIndex(x => new { x.EmployeeLeaveId, x.LeaveYear }).IsUnique();
        b.HasOne<EmployeeLeave>().WithMany().HasForeignKey(x => x.EmployeeLeaveId).OnDelete(DeleteBehavior.NoAction);
    }
}
