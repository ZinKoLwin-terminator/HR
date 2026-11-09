using Apex.Hrms.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Apex.Hrms.Infrastructure.Persistence.Configurations;

internal static class EnumTextExtensions
{
    /// <summary>Stores an enum as the text of its <see cref="TextMap{TEnum}"/> in a non-Unicode varchar column, as the SSDT project does.</summary>
    public static PropertyBuilder<TEnum> HasTextConversion<TEnum>(this PropertyBuilder<TEnum> property, TextMap<TEnum> map, int maxLength = 30)
        where TEnum : struct, Enum =>
        property
            .HasConversion(new ValueConverter<TEnum, string>(value => map.ToText(value), text => map.Parse(text)))
            .HasMaxLength(maxLength)
            .IsUnicode(false);

    /// <summary>The audit columns and the rowversion concurrency token every audited table has.</summary>
    public static void ConfigureAudit<TEntity>(this EntityTypeBuilder<TEntity> builder)
        where TEntity : AuditedEntity
    {
        builder.Property(entity => entity.CreatedAtUtc).HasColumnType("datetime2(3)");
        builder.Property(entity => entity.ModifiedAtUtc).HasColumnType("datetime2(3)");
        builder.Property(entity => entity.RowVersion).IsRowVersion();
    }
}
