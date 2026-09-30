using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIAMIS.Domain.Entities.MasterData;

namespace SIAMIS.Infrastructure.Configurations;

internal sealed class MasterDataConfiguration<T>(string tableName) : IEntityTypeConfiguration<T>
    where T : MasterDataEntity
{
    public void Configure(EntityTypeBuilder<T> builder)
    {
        builder.ToTable(tableName);
        builder.Property(item => item.Name).HasMaxLength(150).IsRequired();
        builder.Property(item => item.Code).HasMaxLength(50);
        builder.Property(item => item.Description).HasMaxLength(1000);
        builder.Property(item => item.IsActive).IsRequired();
        builder.Property(item => item.CreatedAt).HasColumnType("datetime2").IsRequired();
        builder.Property(item => item.UpdatedAt).HasColumnType("datetime2").IsRequired();
        builder.HasIndex(item => item.Code).IsUnique().HasFilter("[Code] IS NOT NULL");
        builder.HasIndex(item => item.Name);
    }
}
