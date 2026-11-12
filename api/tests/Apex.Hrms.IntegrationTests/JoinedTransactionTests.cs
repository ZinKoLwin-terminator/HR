using Apex.Hrms.Application;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Organization.Companies;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.IntegrationTests.Support;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace Apex.Hrms.IntegrationTests;

/// <summary>
/// <c>EfUnitOfWork.BeginTransactionAsync</c> when a transaction is already open on the context (the Excel import runs every
/// row inside one): the inner scope takes a savepoint, a dispose without commit rolls back to it and the outer transaction
/// decides about everything else. The outer scope and the inner one share the scoped unit of work, as the import does.
/// </summary>
[Collection(SqlServerFixtureDefinition.Name)]
[Trait("Category", "Integration")]
public sealed class JoinedTransactionTests : DatabaseTest
{
    private Task<int> CountCompaniesAsync(string code) =>
        Sql.ScalarAsync<int>("SELECT COUNT(*) FROM org.Company WHERE Code = @code", ("@code", Code(code)));

    [IntegrationFact]
    public async Task An_inner_scope_disposed_without_commit_rolls_back_to_its_savepoint_and_the_outer_commit_keeps_the_rest()
    {
        await InScopeAsync(async sp =>
        {
            var unitOfWork = sp.GetRequiredService<IUnitOfWork>();
            var create = sp.GetRequiredService<ICreateHandler<CompanyCreateRequest, CompanyDto>>();

            await using var outer = Succeeded(await unitOfWork.BeginTransactionAsync(null, default));
            Succeeded(await create.HandleAsync(Requests.Company("OUT", Name("Outer")), default));

            await using (var inner = Succeeded(await unitOfWork.BeginTransactionAsync(null, default)))
            {
                Succeeded(await create.HandleAsync(Requests.Company("IN", Name("Inner")), default));
            }

            Succeeded(await create.HandleAsync(Requests.Company("AFTER", Name("After")), default));
            await outer.CommitAsync(default);
            return 0;
        });

        Assert.Equal(1, await CountCompaniesAsync("OUT"));
        Assert.Equal(0, await CountCompaniesAsync("IN"));
        Assert.Equal(1, await CountCompaniesAsync("AFTER"));
    }

    [IntegrationFact]
    public async Task An_inner_scope_that_commits_leaves_the_decision_to_the_outer_transaction()
    {
        await InScopeAsync(async sp =>
        {
            var unitOfWork = sp.GetRequiredService<IUnitOfWork>();
            var create = sp.GetRequiredService<ICreateHandler<CompanyCreateRequest, CompanyDto>>();

            await using var outer = Succeeded(await unitOfWork.BeginTransactionAsync(null, default));
            await using (var inner = Succeeded(await unitOfWork.BeginTransactionAsync(null, default)))
            {
                Succeeded(await create.HandleAsync(Requests.Company("IN", Name("Inner")), default));
                await inner.CommitAsync(default);
            }

            // The outer scope is disposed without a commit: the inner "commit" must not have made anything permanent.
            return 0;
        });

        Assert.Equal(0, await CountCompaniesAsync("IN"));
    }

    [IntegrationFact]
    public async Task The_application_lock_taken_in_a_joined_scope_is_held_until_the_outer_transaction_ends()
    {
        var resource = Code("LOCK");
        await InScopeAsync(async sp =>
        {
            var unitOfWork = sp.GetRequiredService<IUnitOfWork>();
            await using var outer = Succeeded(await unitOfWork.BeginTransactionAsync(null, default));
            await using (var inner = Succeeded(await unitOfWork.BeginTransactionAsync(resource, default)))
            {
                await inner.CommitAsync(default);
            }

            Assert.False(await CanTakeLockAsync(resource), "The lock was released when the inner scope committed.");
            await outer.CommitAsync(default);
            return 0;
        });

        Assert.True(await CanTakeLockAsync(resource), "The lock stayed held after the outer transaction ended.");
    }

    /// <summary>Pins what SQL Server does with an application lock when its joined scope is disposed without a commit: rolling back to a savepoint releases the locks taken after it.</summary>
    [IntegrationFact]
    public async Task The_application_lock_of_a_joined_scope_that_rolls_back_to_its_savepoint_is_released()
    {
        var resource = Code("LOCKRB");
        var releasedAfterRollbackToSavepoint = await InScopeAsync(async sp =>
        {
            var unitOfWork = sp.GetRequiredService<IUnitOfWork>();
            await using var outer = Succeeded(await unitOfWork.BeginTransactionAsync(null, default));
            await using (Succeeded(await unitOfWork.BeginTransactionAsync(resource, default)))
            {
            }

            return await CanTakeLockAsync(resource);
        });

        // Observed behaviour (see the README, "Leavers and administrators"): the savepoint rollback releases the lock.
        Assert.True(releasedAfterRollbackToSavepoint);
        Assert.True(await CanTakeLockAsync(resource));
    }

    /// <summary>A second connection asks for the lock without waiting; false when somebody else holds it.</summary>
    private static async Task<bool> CanTakeLockAsync(string resource)
    {
        await using var connection = new SqlConnection(SqlServerEnvironment.ConnectionString);
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        await using var command = new SqlCommand(
            "DECLARE @r int; EXEC @r = sys.sp_getapplock @Resource = @resource, @LockMode = N'Exclusive', @LockOwner = N'Transaction', @LockTimeout = 0; SELECT @r;",
            connection,
            transaction);
        command.Parameters.AddWithValue("@resource", resource);
        return (int)(await command.ExecuteScalarAsync())! >= 0;
    }
}
