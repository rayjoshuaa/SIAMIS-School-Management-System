using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SIAMIS.Application.Security;
using SIAMIS.Domain.Entities.Employees;
using SIAMIS.Infrastructure.Security;

namespace SIAMIS.Infrastructure.Configurations;

internal static class SecurityConfiguration
{
    public static void Configure(ModelBuilder b)
    {
        b.Entity<ApplicationUser>(u =>
        {
            u.ToTable("Users");
            u.HasIndex(x => x.EmployeeId).IsUnique().HasFilter("[EmployeeId] IS NOT NULL");
            u.HasIndex(x => x.NormalizedEmail).IsUnique().HasFilter("[NormalizedEmail] IS NOT NULL");
            u.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.NoAction);
            u.Property(x => x.CreatedAtUtc).HasColumnType("datetime2(7)");
            u.Property(x => x.AdministrationVersion).HasMaxLength(36);
        });
        b.Entity<IdentityRole<Guid>>().ToTable("Roles");
        b.Entity<IdentityUserRole<Guid>>().ToTable("UserRoles");
        b.Entity<IdentityUserClaim<Guid>>().ToTable("UserClaims");
        b.Entity<IdentityUserLogin<Guid>>().ToTable("UserLogins");
        b.Entity<IdentityUserToken<Guid>>().ToTable("UserTokens");
        b.Entity<IdentityUserLogin<Guid>>().Property(x=>x.LoginProvider).HasMaxLength(128);
        b.Entity<IdentityUserLogin<Guid>>().Property(x=>x.ProviderKey).HasMaxLength(128);
        b.Entity<IdentityUserToken<Guid>>().Property(x=>x.LoginProvider).HasMaxLength(128);
        b.Entity<IdentityUserToken<Guid>>().Property(x=>x.Name).HasMaxLength(128);
        b.Entity<IdentityRoleClaim<Guid>>().ToTable("RoleClaims");
        int n = 1;
        foreach (var role in SecurityCapabilities.Roles.Keys)
        {
            var id = Guid.Parse($"D1000000-0000-0000-0000-{n++:D12}");
            b.Entity<IdentityRole<Guid>>().HasData(new IdentityRole<Guid>(role) { Id = id, NormalizedName = role.ToUpperInvariant(), ConcurrencyStamp = id.ToString() });
        }
        b.Entity<SecurityAuditEvent>(a =>
        {
            a.ToTable("SecurityAuditEvents"); a.HasKey(x => x.Id);
            a.Property(x => x.Operation).HasMaxLength(200).IsRequired();
            a.Property(x => x.ResourceType).HasMaxLength(100).IsRequired();
            a.Property(x => x.ResourceId).HasMaxLength(100);
            a.Property(x => x.OccurredAtUtc).HasColumnType("datetime2(7)");
            a.HasIndex(x => x.OccurredAtUtc);
            // Historical attribution is a snapshot identifier; accounts have no hard-delete API.
        });
    }
}
