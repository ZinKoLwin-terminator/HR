using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Organization;
using Apex.Hrms.Infrastructure.Identity;
using Apex.Hrms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Apex.Hrms.Infrastructure.Tests;

/// <summary>
/// The audit rule: an audited row that is added or changed needs a signed-in user, except for the explicit system
/// identity. The interceptor is called directly, so no connection is ever opened.
/// </summary>
public sealed class AuditStampInterceptorTests : IDisposable
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
    }

    private sealed class TestUser(int? userId, bool isSystem = false) : ICurrentUser
    {
        public int? UserId { get; } = userId;

        public int? EmployeeId => null;

        public bool IsAuthenticated => UserId is not null;

        public bool IsSystem { get; } = isSystem;

        public string DisplayName => "TST- Test User";
    }

    private JobGrade TrackedNewGrade()
    {
        var grade = JobGrade.Create(new JobGradeDetails("TST-G1", "Test Grade", 1, 100m, 200m, null), RecordStatus.Active).Value;
        context.Add(grade);
        return grade;
    }

    private void Save(ICurrentUser user)
    {
        var interceptor = new AuditStampInterceptor(new FixedClock(), user);
        var data = new DbContextEventData(null!, (_, _) => string.Empty, context);
        interceptor.SavingChanges(data, default);
    }

    [Fact]
    public void An_added_row_without_a_user_is_rejected()
    {
        TrackedNewGrade();

        Assert.Throws<UnauthenticatedWriteException>(() => Save(new AnonymousCurrentUser()));
    }

    [Fact]
    public void A_changed_row_without_a_user_is_rejected()
    {
        var grade = TrackedNewGrade();
        context.Entry(grade).Property(nameof(JobGrade.Id)).CurrentValue = 7;
        context.Entry(grade).State = EntityState.Unchanged;
        context.Entry(grade).Property(nameof(JobGrade.Name)).CurrentValue = "Renamed";

        Assert.Throws<UnauthenticatedWriteException>(() => Save(new TestUser(userId: null)));
    }

    [Fact]
    public void A_signed_in_user_is_stamped_on_create()
    {
        var grade = TrackedNewGrade();

        Save(new TestUser(userId: 42));

        Assert.Equal(42, grade.CreatedByUserId);
        Assert.Equal(42, grade.ModifiedByUserId);
        Assert.Equal(Now.UtcDateTime, grade.CreatedAtUtc);
    }

    [Fact]
    public void The_system_identity_may_write_without_a_user_and_leaves_the_user_columns_null()
    {
        var grade = TrackedNewGrade();

        Save(new SystemCurrentUser());

        Assert.Null(grade.CreatedByUserId);
        Assert.Null(grade.ModifiedByUserId);
        Assert.Equal(Now.UtcDateTime, grade.CreatedAtUtc);
    }

    [Fact]
    public void Saving_nothing_needs_no_user() => Save(new AnonymousCurrentUser());

    [Fact]
    public void The_unauthorized_failure_has_a_message_without_data()
    {
        TrackedNewGrade();

        var exception = Assert.Throws<UnauthenticatedWriteException>(() => Save(new AnonymousCurrentUser()));

        Assert.Equal(UnauthenticatedWriteException.PublicMessage, exception.Message);
    }
}
