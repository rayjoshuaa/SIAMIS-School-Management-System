using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIAMIS.Domain.Entities.Employees;

namespace SIAMIS.Infrastructure.Configurations;
public sealed class EmployeePhotoRevisionConfiguration : IEntityTypeConfiguration<EmployeePhotoRevision>
{
    public void Configure(EntityTypeBuilder<EmployeePhotoRevision> b)
    {
        b.ToTable("EmployeePhotoRevisions", t => t.HasCheckConstraint("CK_EmployeePhoto_Binary", "([Operation]='Remove' AND [StorageKey] IS NULL AND [Sha256] IS NULL AND [ContentType] IS NULL AND [SizeBytes] IS NULL AND [Width] IS NULL AND [Height] IS NULL) OR ([Operation] IN ('Upload','Replace') AND [StorageKey] IS NOT NULL AND [Sha256] IS NOT NULL AND [ContentType] IS NOT NULL AND [SizeBytes] IS NOT NULL AND [Width] IS NOT NULL AND [Height] IS NOT NULL AND [ContentType] IN ('image/jpeg','image/png') AND [SizeBytes] > 0 AND [SizeBytes] <= 5242880 AND [Width] BETWEEN 1 AND 4096 AND [Height] BETWEEN 1 AND 4096)"));
        b.HasKey(x => x.Id);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.EmployeeId).IsUnique().HasFilter("[IsCurrent] = 1");
        b.HasIndex(x => new { x.EmployeeId, x.CreatedAtUtc });
        b.Property(x => x.StorageKey).HasMaxLength(37);
        b.Property(x => x.Sha256).HasMaxLength(64);
        b.Property(x => x.ContentType).HasMaxLength(20);
        b.Property(x => x.Operation).HasMaxLength(10);
        b.Property(x => x.IsCurrent).IsConcurrencyToken();
        b.Property(x => x.CreatedAtUtc).HasColumnType("datetime2(7)");
    }
}
