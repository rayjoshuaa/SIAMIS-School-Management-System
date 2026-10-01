using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIAMIS.Domain.Entities.Payroll;

namespace SIAMIS.Infrastructure.Configurations;

internal sealed class StatutorySchemeConfiguration : IEntityTypeConfiguration<StatutoryScheme>
{
    public void Configure(EntityTypeBuilder<StatutoryScheme> b)
    {
        b.ToTable("StatutorySchemes", t => {
            t.HasCheckConstraint("CK_StatutorySchemes_Type", "[SchemeType] IN ('SocialSecurity','PersonalIncomeTax')");
            t.HasCheckConstraint("CK_StatutorySchemes_Jurisdiction", "[Jurisdiction] = 'TH'");
        });
        b.HasKey(x => x.StatutorySchemeId);
        b.HasAlternateKey(x => new { x.StatutorySchemeId, x.SchemeType });
        b.Property(x => x.Code).HasMaxLength(50).IsRequired();
        b.Property(x => x.Name).HasMaxLength(150).IsRequired();
        b.Property(x => x.Jurisdiction).HasMaxLength(2).IsRequired();
        b.Property(x => x.SchemeType).HasMaxLength(30).IsRequired();
        b.Property(x => x.CreatedAt).HasColumnType("datetime2");
        b.Property(x => x.UpdatedAt).HasColumnType("datetime2");
        b.HasIndex(x => x.Code).IsUnique().HasDatabaseName("UX_StatutorySchemes_Code");
    }
}

internal sealed class StatutoryPolicyVersionConfiguration : IEntityTypeConfiguration<StatutoryPolicyVersion>
{
    public void Configure(EntityTypeBuilder<StatutoryPolicyVersion> b)
    {
        b.ToTable("StatutoryPolicyVersions", t => {
            t.HasCheckConstraint("CK_StatutoryPolicyVersions_Status", "[Status] IN ('Draft','Published')");
            t.HasCheckConstraint("CK_StatutoryPolicyVersions_Dates", "[EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveFrom]");
            t.HasCheckConstraint("CK_StatutoryPolicyVersions_Currency", "[Currency] = 'THB'");
            t.HasCheckConstraint("CK_StatutoryPolicyVersions_Publication", "([Status] = 'Draft' AND [PublishedAt] IS NULL) OR ([Status] = 'Published' AND [PublishedAt] IS NOT NULL AND LEN(LTRIM(RTRIM([OfficialReference]))) > 0 AND [OfficialReference] IS NOT NULL AND LEN(LTRIM(RTRIM([CalculationMethodVersion]))) > 0 AND [CalculationMethodVersion] IS NOT NULL)");
        });
        b.HasKey(x => x.StatutoryPolicyVersionId);
        b.HasAlternateKey(x => new { x.StatutoryPolicyVersionId, x.SchemeType });
        b.Property(x => x.SchemeType).HasMaxLength(30).IsRequired();
        b.Property(x => x.Version).HasMaxLength(50).IsRequired();
        b.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        b.Property(x => x.Status).HasMaxLength(20).IsRequired();
        b.Property(x => x.OfficialReference).HasMaxLength(2000);
        b.Property(x => x.CalculationMethodVersion).HasMaxLength(50);
        b.Property(x => x.EffectiveFrom).HasColumnType("date");
        b.Property(x => x.EffectiveTo).HasColumnType("date");
        b.Property(x => x.CreatedAt).HasColumnType("datetime2");
        b.Property(x => x.UpdatedAt).HasColumnType("datetime2");
        b.Property(x => x.PublishedAt).HasColumnType("datetime2");
        b.HasIndex(x => new { x.StatutorySchemeId, x.Version }).IsUnique().HasDatabaseName("UX_StatutoryPolicyVersions_SchemeVersion");
        b.HasIndex(x => new { x.StatutorySchemeId, x.Status, x.EffectiveFrom, x.EffectiveTo }).HasDatabaseName("IX_StatutoryPolicyVersions_Resolution");
        b.HasOne(x => x.StatutoryScheme).WithMany().HasForeignKey(x => new { x.StatutorySchemeId, x.SchemeType })
            .HasPrincipalKey(x => new { x.StatutorySchemeId, x.SchemeType }).OnDelete(DeleteBehavior.NoAction);
    }
}

internal sealed class SocialSecurityPolicyConfigurationMapping : IEntityTypeConfiguration<SocialSecurityPolicyConfiguration>
{
    public void Configure(EntityTypeBuilder<SocialSecurityPolicyConfiguration> b)
    {
        b.ToTable("SocialSecurityPolicyConfigurations", t => {
            t.HasCheckConstraint("CK_SocialSecurityPolicyConfigurations_Type", "[SchemeType] = 'SocialSecurity'");
            t.HasCheckConstraint("CK_SocialSecurityPolicyConfigurations_Values", "([EmployeeContributionRate] IS NULL OR [EmployeeContributionRate] >= 0) AND ([EmployerContributionRate] IS NULL OR [EmployerContributionRate] >= 0) AND ([MinimumContributionBase] IS NULL OR [MinimumContributionBase] >= 0) AND ([MaximumContributionBase] IS NULL OR [MaximumContributionBase] >= 0)");
            t.HasCheckConstraint("CK_SocialSecurityPolicyConfigurations_BaseRange", "[MinimumContributionBase] IS NULL OR [MaximumContributionBase] IS NULL OR [MaximumContributionBase] >= [MinimumContributionBase]");
        });
        b.HasKey(x => x.StatutoryPolicyVersionId);
        b.Property(x => x.SchemeType).HasMaxLength(30).IsRequired();
        b.Property(x => x.EmployeeContributionRate).HasColumnType("decimal(19,4)");
        b.Property(x => x.EmployerContributionRate).HasColumnType("decimal(19,4)");
        b.Property(x => x.MinimumContributionBase).HasColumnType("decimal(19,4)");
        b.Property(x => x.MaximumContributionBase).HasColumnType("decimal(19,4)");
        b.Property(x => x.InsuredPersonClassification).HasMaxLength(50);
        b.HasOne(x => x.Policy).WithOne(x => x.SocialSecurity)
            .HasForeignKey<SocialSecurityPolicyConfiguration>(x => new { x.StatutoryPolicyVersionId, x.SchemeType })
            .HasPrincipalKey<StatutoryPolicyVersion>(x => new { x.StatutoryPolicyVersionId, x.SchemeType }).OnDelete(DeleteBehavior.NoAction);
    }
}

internal sealed class PitPolicyConfigurationMapping : IEntityTypeConfiguration<PitPolicyConfiguration>
{
    public void Configure(EntityTypeBuilder<PitPolicyConfiguration> b)
    {
        b.ToTable("PitPolicyConfigurations", t => {
            t.HasCheckConstraint("CK_PitPolicyConfigurations_Type", "[SchemeType] = 'PersonalIncomeTax'");
            t.HasCheckConstraint("CK_PitPolicyConfigurations_TaxYear", "[TaxYear] IS NULL OR [TaxYear] BETWEEN 1 AND 9999");
            t.HasCheckConstraint("CK_PitPolicyConfigurations_Values", "([EmploymentExpenseDeductionRate] IS NULL OR [EmploymentExpenseDeductionRate] >= 0) AND ([EmploymentExpenseDeductionCap] IS NULL OR [EmploymentExpenseDeductionCap] >= 0) AND ([PersonalAllowanceAmount] IS NULL OR [PersonalAllowanceAmount] >= 0)");
        });
        b.HasKey(x => x.StatutoryPolicyVersionId);
        b.Property(x => x.SchemeType).HasMaxLength(30).IsRequired();
        b.Property(x => x.EmploymentExpenseDeductionRate).HasColumnType("decimal(19,4)");
        b.Property(x => x.EmploymentExpenseDeductionCap).HasColumnType("decimal(19,4)");
        b.Property(x => x.PersonalAllowanceAmount).HasColumnType("decimal(19,4)");
        b.Property(x => x.WithholdingMethodIdentifier).HasMaxLength(50);
        b.HasOne(x => x.Policy).WithOne(x => x.PersonalIncomeTax)
            .HasForeignKey<PitPolicyConfiguration>(x => new { x.StatutoryPolicyVersionId, x.SchemeType })
            .HasPrincipalKey<StatutoryPolicyVersion>(x => new { x.StatutoryPolicyVersionId, x.SchemeType }).OnDelete(DeleteBehavior.NoAction);
    }
}

internal sealed class PitTaxBracketConfiguration : IEntityTypeConfiguration<PitTaxBracket>
{
    public void Configure(EntityTypeBuilder<PitTaxBracket> b)
    {
        b.ToTable("PitTaxBrackets", t => {
            t.HasCheckConstraint("CK_PitTaxBrackets_Values", "[SortOrder] > 0 AND [LowerBoundInclusive] >= 0 AND [Rate] >= 0 AND ([UpperBoundExclusive] IS NULL OR [UpperBoundExclusive] > [LowerBoundInclusive])");
        });
        b.HasKey(x => x.PitTaxBracketId);
        b.Property(x => x.LowerBoundInclusive).HasColumnType("decimal(19,4)");
        b.Property(x => x.UpperBoundExclusive).HasColumnType("decimal(19,4)");
        b.Property(x => x.Rate).HasColumnType("decimal(19,4)");
        b.HasIndex(x => new { x.StatutoryPolicyVersionId, x.SortOrder }).IsUnique().HasDatabaseName("UX_PitTaxBrackets_Order");
        b.HasIndex(x => new { x.StatutoryPolicyVersionId, x.LowerBoundInclusive }).IsUnique().HasDatabaseName("UX_PitTaxBrackets_LowerBound");
        b.HasOne(x => x.Configuration).WithMany(x => x.Brackets).HasForeignKey(x => x.StatutoryPolicyVersionId).OnDelete(DeleteBehavior.NoAction);
    }
}
