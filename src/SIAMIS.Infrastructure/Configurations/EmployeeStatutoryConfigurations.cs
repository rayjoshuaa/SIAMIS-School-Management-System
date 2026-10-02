using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIAMIS.Domain.Common;
using SIAMIS.Domain.Entities.Payroll;

namespace SIAMIS.Infrastructure.Configurations;

internal static class EmployeeStatutoryMapping
{
    public static void Timestamps<T>(EntityTypeBuilder<T> b) where T : class, IHasTimestamps
    {
        b.Property(x => x.CreatedAt).HasColumnType("datetime2");
        b.Property(x => x.UpdatedAt).HasColumnType("datetime2");
    }
}

internal sealed class EmployeeStatutoryEnrollmentConfiguration : IEntityTypeConfiguration<EmployeeStatutoryEnrollment>
{
    public void Configure(EntityTypeBuilder<EmployeeStatutoryEnrollment> b)
    {
        b.ToTable("EmployeeStatutoryEnrollments", t => {
            t.HasCheckConstraint("CK_EmployeeStatutoryEnrollments_Dates", "[EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveFrom]");
            t.HasCheckConstraint("CK_EmployeeStatutoryEnrollments_Applicability", "[Applicability] IN ('Applicable','NotApplicable')");
        });
        b.HasKey(x => x.EmployeeStatutoryEnrollmentId);
        b.Property(x => x.Applicability).HasMaxLength(20).IsRequired();
        b.Property(x => x.MembershipNumber).HasMaxLength(100);
        b.Property(x => x.Remarks).HasMaxLength(2000);
        b.Property(x => x.EffectiveFrom).HasColumnType("date");
        b.Property(x => x.EffectiveTo).HasColumnType("date");
        EmployeeStatutoryMapping.Timestamps(b);
        b.HasIndex(x => new { x.EmployeeId, x.StatutorySchemeId, x.EffectiveFrom, x.EffectiveTo });
        b.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.Scheme).WithMany().HasForeignKey(x => x.StatutorySchemeId).OnDelete(DeleteBehavior.NoAction);
    }
}

internal sealed class EmployeeTaxProfileConfiguration : IEntityTypeConfiguration<EmployeeTaxProfile>
{
    public void Configure(EntityTypeBuilder<EmployeeTaxProfile> b)
    {
        b.ToTable("EmployeeTaxProfiles");
        b.HasKey(x => x.EmployeeTaxProfileId);
        b.HasIndex(x => x.EmployeeId).IsUnique();
        b.Property(x => x.TaxpayerIdentificationNumber).HasMaxLength(100);
        EmployeeStatutoryMapping.Timestamps(b);
        b.HasOne(x => x.Employee).WithOne().HasForeignKey<EmployeeTaxProfile>(x => x.EmployeeId).OnDelete(DeleteBehavior.NoAction);
    }
}

internal sealed class EmployeeTaxDeclarationConfiguration : IEntityTypeConfiguration<EmployeeTaxDeclaration>
{
    public void Configure(EntityTypeBuilder<EmployeeTaxDeclaration> b)
    {
        b.ToTable("EmployeeTaxDeclarations", t => {
            t.HasCheckConstraint("CK_EmployeeTaxDeclarations_ResidencyStatus", "[ResidencyStatus] IN ('Unknown','Resident','NonResident')");
            t.HasCheckConstraint("CK_EmployeeTaxDeclarations_EmploymentTaxTreatment", "[EmploymentTaxTreatment] IN ('Unknown','StandardSection40_1','RequiresReview')");
            t.HasCheckConstraint("CK_EmployeeTaxDeclarations_YearRevision", "[TaxYear] BETWEEN 1 AND 9999 AND [RevisionNumber] > 0");
            t.HasCheckConstraint("CK_EmployeeTaxDeclarations_Status", "[Status] IN ('Draft','Verified')");
            t.HasCheckConstraint("CK_EmployeeTaxDeclarations_Verification", "([Status]='Draft' AND [VerifiedAt] IS NULL AND [TaxpayerIdentificationNumberSnapshot] IS NULL) OR ([Status]='Verified' AND [VerifiedAt] IS NOT NULL)");
            t.HasCheckConstraint("CK_EmployeeTaxDeclarations_Replacement", "[ReplacesDeclarationId] IS NULL OR [ReplacesDeclarationId] <> [EmployeeTaxDeclarationId]");
        });
        b.HasKey(x => x.EmployeeTaxDeclarationId);
        b.HasAlternateKey(x => new { x.EmployeeId, x.TaxYear, x.EmployeeTaxDeclarationId });
        b.HasIndex(x => new { x.EmployeeId, x.TaxYear, x.RevisionNumber }).IsUnique();
        b.HasIndex(x => new { x.EmployeeId, x.TaxYear }).IsUnique().HasFilter("[Status] = 'Draft'")
            .HasDatabaseName("UX_EmployeeTaxDeclarations_OneDraft");
        b.Property(x => x.Status).HasMaxLength(20).IsRequired();
        b.Property(x => x.ResidencyStatus).HasMaxLength(20).HasDefaultValue("Unknown").IsRequired();
        b.Property(x => x.EmploymentTaxTreatment).HasMaxLength(30).HasDefaultValue("Unknown").IsRequired();
        b.Property(x => x.TaxpayerIdentificationNumberSnapshot).HasMaxLength(100);
        b.Property(x => x.Remarks).HasMaxLength(2000);
        b.Property(x => x.VerifiedAt).HasColumnType("datetime2");
        EmployeeStatutoryMapping.Timestamps(b);
        b.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.ReplacesDeclaration).WithMany()
            .HasForeignKey(x => new { x.EmployeeId, x.TaxYear, x.ReplacesDeclarationId })
            .HasPrincipalKey(x => new { x.EmployeeId, x.TaxYear, x.EmployeeTaxDeclarationId }).OnDelete(DeleteBehavior.NoAction);
    }
}

internal sealed class EmployeeTaxDeclarationSelectionConfiguration : IEntityTypeConfiguration<EmployeeTaxDeclarationSelection>
{
    public void Configure(EntityTypeBuilder<EmployeeTaxDeclarationSelection> b)
    {
        b.ToTable("EmployeeTaxDeclarationSelections", t =>
            t.HasCheckConstraint("CK_EmployeeTaxDeclarationSelections_Year", "[TaxYear] BETWEEN 1 AND 9999"));
        b.HasKey(x => new { x.EmployeeId, x.TaxYear });
        b.HasOne(x => x.Declaration).WithMany()
            .HasForeignKey(x => new { x.EmployeeId, x.TaxYear, x.CurrentDeclarationId })
            .HasPrincipalKey(x => new { x.EmployeeId, x.TaxYear, x.EmployeeTaxDeclarationId }).OnDelete(DeleteBehavior.NoAction);
    }
}

internal sealed class EmployeeTaxClaimConfiguration : IEntityTypeConfiguration<EmployeeTaxClaim>
{
    public void Configure(EntityTypeBuilder<EmployeeTaxClaim> b)
    {
        b.ToTable("EmployeeTaxClaims", t => {
            t.HasCheckConstraint("CK_EmployeeTaxClaims_Type", "[ClaimType] IN ('Spouse','Child','Parent')");
            t.HasCheckConstraint("CK_EmployeeTaxClaims_Values", "([Amount] IS NULL OR [Amount]>=0) AND ([Quantity] IS NULL OR [Quantity]>0)");
        });
        b.HasKey(x => x.EmployeeTaxClaimId);
        b.Property(x => x.ClaimType).HasMaxLength(20).IsRequired();
        b.Property(x => x.Amount).HasColumnType("decimal(19,4)");
        b.Property(x => x.Reference).HasMaxLength(500);
        b.Property(x => x.Remarks).HasMaxLength(2000);
        EmployeeStatutoryMapping.Timestamps(b);
        b.HasOne(x => x.Declaration).WithMany(x => x.Claims).HasForeignKey(x => x.EmployeeTaxDeclarationId).OnDelete(DeleteBehavior.NoAction);
    }
}

internal sealed class EmployeeTaxOpeningBalanceConfiguration : IEntityTypeConfiguration<EmployeeTaxOpeningBalance>
{
    public void Configure(EntityTypeBuilder<EmployeeTaxOpeningBalance> b)
    {
        b.ToTable("EmployeeTaxOpeningBalances", t => {
            t.HasCheckConstraint("CK_EmployeeTaxOpeningBalances_Currency", "[Currency]='THB'");
            t.HasCheckConstraint("CK_EmployeeTaxOpeningBalances_State", "[State] IN ('Unknown','ConfirmedZero','VerifiedAmount')");
            t.HasCheckConstraint("CK_EmployeeTaxOpeningBalances_Values", "([PriorTaxableEmploymentIncome] IS NULL OR [PriorTaxableEmploymentIncome]>=0) AND ([PriorTaxWithheld] IS NULL OR [PriorTaxWithheld]>=0) AND ([PriorSocialSecurityContribution] IS NULL OR [PriorSocialSecurityContribution]>=0)");
            t.HasCheckConstraint("CK_EmployeeTaxOpeningBalances_Consistency",
                "([State]='Unknown' AND [PriorTaxableEmploymentIncome] IS NULL AND [PriorTaxWithheld] IS NULL AND [PriorSocialSecurityContribution] IS NULL AND [VerifiedAt] IS NULL) OR " +
                "([State] IN ('ConfirmedZero','VerifiedAmount') AND [PriorTaxableEmploymentIncome] IS NOT NULL AND [PriorTaxWithheld] IS NOT NULL AND [PriorSocialSecurityContribution] IS NOT NULL AND [VerifiedAt] IS NOT NULL AND [Remarks] IS NOT NULL AND LEN(LTRIM(RTRIM([Remarks])))>0 AND " +
                "([State]='VerifiedAmount' OR ([PriorTaxableEmploymentIncome]=0 AND [PriorTaxWithheld]=0 AND [PriorSocialSecurityContribution]=0)))");
        });
        b.HasKey(x => x.EmployeeTaxDeclarationId);
        b.Property(x => x.State).HasMaxLength(20).IsRequired();
        b.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        b.Property(x => x.PriorTaxableEmploymentIncome).HasColumnType("decimal(19,4)");
        b.Property(x => x.PriorTaxWithheld).HasColumnType("decimal(19,4)");
        b.Property(x => x.PriorSocialSecurityContribution).HasColumnType("decimal(19,4)");
        b.Property(x => x.AsOfDate).HasColumnType("date");
        b.Property(x => x.Remarks).HasMaxLength(2000);
        b.Property(x => x.VerifiedAt).HasColumnType("datetime2");
        EmployeeStatutoryMapping.Timestamps(b);
        b.HasOne(x => x.Declaration).WithOne(x => x.OpeningBalance).HasForeignKey<EmployeeTaxOpeningBalance>(x => x.EmployeeTaxDeclarationId).OnDelete(DeleteBehavior.NoAction);
    }
}
