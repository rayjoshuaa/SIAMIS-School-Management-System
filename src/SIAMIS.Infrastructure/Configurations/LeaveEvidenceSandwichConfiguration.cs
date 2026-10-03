using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIAMIS.Domain.Entities.Leave;
using SIAMIS.Domain.Entities.Employees;
using SIAMIS.Domain.Entities.MasterData;

namespace SIAMIS.Infrastructure.Configurations;

internal sealed class LeaveEvidenceConfiguration : IEntityTypeConfiguration<EmployeeLeaveEvidence>
{
    public void Configure(EntityTypeBuilder<EmployeeLeaveEvidence> b)
    {
        b.ToTable("EmployeeLeaveEvidence", t => {
            t.HasCheckConstraint("CK_LeaveEvidence_Kind", "[EvidenceKind] = 'ExternalReceipt'");
            t.HasCheckConstraint("CK_LeaveEvidence_Reference", "LEN(LTRIM(RTRIM([ExternalReference]))) > 0");
            t.HasCheckConstraint("CK_LeaveEvidence_Successor", "[SupersedesEvidenceId] IS NULL OR [SupersedesEvidenceId] <> [Id]");
        });
        b.HasKey(x => x.Id); b.HasAlternateKey(x => new { x.EmployeeId, x.LeaveId, x.Id });
        b.HasIndex(x => x.SupersedesEvidenceId).IsUnique().HasFilter("[SupersedesEvidenceId] IS NOT NULL");
        b.Property(x => x.EvidenceKind).HasMaxLength(30); b.Property(x => x.ExternalReference).HasMaxLength(200);
        b.Property(x => x.RecordedAt).HasColumnType("datetime2");
        b.HasOne<EmployeeLeave>().WithMany().HasForeignKey(x => new { x.EmployeeId, x.LeaveId }).HasPrincipalKey(x => new { x.EmployeeId, x.LeaveId }).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<DocumentType>().WithMany().HasForeignKey(x => x.DocumentTypeId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<EmployeeLeaveEvidence>().WithMany().HasForeignKey(x => new { x.EmployeeId, x.LeaveId, x.SupersedesEvidenceId }).HasPrincipalKey(x => new { x.EmployeeId, x.LeaveId, x.Id }).OnDelete(DeleteBehavior.NoAction);
    }
}
internal sealed class LeaveEvidenceEventConfiguration : IEntityTypeConfiguration<EmployeeLeaveEvidenceEvent>
{
    public void Configure(EntityTypeBuilder<EmployeeLeaveEvidenceEvent> b)
    {
        b.ToTable("EmployeeLeaveEvidenceEvents", t => t.HasCheckConstraint("CK_LeaveEvidenceEvent_Action", "[Action] IN ('Recorded','Accepted','Rejected','Superseded')"));
        b.HasKey(x => x.Id); b.Property(x => x.Action).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Remarks).HasMaxLength(2000); b.Property(x => x.OccurredAt).HasColumnType("datetime2");
        b.HasOne<EmployeeLeaveEvidence>().WithMany().HasForeignKey(x => x.EvidenceId).OnDelete(DeleteBehavior.NoAction);
    }
}
internal sealed class LeaveApprovalEvidenceConfiguration : IEntityTypeConfiguration<EmployeeLeaveApprovalEvidence>
{
    public void Configure(EntityTypeBuilder<EmployeeLeaveApprovalEvidence> b)
    {
        b.ToTable("EmployeeLeaveApprovalEvidence"); b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.LeaveId, x.EvidenceId }).IsUnique(); b.Property(x => x.AttachedAt).HasColumnType("datetime2");
        b.HasOne<EmployeeLeaveEvidence>().WithMany().HasForeignKey(x => new { x.EmployeeId, x.LeaveId, x.EvidenceId }).HasPrincipalKey(x => new { x.EmployeeId, x.LeaveId, x.Id }).OnDelete(DeleteBehavior.NoAction);
    }
}
internal sealed class LeaveSandwichConfiguration : IEntityTypeConfiguration<EmployeeLeaveSandwichCase>
{
    public void Configure(EntityTypeBuilder<EmployeeLeaveSandwichCase> b)
    {
        b.ToTable("EmployeeLeaveSandwichCases", t => {
            t.HasCheckConstraint("CK_LeaveSandwich_State", "[State] IN ('Reserved','Charged','Exempted','Released','ReviewPending','ReasonAccepted','ReasonNotAccepted')");
            t.HasCheckConstraint("CK_LeaveSandwich_Span", "[GapStart] <= [GapEnd] AND [Revision] > 0");
            t.HasCheckConstraint("CK_LeaveSandwich_Active", "([State] = 'Released' AND [IsActive] = 0) OR ([State] <> 'Released' AND [IsActive] = 1)");
        });
        b.HasKey(x => x.Id); b.HasAlternateKey(x => new { x.EmployeeId, x.Id });
        b.HasIndex(x => new { x.EmployeeId, x.GapStart, x.GapEnd, x.Revision }).IsUnique();
        b.HasIndex(x => new { x.EmployeeId, x.GapStart, x.GapEnd }).IsUnique().HasFilter("[IsActive] = 1");
        b.Property(x => x.State).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.GapStart).HasColumnType("date"); b.Property(x => x.GapEnd).HasColumnType("date");
        b.Property(x => x.DetectedAt).HasColumnType("datetime2"); b.Property(x => x.CalculationSnapshotJson).IsRequired();
        b.HasOne<EmployeeLeave>().WithMany().HasForeignKey(x => new { x.EmployeeId, x.BeforeLeaveId }).HasPrincipalKey(x => new { x.EmployeeId, x.LeaveId }).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<EmployeeLeave>().WithMany().HasForeignKey(x => new { x.EmployeeId, x.AfterLeaveId }).HasPrincipalKey(x => new { x.EmployeeId, x.LeaveId }).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<LeaveType>().WithMany().HasForeignKey(x => x.LeaveTypeId).OnDelete(DeleteBehavior.NoAction);
    }
}
internal sealed class LeaveSandwichEventConfiguration : IEntityTypeConfiguration<EmployeeLeaveSandwichEvent>
{
    public void Configure(EntityTypeBuilder<EmployeeLeaveSandwichEvent> b)
    {
        b.ToTable("EmployeeLeaveSandwichEvents", t => t.HasCheckConstraint("CK_LeaveSandwichEvent_State", "[State] IN ('Reserved','Charged','Exempted','Released','ReviewPending','ReasonAccepted','ReasonNotAccepted')"));
        b.HasKey(x => x.Id); b.Property(x => x.State).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Reason).HasMaxLength(2000); b.Property(x => x.OccurredAt).HasColumnType("datetime2");
        b.HasOne<EmployeeLeaveSandwichCase>().WithMany().HasForeignKey(x => x.CaseId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<EmployeeLeave>().WithMany().HasForeignKey(x => x.CausingLeaveId).OnDelete(DeleteBehavior.NoAction);
    }
}
internal sealed class LeaveSandwichDateConfiguration : IEntityTypeConfiguration<EmployeeLeaveSandwichDate>
{
    public void Configure(EntityTypeBuilder<EmployeeLeaveSandwichDate> b)
    {
        b.ToTable("EmployeeLeaveSandwichDates", t => t.HasCheckConstraint("CK_LeaveSandwichDate_Debit", "[ScheduledMinutes] = 0 AND [SandwichDebitMinutes] > 0"));
        b.HasKey(x => x.Id); b.HasIndex(x => new { x.CaseId, x.Date }).IsUnique(); b.Property(x => x.Date).HasColumnType("date");
        b.HasOne<EmployeeLeaveSandwichCase>().WithMany().HasForeignKey(x => x.CaseId).OnDelete(DeleteBehavior.NoAction);
    }
}
internal sealed class LeaveSandwichAllocationConfiguration : IEntityTypeConfiguration<EmployeeLeaveSandwichAllocation>
{
    public void Configure(EntityTypeBuilder<EmployeeLeaveSandwichAllocation> b)
    {
        b.ToTable("EmployeeLeaveSandwichAllocations", t => {
            t.HasCheckConstraint("CK_LeaveSandwichAllocation_Debit", "[LeaveYear] BETWEEN 1 AND 9999 AND [SandwichDebitMinutes] > 0");
            t.HasCheckConstraint("CK_LeaveSandwichAllocation_Applied", "[AppliedDebitMinutes] IS NULL OR ([AppliedDebitMinutes] >= 0 AND [AppliedDebitMinutes] <= [SandwichDebitMinutes])");
        });
        b.HasKey(x => x.Id); b.HasIndex(x => new { x.CaseId, x.LeaveYear }).IsUnique();
        b.HasOne<EmployeeLeaveSandwichCase>().WithMany().HasForeignKey(x => x.CaseId).OnDelete(DeleteBehavior.NoAction);
    }
}
