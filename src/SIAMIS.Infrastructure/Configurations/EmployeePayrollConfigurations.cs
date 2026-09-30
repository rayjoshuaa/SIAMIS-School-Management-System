using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIAMIS.Domain.Entities.Payroll;

namespace SIAMIS.Infrastructure.Configurations;

internal sealed class EmployeePayrollConfiguration : IEntityTypeConfiguration<EmployeePayroll>
{
    public void Configure(EntityTypeBuilder<EmployeePayroll> builder)
    {
        builder.ToTable("EmployeePayrolls", table =>
        {
            table.HasCheckConstraint("CK_EmployeePayrolls_NonNegativeTotals",
                "[BasicSalary] >= 0 AND [GrossPay] >= 0 AND [TotalDeductions] >= 0 AND [NetPay] >= 0");
            table.HasCheckConstraint("CK_EmployeePayrolls_Status",
                "[Status] IN ('Draft', 'Calculated', 'Approved', 'Paid', 'Cancelled')");
        });
        builder.HasKey(item => item.EmployeePayrollId);
        builder.Property(item => item.BasicSalary).HasColumnType("decimal(19,4)").IsRequired();
        builder.Property(item => item.GrossPay).HasColumnType("decimal(19,4)").IsRequired();
        builder.Property(item => item.TotalDeductions).HasColumnType("decimal(19,4)").IsRequired();
        builder.Property(item => item.NetPay).HasColumnType("decimal(19,4)").IsRequired();
        builder.Property(item => item.Status).HasMaxLength(30).IsRequired();
        builder.Property(item => item.Remarks).HasMaxLength(2000);
        builder.Property(item => item.CreatedAt).HasColumnType("datetime2").IsRequired();
        builder.Property(item => item.UpdatedAt).HasColumnType("datetime2").IsRequired();
        builder.HasIndex(item => new { item.PayrollPeriodId, item.EmployeeId })
            .IsUnique().HasDatabaseName("UX_EmployeePayroll_PeriodEmployee");
        builder.HasIndex(item => item.EmployeeId).HasDatabaseName("IX_EmployeePayrolls_EmployeeId");
        builder.HasIndex(item => item.PayrollPeriodId).HasDatabaseName("IX_EmployeePayrolls_PayrollPeriodId");
        builder.HasIndex(item => item.Status).HasDatabaseName("IX_EmployeePayrolls_Status");
        builder.HasOne(item => item.Employee).WithMany(employee => employee.PayrollRecords)
            .HasForeignKey(item => item.EmployeeId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(item => item.PayrollPeriod).WithMany(period => period.EmployeePayrolls)
            .HasForeignKey(item => item.PayrollPeriodId).OnDelete(DeleteBehavior.NoAction);
    }
}

internal sealed class EmployeePayrollLineConfiguration : IEntityTypeConfiguration<EmployeePayrollLine>
{
    public void Configure(EntityTypeBuilder<EmployeePayrollLine> builder)
    {
        builder.ToTable("EmployeePayrollLines", table =>
        {
            table.HasCheckConstraint("CK_EmployeePayrollLines_AmountPositive", "[Amount] > 0");
            table.HasCheckConstraint("CK_EmployeePayrollLines_QuantityNonNegative", "[Quantity] IS NULL OR [Quantity] >= 0");
            table.HasCheckConstraint("CK_EmployeePayrollLines_RateNonNegative", "[Rate] IS NULL OR [Rate] >= 0");
            table.HasCheckConstraint("CK_EmployeePayrollLines_ComponentType", "[ComponentType] IN ('Earning', 'Deduction')");
        });
        builder.HasKey(item => item.EmployeePayrollLineId);
        builder.Property(item => item.ComponentCode).HasMaxLength(50).IsRequired();
        builder.Property(item => item.ComponentName).HasMaxLength(150).IsRequired();
        builder.Property(item => item.ComponentType).HasMaxLength(20).IsRequired();
        builder.Property(item => item.Amount).HasColumnType("decimal(19,4)").IsRequired();
        builder.Property(item => item.Quantity).HasColumnType("decimal(19,4)");
        builder.Property(item => item.Rate).HasColumnType("decimal(19,4)");
        builder.Property(item => item.Remarks).HasMaxLength(1000);
        builder.HasIndex(item => item.EmployeePayrollId).HasDatabaseName("IX_EmployeePayrollLines_EmployeePayrollId");
        builder.HasIndex(item => item.PayrollComponentId).HasDatabaseName("IX_EmployeePayrollLines_PayrollComponentId");
        builder.HasOne(item => item.EmployeePayroll).WithMany(payroll => payroll.Lines)
            .HasForeignKey(item => item.EmployeePayrollId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(item => item.PayrollComponent).WithMany(component => component.PayrollLines)
            .HasForeignKey(item => item.PayrollComponentId).OnDelete(DeleteBehavior.NoAction);
    }
}
