using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIAMIS.Domain.Entities.Payroll;

namespace SIAMIS.Infrastructure.Configurations;

internal sealed class EmployeePayrollComponentAssignmentConfiguration : IEntityTypeConfiguration<EmployeePayrollComponentAssignment>
{
    public void Configure(EntityTypeBuilder<EmployeePayrollComponentAssignment> builder)
    {
        builder.ToTable("EmployeePayrollComponentAssignments", table =>
        {
            table.HasCheckConstraint("CK_EmployeePayrollComponentAssignments_NonNegativeValues",
                "[Amount] >= 0 AND ([Quantity] IS NULL OR [Quantity] >= 0) AND ([Rate] IS NULL OR [Rate] >= 0)");
            table.HasCheckConstraint("CK_EmployeePayrollComponentAssignments_EffectiveDates",
                "[EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveFrom]");
        });
        builder.HasKey(item => item.EmployeePayrollComponentAssignmentId);
        builder.Property(item => item.Amount).HasColumnType("decimal(19,4)").IsRequired();
        builder.Property(item => item.Quantity).HasColumnType("decimal(19,4)");
        builder.Property(item => item.Rate).HasColumnType("decimal(19,4)");
        builder.Property(item => item.EffectiveFrom).HasColumnType("date").IsRequired();
        builder.Property(item => item.EffectiveTo).HasColumnType("date");
        builder.Property(item => item.Remarks).HasMaxLength(1000);
        builder.Property(item => item.CreatedAt).HasColumnType("datetime2").IsRequired();
        builder.Property(item => item.UpdatedAt).HasColumnType("datetime2").IsRequired();

        builder.HasIndex(item => item.EmployeeId).HasDatabaseName("IX_EmployeePayrollComponentAssignments_EmployeeId");
        builder.HasIndex(item => item.PayrollComponentId).HasDatabaseName("IX_EmployeePayrollComponentAssignments_PayrollComponentId");
        builder.HasIndex(item => new { item.EmployeeId, item.EffectiveFrom })
            .HasDatabaseName("IX_EmployeePayrollComponentAssignments_Employee_EffectiveFrom");
        builder.HasIndex(item => new { item.EmployeeId, item.PayrollComponentId, item.EffectiveFrom })
            .HasDatabaseName("IX_EmployeePayrollComponentAssignments_Employee_Component_EffectiveFrom");

        builder.HasOne(item => item.Employee).WithMany()
            .HasForeignKey(item => item.EmployeeId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(item => item.PayrollComponent).WithMany()
            .HasForeignKey(item => item.PayrollComponentId).OnDelete(DeleteBehavior.NoAction);
    }
}
