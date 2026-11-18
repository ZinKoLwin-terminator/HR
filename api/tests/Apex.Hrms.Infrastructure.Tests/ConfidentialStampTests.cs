using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Infrastructure.Identity;
using Apex.Hrms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Apex.Hrms.Infrastructure.Tests;

/// <summary>
/// A confidential row (<see cref="ConfidentialEntity"/>) gets its times stamped but never a user id, and it still needs a
/// signed-in user or the system identity. A small model of its own is used (the grievance mapping comes later); the interceptor
/// is called directly, so no connection is ever opened.
/// </summary>
public sealed class ConfidentialStampTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Later = Now.AddHours(2);

    private readonly TestContext context = new();

    public void Dispose() => context.Dispose();

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;

        public DateOnly Today => DateOnly.FromDateTime(now.UtcDateTime);

        public TimeOnly TimeNow => TimeOnly.FromDateTime(now.UtcDateTime);
    }

    private sealed class TestUser(int? userId) : ICurrentUser
    {
        public int? UserId { get; } = userId;

        public int? EmployeeId => null;

        public bool IsAuthenticated => UserId is not null;

        public bool IsSystem => false;

        public string DisplayName => "TST- Test User";
    }

    private sealed class SecretRecord : ConfidentialEntity
    {
        public string Text { get; set; } = string.Empty;
    }

    private sealed class TestContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
            optionsBuilder.UseSqlServer("Server=.;Database=Placeholder;Integrated Security=True;Encrypt=True;TrustServerCertificate=True");

        protected override void OnModelCreating(ModelBuilder modelBuilder) =>
            modelBuilder.Entity<SecretRecord>(entity =>
            {
                entity.HasKey(record => record.Id);
                entity.Property(record => record.RowVersion).IsRowVersion();
            });
    }

    private SecretRecord TrackedNew()
    {
        var record = new SecretRecord { Text = "TST- confidential" };
        context.Add(record);
        return record;
    }

    private void Save(ICurrentUser user, DateTimeOffset? at = null)
    {
        var interceptor = new AuditStampInterceptor(new FixedClock(at ?? Now), user);
        var data = new DbContextEventData(null!, (_, _) => string.Empty, context);
        interceptor.SavingChanges(data, default);
    }

    [Fact]
    public void A_confidential_entity_has_no_user_id_columns_at_all()
    {
        var properties = typeof(ConfidentialEntity).GetProperties().Select(property => property.Name);

        Assert.DoesNotContain(properties, name => name.Contains("User", StringComparison.Ordinal));
        Assert.Equal(["CreatedAtUtc", "Id", "ModifiedAtUtc", "RowVersion"], properties.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void A_signed_in_write_stamps_the_times_only()
    {
        var record = TrackedNew();

        Save(new TestUser(userId: 42));

        Assert.Equal(Now.UtcDateTime, record.CreatedAtUtc);
        Assert.Equal(Now.UtcDateTime, record.ModifiedAtUtc);
        Assert.DoesNotContain(context.Entry(record).Properties, property => property.Metadata.Name.Contains("User", StringComparison.Ordinal));
    }

    [Fact]
    public void A_change_moves_only_the_modification_time()
    {
        var record = TrackedNew();
        Save(new TestUser(userId: 42));
        context.Entry(record).Property(nameof(SecretRecord.Id)).CurrentValue = 7;
        context.Entry(record).State = EntityState.Unchanged;
        record.Text = "TST- changed";
        context.ChangeTracker.DetectChanges();

        Save(new TestUser(userId: 43), Later);

        Assert.Equal(Now.UtcDateTime, record.CreatedAtUtc);
        Assert.Equal(Later.UtcDateTime, record.ModifiedAtUtc);
    }

    [Fact]
    public void A_confidential_write_without_a_user_is_rejected()
    {
        TrackedNew();

        var exception = Assert.Throws<UnauthenticatedWriteException>(() => Save(new AnonymousCurrentUser()));

        Assert.Equal(UnauthenticatedWriteException.PublicMessage, exception.Message);
    }

    [Fact]
    public void The_system_identity_may_write_a_confidential_row()
    {
        var record = TrackedNew();

        Save(new SystemCurrentUser());

        Assert.Equal(Now.UtcDateTime, record.CreatedAtUtc);
    }
}
