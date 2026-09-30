using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIAMIS.Domain.Entities.Payroll;

namespace SIAMIS.Infrastructure.Configurations;

internal sealed class PayrollSettingsConfiguration : IEntityTypeConfiguration<PayrollSettings>
{
    public void Configure(EntityTypeBuilder<PayrollSettings> builder)
    {
        builder.ToTable("PayrollSettings", table =>
        {
            table.HasCheckConstraint("CK_PayrollSettings_CalendarDays",
                "[PayrollCutoffDay] BETWEEN 1 AND 31 AND [DefaultPayDay] BETWEEN 1 AND 31");
            table.HasCheckConstraint("CK_PayrollSettings_WorkingValues",
                "[WorkingDaysPerPeriod] > 0 AND [WorkingDaysPerPeriod] <= 366 AND [WorkingHoursPerDay] > 0 AND [WorkingHoursPerDay] <= 24");
            table.HasCheckConstraint("CK_PayrollSettings_BasicSalaryProrationMethod", "[BasicSalaryProrationMethod] = 'ThirtyDay'");
            table.HasCheckConstraint("CK_PayrollSettings_DecimalPlaces", "[DecimalPlaces] BETWEEN 0 AND 6");
        });
        builder.HasKey(item => item.PayrollSettingsId);
        builder.Property(item => item.Currency).HasMaxLength(10).IsRequired();
        builder.Property(item => item.PayFrequency).HasMaxLength(30).IsRequired();
        builder.Property(item => item.PayrollCutoffDay).IsRequired();
        builder.Property(item => item.DefaultPayDay).IsRequired();
        builder.Property(item => item.WorkingDaysPerPeriod).HasColumnType("decimal(5,2)").IsRequired();
        builder.Property(item => item.WorkingHoursPerDay).HasColumnType("decimal(5,2)").IsRequired();
        builder.Property(item => item.RoundingMode).HasMaxLength(20).IsRequired();
        builder.Property(item => item.BasicSalaryProrationMethod).HasMaxLength(30).HasDefaultValue("ThirtyDay").IsRequired();
        builder.Property(item => item.DecimalPlaces).IsRequired();
        builder.Property(item => item.IsActive).IsRequired();
        builder.Property(item => item.CreatedAt).HasColumnType("datetime2").IsRequired();
        builder.Property(item => item.UpdatedAt).HasColumnType("datetime2").IsRequired();

        builder.HasIndex(item => item.IsActive).IsUnique().HasFilter("[IsActive] = 1")
            .HasDatabaseName("UX_PayrollSettings_OneActive");
    }
}
