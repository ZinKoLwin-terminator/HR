using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Domain.Activity;
using Apex.Hrms.Domain.Notifications;
using Apex.Hrms.Infrastructure.Identity;
using Apex.Hrms.Infrastructure.Persistence;
using Apex.Hrms.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Apex.Hrms.Infrastructure.Tests;

/// <summary>
/// The app schema part of the EF model (app.ActivityLog, app.Notification), the recorder that writes activity rows in the
/// unit of work of a change, and the business-day start the activity date filters use. No connection is ever opened.
/// </summary>
public sealed class ActivityPersistenceTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 9, 8, 0, 0, TimeSpan.Zero);

    private readonly HrmsDbContext context = new(
        new DbContextOptionsBuilder<HrmsDbContext>()
            .UseSqlServer("Server=.;Database=HR;Integrated Security=True;Encrypt=True;TrustServerCertificate=True")
            .Options);

    public void Dispose() => context.Dispose();

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow => Now;

        public DateOnly Today => DateOnly.FromDateTime(Now.UtcDateTime);

        public TimeOnly TimeNow => TimeOnly.FromDateTime(Now.UtcDateTime);
    }

    private sealed class TestUser(int? userId, bool isSystem = false) : ICurrentUser
    {
        public int? UserId { get; } = userId;

        public int? EmployeeId => null;

        public bool IsAuthenticated => UserId is not null;

        public bool IsSystem { get; } = isSystem;

        public string DisplayName => "TST- Test User";
    }

    private EfActivityRecorder Recorder(ICurrentUser user) => new(context, new FixedClock(), user);

    private IEnumerable<ActivityLogEntry> Added() =>
        context.ChangeTracker.Entries<ActivityLogEntry>().Where(e => e.State == EntityState.Added).Select(e => e.Entity);

    // ---- Mapping ----

    [Fact]
    public void The_tables_map_to_the_app_schema_with_bigint_identity_keys()
    {
        var log = context.Model.FindEntityType(typeof(ActivityLogEntry))!;
        var notification = context.Model.FindEntityType(typeof(Notification))!;

        Assert.Equal(("app", "ActivityLog"), (log.GetSchema(), log.GetTableName()));
        Assert.Equal(("app", "Notification"), (notification.GetSchema(), notification.GetTableName()));
        Assert.Equal("bigint", log.FindProperty("Id")!.GetColumnType());
        Assert.Equal("bigint", notification.FindProperty("Id")!.GetColumnType());
        Assert.Equal(typeof(long), log.FindProperty("Id")!.ClrType);
        Assert.Equal(ValueGenerated.OnAdd, log.FindProperty("Id")!.ValueGenerated);
    }

    [Fact]
    public void Activity_columns_have_the_ssdt_types_and_sizes()
    {
        var log = context.Model.FindEntityType(typeof(ActivityLogEntry))!;

        Assert.Equal(30, log.FindProperty("Category")!.GetMaxLength());
        Assert.False(log.FindProperty("Category")!.IsUnicode());
        Assert.Equal(500, log.FindProperty("Message")!.GetMaxLength());
        Assert.Equal(130, log.FindProperty("ActorName")!.GetMaxLength());
        Assert.Equal("datetime2(3)", log.FindProperty("OccurredAtUtc")!.GetColumnType());
        Assert.True(log.FindProperty("ActorUserId")!.IsNullable);
        Assert.False(log.FindProperty("Message")!.IsNullable);
    }

    [Fact]
    public void Notification_columns_have_the_ssdt_types_and_sizes()
    {
        var notification = context.Model.FindEntityType(typeof(Notification))!;

        Assert.True(notification.FindProperty("RecipientUserId")!.IsNullable);
        Assert.Equal(120, notification.FindProperty("Title")!.GetMaxLength());
        Assert.Equal(500, notification.FindProperty("Message")!.GetMaxLength());
        Assert.Equal(40, notification.FindProperty("Icon")!.GetMaxLength());
        Assert.False(notification.FindProperty("Icon")!.IsUnicode());
        Assert.Equal(300, notification.FindProperty("Link")!.GetMaxLength());
        Assert.True(notification.FindProperty("Link")!.IsNullable);
        Assert.Equal("datetime2(3)", notification.FindProperty("CreatedAtUtc")!.GetColumnType());
        Assert.Equal("datetime2(3)", notification.FindProperty("ReadAtUtc")!.GetColumnType());
        Assert.True(notification.FindProperty("ReadAtUtc")!.IsNullable);
        Assert.Equal(typeof(bool), notification.FindProperty("IsRead")!.ClrType);
    }

    // ---- The recorder ----

    [Fact]
    public void A_signed_in_users_entry_carries_the_user_id_the_name_and_the_clock_time()
    {
        Recorder(new TestUser(42)).Record("leave", "LV-0001 approved");

        var entry = Assert.Single(Added());
        Assert.Equal(("leave", "LV-0001 approved", 42, "TST- Test User"), (entry.Category, entry.Message, entry.ActorUserId, entry.ActorName));
        Assert.Equal(Now.UtcDateTime, entry.OccurredAtUtc);
    }

    [Fact]
    public void The_system_identity_writes_with_no_user_id_and_the_name_System()
    {
        Recorder(new TestUser(null, isSystem: true)).Record("system", "Seeded");

        var entry = Assert.Single(Added());
        Assert.Null(entry.ActorUserId);
        Assert.Equal("System", entry.ActorName);
    }

    [Fact]
    public void A_deferred_message_is_not_added_until_the_unit_of_work_writes_it_and_is_worked_out_then()
    {
        var recorder = Recorder(new TestUser(42));
        var id = 0;

        recorder.Record("attendance", () => $"Adjustment request \"ADJ-{id:0000}\" created");
        Assert.True(recorder.HasDeferred);
        Assert.Empty(Added());

        id = 7;
        recorder.WriteDeferred();

        Assert.False(recorder.HasDeferred);
        Assert.Equal("Adjustment request \"ADJ-0007\" created", Assert.Single(Added()).Message);
    }

    [Fact]
    public void An_invalid_category_is_a_programming_error()
    {
        Assert.Throws<InvalidOperationException>(() => Recorder(new TestUser(1)).Record("payslips", "x"));
    }

    // ---- The audit rule covers activity rows ----

    private void Save(ICurrentUser user)
    {
        var interceptor = new AuditStampInterceptor(new FixedClock(), user);
        interceptor.SavingChanges(new DbContextEventData(null!, (_, _) => string.Empty, context), default);
    }

    [Fact]
    public void An_activity_row_without_a_user_is_rejected_like_any_audited_write()
    {
        Recorder(new TestUser(null)).Record("system", "Anonymous");

        Assert.Throws<UnauthenticatedWriteException>(() => Save(new AnonymousCurrentUser()));
    }

    [Fact]
    public void An_activity_row_by_a_signed_in_user_or_the_system_identity_is_allowed()
    {
        Recorder(new TestUser(1)).Record("system", "Signed in");
        Save(new TestUser(1));

        Recorder(new TestUser(null, isSystem: true)).Record("system", "System");
        Save(new TestUser(null, isSystem: true));
    }

    // ---- Business day start ----

    [Fact]
    public void The_business_day_starts_at_local_midnight_expressed_in_utc()
    {
        var yangon = TimeZoneInfo.CreateCustomTimeZone("Test/Yangon", TimeSpan.FromMinutes(390), "Yangon", "Yangon");
        var clock = new SystemClock(TimeProvider.System, yangon);

        var start = clock.StartOfDayUtc(new DateOnly(2026, 11, 9));

        Assert.Equal(new DateTime(2026, 11, 8, 17, 30, 0, DateTimeKind.Utc), start);
        Assert.Equal(DateTimeKind.Utc, start.Kind);
    }

    [Fact]
    public void In_utc_the_business_day_starts_at_midnight_utc()
    {
        var clock = new SystemClock(TimeProvider.System, TimeZoneInfo.Utc);

        Assert.Equal(new DateTime(2026, 11, 9, 0, 0, 0, DateTimeKind.Utc), clock.StartOfDayUtc(new DateOnly(2026, 11, 9)));
    }
}
