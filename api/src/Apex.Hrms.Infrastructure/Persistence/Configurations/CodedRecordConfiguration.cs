using Apex.Hrms.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Apex.Hrms.Infrastructure.Persistence.Configurations;

/// <summary>
/// Columns every org.* table shares: identity key, unique code, status, audit columns and rowversion.
/// CHECK constraints and column defaults stay in the SSDT project; the application always supplies those values.
/// </summary>
internal abstract class CodedRecordConfiguration<TEntity>(string tableName) : IEntityTypeConfiguration<TEntity>
    where TEntity : CodedRecord
{
    public void Configure(EntityTypeBuilder<TEntity> builder)
    {
        builder.ToTable(tableName, "org");
        builder.HasKey(entity => entity.Id).HasName($"PK_{tableName}");
        builder.Property(entity => entity.Id).UseIdentityColumn();

        builder.Property(entity => entity.Code).HasMaxLength(20);
        builder.HasIndex(entity => entity.Code).IsUnique().HasDatabaseName($"UQ_{tableName}_Code");

        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(30).IsUnicode(false);

        builder.Property(entity => entity.CreatedAtUtc).HasColumnType("datetime2(3)");
        builder.Property(entity => entity.ModifiedAtUtc).HasColumnType("datetime2(3)");
        builder.Property(entity => entity.RowVersion).IsRowVersion();

        ConfigureColumns(builder);
        ConfigureRelationships(builder);
    }

    protected abstract void ConfigureColumns(EntityTypeBuilder<TEntity> builder);

    protected virtual void ConfigureRelationships(EntityTypeBuilder<TEntity> builder)
    {
    }
}
