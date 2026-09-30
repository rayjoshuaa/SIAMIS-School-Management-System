using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIAMIS.Domain.Entities.Payroll;

namespace SIAMIS.Infrastructure.Configurations;

internal sealed class PayrollRuleConfiguration : IEntityTypeConfiguration<PayrollRule>
{
    public void Configure(EntityTypeBuilder<PayrollRule> builder)
    {
        builder.ToTable("PayrollRules", table =>
        {
            table.HasCheckConstraint("CK_PayrollRules_EffectiveDates", "[EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveFrom]");
            table.HasCheckConstraint("CK_PayrollRules_NonNegativeValues",
                "([Rate] IS NULL OR [Rate] >= 0) AND ([FixedAmount] IS NULL OR [FixedAmount] >= 0) AND ([MinimumBase] IS NULL OR [MinimumBase] >= 0) AND ([MaximumBase] IS NULL OR [MaximumBase] >= 0)");
            table.HasCheckConstraint("CK_PayrollRules_BaseRange", "[MinimumBase] IS NULL OR [MaximumBase] IS NULL OR [MaximumBase] >= [MinimumBase]");
            table.HasCheckConstraint("CK_PayrollRules_ApplicationMode", "[ApplicationMode] IN ('Supplement', 'ReplaceAssignment')");
        });
        builder.HasKey(item => item.PayrollRuleId);
        builder.Property(item => item.Code).HasMaxLength(50).IsRequired();
        builder.Property(item => item.Name).HasMaxLength(150).IsRequired();
        builder.Property(item => item.PayrollComponentId).IsRequired();
        builder.Property(item => item.ApplicationMode).HasMaxLength(30).HasDefaultValue("Supplement").IsRequired();
        builder.Property(item => item.Priority).HasDefaultValue(0).IsRequired();
        builder.Property(item => item.Description).HasMaxLength(1000);
        builder.Property(item => item.RuleType).HasMaxLength(30).IsRequired();
        builder.Property(item => item.CalculationMethod).HasMaxLength(30).IsRequired();
        builder.Property(item => item.CalculationStage).HasMaxLength(20).HasDefaultValue("Earning").IsRequired();
        builder.Property(item => item.Rate).HasColumnType("decimal(19,4)");
        builder.Property(item => item.FixedAmount).HasColumnType("decimal(19,4)");
        builder.Property(item => item.MinimumBase).HasColumnType("decimal(19,4)");
        builder.Property(item => item.MaximumBase).HasColumnType("decimal(19,4)");
        builder.Property(item => item.BaseType).HasMaxLength(30);
        builder.Property(item => item.AppliesTo).HasMaxLength(30).IsRequired();
        builder.Property(item => item.EffectiveFrom).HasColumnType("date").IsRequired();
        builder.Property(item => item.EffectiveTo).HasColumnType("date");
        builder.Property(item => item.IsActive).IsRequired();
        builder.Property(item => item.CreatedAt).HasColumnType("datetime2").IsRequired();
        builder.Property(item => item.UpdatedAt).HasColumnType("datetime2").IsRequired();

        builder.HasIndex(item => item.Code).IsUnique().HasDatabaseName("UX_PayrollRules_Code");
        builder.HasIndex(item => item.PayrollComponentId).HasDatabaseName("IX_PayrollRules_PayrollComponentId");
        builder.HasIndex(item => item.RuleType).HasDatabaseName("IX_PayrollRules_RuleType");
        builder.HasIndex(item => item.IsActive).HasDatabaseName("IX_PayrollRules_IsActive");
        builder.HasIndex(item => item.EffectiveFrom).HasDatabaseName("IX_PayrollRules_EffectiveFrom");
        builder.HasOne(item => item.PayrollComponent).WithMany()
            .HasForeignKey(item => item.PayrollComponentId).OnDelete(DeleteBehavior.NoAction);
    }
}
