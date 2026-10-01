using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIAMIS.Domain.Entities.Payroll;

namespace SIAMIS.Infrastructure.Configurations;

internal sealed class EmployeePayrollStatutoryResultConfiguration : IEntityTypeConfiguration<EmployeePayrollStatutoryResult>
{
    public void Configure(EntityTypeBuilder<EmployeePayrollStatutoryResult> b)
    {
        b.ToTable("EmployeePayrollStatutoryResults", t => {
            t.HasCheckConstraint("CK_PayrollStatutoryResult_Json", "ISJSON([CalculationSnapshotJson]) = 1");
            t.HasCheckConstraint("CK_PayrollStatutoryResult_V1", "[CalculationMethodVersion] = 'SSO-TH-V1' AND [Currency] = 'THB'");
            t.HasCheckConstraint("CK_PayrollStatutoryResult_Month", "[ContributionMonth] = CONVERT(char(7), [GoverningDate], 126)");
        });
        b.HasKey(x => x.EmployeePayrollStatutoryResultId);
        b.Property(x => x.CalculationMethodVersion).HasMaxLength(50).IsRequired();
        b.Property(x => x.ContributionMonth).HasMaxLength(7).IsRequired();
        b.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        b.Property(x => x.GoverningDate).HasColumnType("date");
        b.Property(x => x.CalculationSnapshotJson).HasColumnType("nvarchar(max)").IsRequired();
        b.Property(x => x.CreatedAt).HasColumnType("datetime2");
        b.HasIndex(x => new { x.EmployeePayrollId, x.StatutorySchemeId }).IsUnique();
        b.HasOne<EmployeePayroll>().WithMany().HasForeignKey(x => x.EmployeePayrollId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<StatutoryScheme>().WithMany().HasForeignKey(x => x.StatutorySchemeId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<StatutoryPolicyVersion>().WithMany().HasForeignKey(x => x.StatutoryPolicyVersionId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<EmployeeStatutoryEnrollment>().WithMany().HasForeignKey(x => x.EmployeeStatutoryEnrollmentId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.SocialSecurity).WithOne().HasForeignKey<EmployeePayrollSocialSecurityResult>(x => x.EmployeePayrollStatutoryResultId).OnDelete(DeleteBehavior.NoAction);
    }
}

internal sealed class EmployeePayrollSocialSecurityResultConfiguration : IEntityTypeConfiguration<EmployeePayrollSocialSecurityResult>
{
    public void Configure(EntityTypeBuilder<EmployeePayrollSocialSecurityResult> b)
    {
        b.ToTable("EmployeePayrollSocialSecurityResults", t => {
            t.HasCheckConstraint("CK_PayrollSsoResult_NonNegative", "[ContributionWage] >= 0 AND [ContributionBase] >= 0 AND [MinimumBase] >= 0 AND [MaximumBase] >= [MinimumBase] AND [EmployeeRate] >= 0 AND [EmployerRate] >= 0 AND [RawEmployeeAmount] >= 0 AND [EmployeeAmount] >= 0 AND [EmployerAmount] >= 0");
            t.HasCheckConstraint("CK_PayrollSsoResult_V1Amounts", "[EmployeeRate] = [EmployerRate] AND [EmployerAmount] = [EmployeeAmount] AND [EmployeeAmount] = FLOOR([EmployeeAmount])");
            t.HasCheckConstraint("CK_PayrollSsoResult_Base", "([ContributionWage] = 0 AND [ContributionBase] = 0 AND [RawEmployeeAmount] = 0 AND [EmployeeAmount] = 0) OR ([ContributionWage] > 0 AND [ContributionBase] >= [MinimumBase] AND [ContributionBase] <= [MaximumBase])");
        });
        b.HasKey(x => x.EmployeePayrollStatutoryResultId);
        foreach (var name in new[] { nameof(EmployeePayrollSocialSecurityResult.ContributionWage), nameof(EmployeePayrollSocialSecurityResult.ContributionBase),
            nameof(EmployeePayrollSocialSecurityResult.MinimumBase), nameof(EmployeePayrollSocialSecurityResult.MaximumBase),
            nameof(EmployeePayrollSocialSecurityResult.EmployeeRate), nameof(EmployeePayrollSocialSecurityResult.EmployerRate),
            nameof(EmployeePayrollSocialSecurityResult.EmployeeAmount), nameof(EmployeePayrollSocialSecurityResult.EmployerAmount) })
            b.Property<decimal>(name).HasColumnType("decimal(19,4)");
        b.Property(x => x.RawEmployeeAmount).HasColumnType("decimal(38,10)");
    }
}
