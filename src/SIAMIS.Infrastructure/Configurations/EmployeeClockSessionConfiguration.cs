using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIAMIS.Domain.Entities.Employees;

namespace SIAMIS.Infrastructure.Configurations;

internal sealed class EmployeeClockSessionConfiguration : IEntityTypeConfiguration<EmployeeClockSession>
{
    public void Configure(EntityTypeBuilder<EmployeeClockSession> b)
    {
        b.ToTable("EmployeeClockSessions", t =>
        {
            t.HasCheckConstraint("CK_EmployeeClockSession_Arrangement", "[WorkArrangement] IN ('OnCampus','OnlineClass','RemoteWork')");
            t.HasCheckConstraint("CK_EmployeeClockSession_Events", "[OutEventId] IS NULL OR [OutEventId] <> [InEventId]");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.WorkArrangement).HasMaxLength(20).IsRequired().UseCollation("Latin1_General_100_BIN2");
        b.HasIndex(x => x.EmployeeId).IsUnique().HasFilter("[OutEventId] IS NULL");
        b.HasIndex(x => x.InEventId).IsUnique();
        b.HasIndex(x => x.OutEventId).IsUnique().HasFilter("[OutEventId] IS NOT NULL");
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<AttendanceEvent>().WithMany().HasForeignKey(x => new { x.EmployeeId, x.InEventId })
            .HasPrincipalKey(x => new { x.EmployeeId, x.AttendanceEventId }).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<AttendanceEvent>().WithMany().HasForeignKey(x => new { x.EmployeeId, x.OutEventId })
            .HasPrincipalKey(x => new { x.EmployeeId, x.AttendanceEventId }).OnDelete(DeleteBehavior.NoAction);
    }
}
