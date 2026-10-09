using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIAMIS.Domain.Entities.Employees;

namespace SIAMIS.Infrastructure.Configurations;

internal sealed class EmployeeNumberReservationConfiguration : IEntityTypeConfiguration<EmployeeNumberReservation>
{
    public void Configure(EntityTypeBuilder<EmployeeNumberReservation> b)
    {
        b.ToTable("EmployeeNumberReservations", t =>
        {
            t.HasTrigger("TR_EmployeeNumberReservations_Permanent");
            t.UseSqlOutputClause(false);
            t.HasCheckConstraint("CK_EmployeeNumberReservations_State", "([AssignedAtUtc] IS NULL AND [RetiredAtUtc] IS NULL) OR ([AssignedAtUtc] IS NOT NULL AND ([RetiredAtUtc] IS NULL OR [RetiredAtUtc] >= [AssignedAtUtc]))");
        });
        b.HasKey(x => x.EmployeeNumber);
        b.Property(x => x.EmployeeNumber).HasMaxLength(30);
        b.HasIndex(x => x.EmployeeId).IsUnique();
        b.Property(x => x.ReservedAtUtc).HasColumnType("datetime2(7)");
        b.Property(x => x.AssignedAtUtc).HasColumnType("datetime2(7)");
        b.Property(x => x.RetiredAtUtc).HasColumnType("datetime2(7)");
    }
}
