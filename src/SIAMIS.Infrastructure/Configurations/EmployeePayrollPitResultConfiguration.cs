using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIAMIS.Domain.Entities.Employees;
using SIAMIS.Domain.Entities.Payroll;

namespace SIAMIS.Infrastructure.Configurations;

public sealed class EmployeePayrollPitResultConfiguration : IEntityTypeConfiguration<EmployeePayrollPitResult>
{
    public void Configure(EntityTypeBuilder<EmployeePayrollPitResult> b)
    {
        b.ToTable("EmployeePayrollPitResults", t =>
        {
            t.HasCheckConstraint("CK_PitResult_YearSchedule", "[TaxYear] = DATEPART(year, [GoverningDate]) AND [ApplicablePaymentCount] BETWEEN 1 AND 12 AND [PaymentOrdinal] BETWEEN 1 AND [ApplicablePaymentCount] AND [ScheduleRevisionNumber] > 0");
            t.HasCheckConstraint("CK_PitResult_Amount", "[CurrentWithholding] >= 0 AND [CurrentWithholding] = ROUND([CurrentWithholding], 2) AND [NetTaxableIncome] >= 0 AND [RecognizedEmployeeSso] >= 0");
            t.HasCheckConstraint("CK_PitResult_Final", "([IsFinalScheduledPayment] = 1 AND [PaymentOrdinal] = [ApplicablePaymentCount] AND [FinalAllocationResidual] IS NOT NULL) OR ([IsFinalScheduledPayment] = 0 AND [PaymentOrdinal] < [ApplicablePaymentCount] AND [FinalAllocationResidual] IS NULL)");
            t.HasCheckConstraint("CK_PitResult_Snapshot", "ISJSON([CalculationSnapshotJson]) = 1");
        });
        b.HasKey(x => x.EmployeePayrollPitResultId);
        b.HasIndex(x => x.EmployeePayrollId).IsUnique();
        b.HasIndex(x => new { x.EmployeeId, x.TaxYear, x.GoverningDate });
        b.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        b.Property(x => x.CalculationMethodVersion).HasMaxLength(50).IsRequired();
        b.Property(x => x.CalculationSnapshotJson).HasColumnType("nvarchar(max)").IsRequired();
        b.Property(x => x.CreatedAt).HasColumnType("datetime2");
        b.Property(x => x.GoverningDate).HasColumnType("date");
        foreach (var p in typeof(EmployeePayrollPitResult).GetProperties().Where(p => p.PropertyType == typeof(decimal) || p.PropertyType == typeof(decimal?)))
            b.Property(p.Name).HasColumnType("decimal(38,18)");
        b.HasOne<EmployeePayroll>().WithOne().HasForeignKey<EmployeePayrollPitResult>(x => x.EmployeePayrollId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<PayrollPeriod>().WithMany().HasForeignKey(x => x.PayrollPeriodId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<StatutoryScheme>().WithMany().HasForeignKey(x => x.StatutorySchemeId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<StatutoryPolicyVersion>().WithMany().HasForeignKey(x => x.StatutoryPolicyVersionId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<EmployeeStatutoryEnrollment>().WithMany().HasForeignKey(x => x.EmployeeStatutoryEnrollmentId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<EmployeeTaxDeclaration>().WithMany().HasForeignKey(x => x.EmployeeTaxDeclarationId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<EmployeePitPaymentSchedule>().WithMany().HasForeignKey(x => x.EmployeePitPaymentScheduleId).OnDelete(DeleteBehavior.NoAction);
    }
}
