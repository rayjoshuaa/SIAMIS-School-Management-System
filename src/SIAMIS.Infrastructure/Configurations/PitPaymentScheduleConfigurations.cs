using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIAMIS.Domain.Entities.Employees;
using SIAMIS.Domain.Entities.Payroll;

namespace SIAMIS.Infrastructure.Configurations;

internal sealed class EmployeePitPaymentScheduleConfiguration : IEntityTypeConfiguration<EmployeePitPaymentSchedule>
{
    public void Configure(EntityTypeBuilder<EmployeePitPaymentSchedule> b)
    {
        b.ToTable("EmployeePitPaymentSchedules", t => {
            t.HasCheckConstraint("CK_PitSchedule_Status", "([Status]='Draft' AND [VerifiedAt] IS NULL) OR ([Status]='Verified' AND [VerifiedAt] IS NOT NULL)");
            t.HasCheckConstraint("CK_PitSchedule_YearRevision", "[TaxYear] BETWEEN 1 AND 9999 AND [RevisionNumber]>0");
            t.HasCheckConstraint("CK_PitSchedule_Evidence", "LEN(LTRIM(RTRIM([Evidence])))>0");
        });
        b.HasKey(x => x.EmployeePitPaymentScheduleId);
        b.HasAlternateKey(x => new { x.EmployeeId, x.TaxYear, x.EmployeePitPaymentScheduleId });
        b.HasIndex(x => new { x.EmployeeId, x.TaxYear, x.RevisionNumber }).IsUnique();
        b.HasIndex(x => new { x.EmployeeId, x.TaxYear }).IsUnique().HasFilter("[Status]='Draft'");
        b.Property(x => x.Status).HasMaxLength(20).IsRequired();
        b.Property(x => x.Evidence).HasMaxLength(2000).IsRequired();
        b.Property(x => x.VerifiedAt).HasColumnType("datetime2");
        EmployeeStatutoryMapping.Timestamps(b);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<EmployeePitPaymentSchedule>().WithMany()
            .HasForeignKey(x => new { x.EmployeeId, x.TaxYear, x.ReplacesScheduleId })
            .HasPrincipalKey(x => new { x.EmployeeId, x.TaxYear, x.EmployeePitPaymentScheduleId }).OnDelete(DeleteBehavior.NoAction);
        b.HasMany(x => x.Entries).WithOne().HasForeignKey(x => x.EmployeePitPaymentScheduleId).OnDelete(DeleteBehavior.NoAction);
    }
}
internal sealed class EmployeePitPaymentScheduleEntryConfiguration : IEntityTypeConfiguration<EmployeePitPaymentScheduleEntry>
{
    public void Configure(EntityTypeBuilder<EmployeePitPaymentScheduleEntry> b)
    {
        b.ToTable("EmployeePitPaymentScheduleEntries", t => t.HasCheckConstraint("CK_PitScheduleEntry_Ordinal", "[PaymentOrdinal] BETWEEN 1 AND 12"));
        b.HasKey(x => x.EmployeePitPaymentScheduleEntryId);
        b.Property(x => x.PlannedPayDate).HasColumnType("date");
        b.HasIndex(x => new { x.EmployeePitPaymentScheduleId, x.PlannedPayDate }).IsUnique();
        b.HasIndex(x => new { x.EmployeePitPaymentScheduleId, x.PaymentOrdinal }).IsUnique();
    }
}
internal sealed class EmployeePitPaymentScheduleSelectionConfiguration : IEntityTypeConfiguration<EmployeePitPaymentScheduleSelection>
{
    public void Configure(EntityTypeBuilder<EmployeePitPaymentScheduleSelection> b)
    {
        b.ToTable("EmployeePitPaymentScheduleSelections");
        b.HasKey(x => new { x.EmployeeId, x.TaxYear });
        b.HasOne<EmployeePitPaymentSchedule>().WithMany()
            .HasForeignKey(x => new { x.EmployeeId, x.TaxYear, x.CurrentScheduleId })
            .HasPrincipalKey(x => new { x.EmployeeId, x.TaxYear, x.EmployeePitPaymentScheduleId }).OnDelete(DeleteBehavior.NoAction);
    }
}
