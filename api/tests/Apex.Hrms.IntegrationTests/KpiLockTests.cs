using System.Diagnostics;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Performance.Periods;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.IntegrationTests.Support;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace Apex.Hrms.IntegrationTests;

/// <summary>
/// The application locks of the KPI module against SQL Server: the plan lock is per period (<c>kpi-plan:{periodId}</c>), so plan writes
/// of different periods never wait for each other while those of one period take turns; the period lock (<c>kpi-period:{companyId}</c>)
/// makes the overlap check of a create or update sound; a lock that stays busy for longer than the wait answers its own 409 message.
/// Another session holds a lock through a second connection, as a concurrent request would.
/// </summary>
[Collection(SqlServerFixtureDefinition.Name)]
[Trait("Category", "Integration")]
public sealed class KpiLockTests : DatabaseTest
{
    /// <summary>Well below the 15-second lock wait, well above the time a plan write takes when nothing holds its lock.</summary>
    private static readonly TimeSpan Unblocked = TimeSpan.FromSeconds(8);

    /// <summary>How long a write that waits for a held lock is watched before the lock is released.</summary>
    private static readonly TimeSpan Watched = TimeSpan.FromSeconds(2);

    private async Task<(int Employee, int Other, int Template, EvaluationPeriodDto Q4, EvaluationPeriodDto Q1, int CompanyId)> SeedAsync()
    {
        var org = await SeedOrganizationAsync();
        var category = await SeedKpiCategoryAsync();
        var template = await SeedKpiTemplateAsync(category.Id);
        var q4 = await SeedActivePeriodAsync(org.Company.Id);
        var q1 = await SeedActivePeriodAsync(org.Company.Id, "2027 Q1", start: "2027-01-01", end: "2027-03-31");
        var employee = await SeedEmployeeAsync(org, "1", "mia");
        var other = await SeedEmployeeAsync(org, "2", "noah");
        return (employee.Id, other.Id, template.Id, q4, q1, org.Company.Id);
    }

    [IntegrationFact]
    public async Task A_plan_write_for_another_period_does_not_wait_while_one_for_the_same_period_waits_its_turn()
    {
        var seed = await SeedAsync();
        await using var held = await HeldLock.TakeAsync($"kpi-plan:{seed.Q4.Id}");

        var clock = Stopwatch.StartNew();
        var elsewhere = await Assign(seed.Employee, seed.Q1.Id, ItemOf(seed.Template, weight: 100m));
        var elapsed = clock.Elapsed;
        var waiting = Assign(seed.Other, seed.Q4.Id, ItemOf(seed.Template, weight: 100m));
        var finishedWhileHeld = await Task.WhenAny(waiting, Task.Delay(Watched)) == waiting;
        await held.DisposeAsync();
        var afterRelease = await waiting;

        Assert.Equal(seed.Q1.Id, Succeeded(elsewhere).Scorecard.PeriodId);
        Assert.True(elapsed < Unblocked, $"The write for another period waited {elapsed}.");
        Assert.False(finishedWhileHeld, "The write for the busy period did not wait for its lock.");
        Assert.Equal(seed.Q4.Id, Succeeded(afterRelease).Scorecard.PeriodId);
    }

    [IntegrationFact]
    public async Task A_lock_that_stays_busy_answers_its_own_conflict_message_and_writes_nothing()
    {
        var seed = await SeedAsync();
        var custom = new ApplicationLock(Code("BUSY"), $"{Tag} is busy. Try again.");
        await using var plan = await HeldLock.TakeAsync($"kpi-plan:{seed.Q4.Id}");
        await using var other = await HeldLock.TakeAsync(custom.Name);

        // Both wait the full 15 seconds, side by side.
        var assign = Assign(seed.Employee, seed.Q4.Id, ItemOf(seed.Template, weight: 100m));
        var begin = InScopeAsync(async sp =>
        {
            var begun = await sp.GetRequiredService<IUnitOfWork>().BeginTransactionAsync(custom, default);
            if (begun.IsSuccess)
            {
                await begun.Value.DisposeAsync();
            }

            return begun.IsSuccess ? null : begun.Error;
        });
        await Task.WhenAll(assign, begin);

        Assert.Equal("Another change to KPI plans is in progress. Try again.", Failed(await assign, ErrorType.Conflict).Message);
        var error = await begin;
        Assert.NotNull(error);
        Assert.Equal((ErrorType.Conflict, custom.BusyMessage), (error.Type, error.Message));
        Assert.Equal(0, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM kpi.Scorecard WHERE PeriodId = @period", ("@period", seed.Q4.Id)));
    }

    [IntegrationFact]
    public async Task Two_overlapping_periods_created_at_the_same_moment_cannot_both_be_saved()
    {
        var company = await SeedCompanyAsync();

        var results = await Task.WhenAll(
            Create(Requests.Period(company.Id, "Race A")),
            Create(Requests.Period(company.Id, "Race B")));

        Assert.Single(results, result => result.IsSuccess);
        var refused = Assert.Single(results, result => result.IsFailure);
        Assert.Equal(ErrorType.Conflict, refused.Error.Type);
        Assert.Contains("The dates overlap period", refused.Error.Message, StringComparison.Ordinal);
        Assert.Equal(1, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM kpi.EvaluationPeriod WHERE CompanyId = @company", ("@company", company.Id)));
    }

    [IntegrationFact]
    public async Task A_period_create_waits_for_the_period_lock_of_its_company_only()
    {
        var company = await SeedCompanyAsync();
        var otherCompany = await SeedCompanyAsync("B", "Test Company B");
        await using var held = await HeldLock.TakeAsync($"kpi-period:{company.Id}");

        var clock = Stopwatch.StartNew();
        var elsewhere = await Create(Requests.Period(otherCompany.Id, "Other company"));
        var elapsed = clock.Elapsed;
        var waiting = Create(Requests.Period(company.Id));
        var finishedWhileHeld = await Task.WhenAny(waiting, Task.Delay(Watched)) == waiting;
        await held.DisposeAsync();
        var afterRelease = await waiting;

        Succeeded(elsewhere);
        Assert.True(elapsed < Unblocked, $"The create for another company waited {elapsed}.");
        Assert.False(finishedWhileHeld, "The create did not wait for the period lock of its company.");
        Assert.Equal("Draft", Succeeded(afterRelease).Status);
    }

    /// <summary>An exclusive application lock taken by another session (a second connection) until disposed.</summary>
    private sealed class HeldLock : IAsyncDisposable
    {
        private readonly SqlConnection connection;
        private readonly SqlTransaction transaction;
        private bool released;

        private HeldLock(SqlConnection connection, SqlTransaction transaction)
        {
            this.connection = connection;
            this.transaction = transaction;
        }

        public static async Task<HeldLock> TakeAsync(string resource)
        {
            var connection = new SqlConnection(SqlServerEnvironment.ConnectionString);
            await connection.OpenAsync();
            var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
            await using var command = new SqlCommand(
                "DECLARE @r int; EXEC @r = sys.sp_getapplock @Resource = @resource, @LockMode = N'Exclusive', @LockOwner = N'Transaction', @LockTimeout = 0; SELECT @r;",
                connection,
                transaction);
            command.Parameters.AddWithValue("@resource", resource);
            Assert.True((int)(await command.ExecuteScalarAsync())! >= 0, $"The test could not take the lock {resource}.");
            return new HeldLock(connection, transaction);
        }

        public async ValueTask DisposeAsync()
        {
            if (released)
            {
                return;
            }

            released = true;
            await transaction.RollbackAsync();
            await transaction.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
