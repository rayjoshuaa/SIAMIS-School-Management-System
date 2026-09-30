using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIAMIS.Domain.Entities.MasterData;

namespace SIAMIS.Infrastructure.Configurations;

internal sealed class DocumentTypeConfiguration : IEntityTypeConfiguration<DocumentType>
{
    public void Configure(EntityTypeBuilder<DocumentType> builder)
    {
        builder.Property(item => item.RequiresExpiryDate).IsRequired();
        builder.Property(item => item.RequiresDocumentNumber).IsRequired();
        builder.Property(item => item.RequiresVerification).IsRequired();
    }
}

internal sealed class LeaveTypeConfiguration : IEntityTypeConfiguration<LeaveType>
{
    public void Configure(EntityTypeBuilder<LeaveType> builder)
    {
        builder.Property(item => item.IsPaid).IsRequired();
    }
}

internal sealed class PayrollComponentConfiguration : IEntityTypeConfiguration<PayrollComponent>
{
    public void Configure(EntityTypeBuilder<PayrollComponent> builder)
    {
        builder.ToTable("PayrollComponents", table =>
            table.HasCheckConstraint("CK_PayrollComponents_ContributionSide",
                "[ContributionSide] IS NULL OR [ContributionSide] IN ('Employee', 'Employer', 'Both')"));
        builder.Property(item => item.Category).HasMaxLength(30).IsRequired();
        builder.Property(item => item.CalculationMethod).HasMaxLength(30).HasDefaultValue("FixedAmount").IsRequired();
        builder.Property(item => item.PercentageBase).HasMaxLength(30).IsRequired(false);
        builder.Property(item => item.IsTaxable).HasDefaultValue(false).IsRequired();
        builder.Property(item => item.IsStatutory).HasDefaultValue(false).IsRequired();
        builder.Property(item => item.ContributionSide).HasMaxLength(20).IsRequired(false);
    }
}
