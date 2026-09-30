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
            table.HasCheckConstraint("CK_EmployeePayrolls_TaxableEarningsNonNegative", "[TaxableEarnings] >= 0");
        });
        builder.HasKey(item => item.EmployeePayrollId);
        builder.Property(item => item.BasicSalary).HasColumnType("decimal(19,4)").IsRequired();
        builder.Property(item => item.GrossPay).HasColumnType("decimal(19,4)").IsRequired();
        builder.Property(item => item.TotalDeductions).HasColumnType("decimal(19,4)").IsRequired();
        builder.Property(item => item.NetPay).HasColumnType("decimal(19,4)").IsRequired();
        builder.Property(item => item.TaxableEarnings).HasColumnType("decimal(19,4)").HasDefaultValue(0m).IsRequired();
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
            table.HasCheckConstraint("CK_EmployeePayrollLines_ContributionSideSnapshot",
                "[ContributionSideSnapshot] IS NULL OR [ContributionSideSnapshot] IN ('Employee', 'Employer', 'Both')");
            table.HasCheckConstraint("CK_EmployeePayrollLines_SourceTypeAndId",
                "([SourceType] = 'BasicSalary' AND [SourceId] IS NULL) OR ([SourceType] = 'Manual' AND [SourceId] IS NULL) OR ([SourceType] IN ('Assignment', 'PayrollRule') AND [SourceId] IS NOT NULL)");
        });
        builder.HasKey(item => item.EmployeePayrollLineId);
        builder.Property(item => item.ComponentCode).HasMaxLength(50).IsRequired();
        builder.Property(item => item.ComponentName).HasMaxLength(150).IsRequired();
        builder.Property(item => item.ComponentType).HasMaxLength(20).IsRequired();
        builder.Property(item => item.SourceType).HasMaxLength(20).IsRequired();
        builder.Property(item => item.IsTaxableSnapshot).HasDefaultValue(false).IsRequired();
        builder.Property(item => item.IsStatutorySnapshot).HasDefaultValue(false).IsRequired();
        builder.Property(item => item.ContributionSideSnapshot).HasMaxLength(20);
        builder.Property(item => item.Amount).HasColumnType("decimal(19,4)").IsRequired();
        builder.Property(item => item.Quantity).HasColumnType("decimal(19,4)");
        builder.Property(item => item.Rate).HasColumnType("decimal(19,4)");
        builder.Property(item => item.CalculationMethodSnapshot).HasMaxLength(30);
        builder.Property(item => item.RuleCode).HasMaxLength(50);
        builder.Property(item => item.RuleName).HasMaxLength(150);
        builder.Property(item => item.ApplicationMode).HasMaxLength(30);
        builder.Property(item => item.BaseType).HasMaxLength(30);
        builder.Property(item => item.BaseAmount).HasColumnType("decimal(19,4)");
        builder.Property(item => item.MinimumBase).HasColumnType("decimal(19,4)");
        builder.Property(item => item.MaximumBase).HasColumnType("decimal(19,4)");
        builder.Property(item => item.CalculationRate).HasColumnType("decimal(19,4)");
        builder.Property(item => item.Remarks).HasMaxLength(1000);
        builder.HasIndex(item => item.EmployeePayrollId).HasDatabaseName("IX_EmployeePayrollLines_EmployeePayrollId");
        builder.HasIndex(item => item.PayrollComponentId).HasDatabaseName("IX_EmployeePayrollLines_PayrollComponentId");
        builder.HasOne(item => item.EmployeePayroll).WithMany(payroll => payroll.Lines)
            .HasForeignKey(item => item.EmployeePayrollId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(item => item.PayrollComponent).WithMany(component => component.PayrollLines)
            .HasForeignKey(item => item.PayrollComponentId).OnDelete(DeleteBehavior.NoAction);
    }
}
