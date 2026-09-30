using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIAMIS.Domain.Entities.Payroll;

namespace SIAMIS.Infrastructure.Configurations;

internal sealed class PayrollPeriodConfiguration : IEntityTypeConfiguration<PayrollPeriod>
{
    public void Configure(EntityTypeBuilder<PayrollPeriod> builder)
    {
        builder.ToTable("PayrollPeriods");
        builder.HasKey(item => item.PayrollPeriodId);
        builder.Property(item => item.Code).HasMaxLength(50).IsRequired();
        builder.Property(item => item.Name).HasMaxLength(150).IsRequired();
        builder.Property(item => item.StartDate).HasColumnType("date").IsRequired();
        builder.Property(item => item.EndDate).HasColumnType("date").IsRequired();
        builder.Property(item => item.PayDate).HasColumnType("date").IsRequired();
        builder.Property(item => item.Status).HasMaxLength(20).IsRequired();
        builder.Property(item => item.Remarks).HasMaxLength(1000);
        builder.Property(item => item.CreatedAt).HasColumnType("datetime2").IsRequired();
        builder.Property(item => item.UpdatedAt).HasColumnType("datetime2").IsRequired();
        builder.HasIndex(item => item.Code).IsUnique();
        builder.HasIndex(item => item.StartDate);
    }
}
