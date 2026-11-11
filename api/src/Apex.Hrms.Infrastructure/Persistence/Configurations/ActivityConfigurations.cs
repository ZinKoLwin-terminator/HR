using Apex.Hrms.Domain.Activity;
using Apex.Hrms.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Apex.Hrms.Infrastructure.Persistence.Configurations;

/// <summary>Maps app.ActivityLog. The CHECK on the category stays in the SSDT project.</summary>
internal sealed class ActivityLogEntryConfiguration : IEntityTypeConfiguration<ActivityLogEntry>
{
    public void Configure(EntityTypeBuilder<ActivityLogEntry> builder)
    {
        builder.ToTable("ActivityLog", "app");
        builder.HasKey(entry => entry.Id).HasName("PK_ActivityLog");
        builder.Property(entry => entry.Id).HasColumnType("bigint").UseIdentityColumn();

        builder.Property(entry => entry.Category).HasMaxLength(30).IsUnicode(false);
        builder.Property(entry => entry.Message).HasMaxLength(ActivityLogEntry.MessageMaxLength);
        builder.Property(entry => entry.ActorUserId);
        builder.Property(entry => entry.ActorName).HasMaxLength(ActivityLogEntry.ActorNameMaxLength);
        builder.Property(entry => entry.OccurredAtUtc).HasColumnType("datetime2(3)");
        builder.HasIndex(entry => entry.OccurredAtUtc).IsDescending().HasDatabaseName("IX_ActivityLog_OccurredAtUtc");
    }
}

/// <summary>Maps app.Notification. The foreign key to sec.AppUser and the read CHECK stay in the SSDT project.</summary>
internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notification", "app");
        builder.HasKey(notification => notification.Id).HasName("PK_Notification");
        builder.Property(notification => notification.Id).HasColumnType("bigint").UseIdentityColumn();

        builder.Property(notification => notification.RecipientUserId);
        builder.Property(notification => notification.Title).HasMaxLength(Notification.TitleMaxLength);
        builder.Property(notification => notification.Message).HasMaxLength(Notification.MessageMaxLength);
        builder.Property(notification => notification.Icon).HasMaxLength(Notification.IconMaxLength).IsUnicode(false);
        builder.Property(notification => notification.Link).HasMaxLength(Notification.LinkMaxLength);
        builder.Property(notification => notification.CreatedAtUtc).HasColumnType("datetime2(3)");
        builder.Property(notification => notification.IsRead);
        builder.Property(notification => notification.ReadAtUtc).HasColumnType("datetime2(3)");
        builder.HasIndex(notification => new { notification.RecipientUserId, notification.CreatedAtUtc })
            .IsDescending(false, true)
            .HasDatabaseName("IX_Notification_RecipientUserId_CreatedAtUtc");
    }
}
