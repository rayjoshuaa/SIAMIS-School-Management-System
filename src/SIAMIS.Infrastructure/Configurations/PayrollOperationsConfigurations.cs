using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIAMIS.Domain.Entities;
using SIAMIS.Domain.Entities.Payroll;

namespace SIAMIS.Infrastructure.Configurations;

internal sealed class OrganizationProfileConfiguration : IEntityTypeConfiguration<OrganizationProfile>
{
    public void Configure(EntityTypeBuilder<OrganizationProfile> builder)
    {
        builder.ToTable("OrganizationProfiles", table =>
        {
            table.HasCheckConstraint("CK_OrganizationProfiles_Singleton", "[OrganizationProfileId] = '00000000-0000-0000-0000-000000000001'");
            table.HasCheckConstraint("CK_OrganizationProfiles_DisplayName", "LEN(LTRIM(RTRIM([DisplayName]))) > 0");
        });
        builder.HasKey(x => x.OrganizationProfileId);
        builder.Property(x => x.OrganizationProfileId).ValueGeneratedNever();
        builder.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.AddressLine1).HasMaxLength(300);
        builder.Property(x => x.AddressLine2).HasMaxLength(300);
        builder.Property(x => x.Phone).HasMaxLength(50);
        builder.Property(x => x.Email).HasMaxLength(254);
        builder.Property(x => x.CreatedAt).HasColumnType("datetime2");
        builder.Property(x => x.UpdatedAt).HasColumnType("datetime2");
    }
}

internal sealed class EmployeePayslipConfiguration : IEntityTypeConfiguration<EmployeePayslip>
{
    public void Configure(EntityTypeBuilder<EmployeePayslip> builder)
    {
        builder.ToTable("EmployeePayslips", table =>
        {
            table.HasCheckConstraint("CK_EmployeePayslips_Json", "ISJSON([SnapshotJson]) = 1");
            table.HasCheckConstraint("CK_EmployeePayslips_Version", "[SnapshotVersion] = 1");
        });
        builder.HasKey(x => x.EmployeePayslipId);
        builder.HasIndex(x => x.EmployeePayrollId).IsUnique();
        builder.HasOne<EmployeePayroll>().WithOne().HasForeignKey<EmployeePayslip>(x => x.EmployeePayrollId).OnDelete(DeleteBehavior.NoAction);
        builder.Property(x => x.SnapshotJson).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnType("datetime2");
        builder.Property(x => x.UpdatedAt).HasColumnType("datetime2");
    }
}
