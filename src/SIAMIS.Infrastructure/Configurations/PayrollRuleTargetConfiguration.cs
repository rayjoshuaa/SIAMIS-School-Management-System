using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIAMIS.Domain.Entities.Payroll;

namespace SIAMIS.Infrastructure.Configurations;

internal sealed class PayrollRuleTargetConfiguration : IEntityTypeConfiguration<PayrollRuleTarget>
{
    public void Configure(EntityTypeBuilder<PayrollRuleTarget> builder)
    {
        builder.ToTable("PayrollRuleTargets", table => table.HasCheckConstraint("CK_PayrollRuleTargets_TargetType",
            "[TargetType] IN ('Employee', 'Department', 'Designation', 'EmploymentType', 'Location')"));
        builder.HasKey(item => item.PayrollRuleTargetId);
        builder.Property(item => item.TargetType).HasMaxLength(30).IsRequired();
        builder.Property(item => item.TargetId).IsRequired();
        builder.Property(item => item.IsExcluded).IsRequired();
        builder.Property(item => item.CreatedAt).HasColumnType("datetime2").IsRequired();

        builder.HasIndex(item => item.PayrollRuleId).HasDatabaseName("IX_PayrollRuleTargets_PayrollRuleId");
        builder.HasIndex(item => new { item.TargetType, item.TargetId })
            .HasDatabaseName("IX_PayrollRuleTargets_TargetType_TargetId");
        builder.HasIndex(item => new { item.PayrollRuleId, item.TargetType, item.TargetId })
            .IsUnique().HasDatabaseName("UX_PayrollRuleTargets_Rule_Type_Target");

        builder.HasOne(item => item.PayrollRule).WithMany(item => item.Targets)
            .HasForeignKey(item => item.PayrollRuleId).OnDelete(DeleteBehavior.Cascade);
    }
}
