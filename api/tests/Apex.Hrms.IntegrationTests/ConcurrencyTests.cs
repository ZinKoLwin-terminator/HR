using Apex.Hrms.Application.Organization.Branches;
using Apex.Hrms.Application.Organization.Companies;
using Apex.Hrms.Application.Organization.Positions;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.IntegrationTests.Support;

namespace Apex.Hrms.IntegrationTests;

/// <summary>
/// Optimistic concurrency with the real SQL Server rowversion: a stale or missing token must never overwrite or
/// remove a record, and a successful change must hand out the token of the new version.
/// </summary>
[Collection(SqlServerFixtureDefinition.Name)]
[Trait("Category", "Integration")]
public sealed class ConcurrencyTests : DatabaseTest
{
    [IntegrationFact]
    public async Task A_successful_update_returns_a_new_row_version_that_matches_the_database()
    {
        var company = await SeedCompanyAsync();

        var updated = Succeeded(await Update(company.Id, Requests.CompanyUpdate(company, city: "Mandalay")));

        Assert.Equal(8, updated.RowVersion.Length);
        Assert.NotEqual(company.RowVersion, updated.RowVersion);
        Assert.Equal(updated.RowVersion, await Sql.ScalarAsync<byte[]>("SELECT RowVersion FROM org.Company WHERE Id = @id", ("@id", company.Id)));
        Assert.Equal(updated.RowVersion, Succeeded(await Get<CompanyDto>(company.Id)).RowVersion);
        Assert.Equal("Mandalay", updated.City);
    }

    [IntegrationFact]
    public async Task Updating_with_a_stale_row_version_is_a_conflict_and_changes_nothing()
    {
        var company = await SeedCompanyAsync();
        Succeeded(await Update(company.Id, Requests.CompanyUpdate(company, city: "Mandalay")));

        var result = await Update(company.Id, Requests.CompanyUpdate(company, city: "Bago"));

        Assert.Equal(RowVersionToken.Stale, Failed(result, ErrorType.Conflict));
        Assert.Equal("Mandalay", Succeeded(await Get<CompanyDto>(company.Id)).City);
    }

    [IntegrationFact]
    public async Task A_change_committed_by_another_session_after_the_load_is_a_conflict_in_the_database()
    {
        var company = await SeedCompanyAsync();
        BeforeNextSave = () => Sql.ExecuteAsync("UPDATE org.Company SET Industry = N'Changed elsewhere' WHERE Id = @id", ("@id", company.Id));

        var result = await Update(company.Id, Requests.CompanyUpdate(company, city: "Mandalay"));

        Assert.Equal(RowVersionToken.Stale, Failed(result, ErrorType.Conflict));
        Assert.Equal("Yangon", Succeeded(await Get<CompanyDto>(company.Id)).City);
    }

    [IntegrationFact]
    public async Task Deleting_with_a_stale_row_version_is_a_conflict_and_keeps_the_record()
    {
        var company = await SeedCompanyAsync();
        var updated = Succeeded(await Update(company.Id, Requests.CompanyUpdate(company, city: "Mandalay")));

        var result = await Delete<CompanyDto>(company.Id, Requests.Version(company.RowVersion));

        Assert.Equal(RowVersionToken.Stale, Failed(result, ErrorType.Conflict));
        Assert.Equal(1, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM org.Company WHERE Id = @id", ("@id", company.Id)));
        Assert.True((await Delete<CompanyDto>(company.Id, Requests.Version(updated.RowVersion))).IsSuccess);
    }

    [IntegrationFact]
    public async Task Deleting_a_record_another_session_already_removed_is_a_conflict()
    {
        var company = await SeedCompanyAsync();
        BeforeNextSave = () => Sql.ExecuteAsync("DELETE FROM org.Company WHERE Id = @id", ("@id", company.Id));

        var result = await Delete<CompanyDto>(company.Id, Requests.Version(company.RowVersion));

        Assert.Equal(RowVersionToken.Stale, Failed(result, ErrorType.Conflict));
    }

    [IntegrationFact]
    public async Task Activating_and_deactivating_with_a_stale_row_version_are_conflicts()
    {
        var company = await SeedCompanyAsync();
        var deactivated = Succeeded(await Deactivate<CompanyDto>(company.Id, Requests.Version(company.RowVersion)));

        var staleActivate = await Activate<CompanyDto>(company.Id, Requests.Version(company.RowVersion));
        var staleDeactivate = await Deactivate<CompanyDto>(company.Id, Requests.Version(company.RowVersion));

        Assert.Equal(RowVersionToken.Stale, Failed(staleActivate, ErrorType.Conflict));
        Assert.Equal(RowVersionToken.Stale, Failed(staleDeactivate, ErrorType.Conflict));
        Assert.Equal("Inactive", Succeeded(await Get<CompanyDto>(company.Id)).Status);
        Assert.Equal(deactivated.RowVersion, Succeeded(await Get<CompanyDto>(company.Id)).RowVersion);
    }

    [IntegrationFact]
    public async Task A_status_change_committed_by_another_session_after_the_load_is_a_conflict_in_the_database()
    {
        var company = await SeedCompanyAsync();
        BeforeNextSave = () => Sql.ExecuteAsync("UPDATE org.Company SET Industry = N'Changed elsewhere' WHERE Id = @id", ("@id", company.Id));

        var result = await Deactivate<CompanyDto>(company.Id, Requests.Version(company.RowVersion));

        Assert.Equal(RowVersionToken.Stale, Failed(result, ErrorType.Conflict));
        Assert.Equal("Active", Succeeded(await Get<CompanyDto>(company.Id)).Status);
    }

    [IntegrationFact]
    public async Task A_missing_row_version_on_update_is_a_validation_error_on_rowVersion()
    {
        var company = await SeedCompanyAsync();

        var result = await Update(company.Id, Requests.CompanyUpdate(company, rowVersion: ""));

        FailedOnField(result, RowVersionToken.Field);
    }

    [IntegrationFact]
    public async Task A_missing_row_version_on_delete_is_a_validation_error_on_rowVersion()
    {
        var company = await SeedCompanyAsync();

        FailedOnField(await Delete<CompanyDto>(company.Id, null), RowVersionToken.Field);
        FailedOnField(await Delete<CompanyDto>(company.Id, "  "), RowVersionToken.Field);
        Assert.Equal(1, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM org.Company WHERE Id = @id", ("@id", company.Id)));
    }

    [IntegrationFact]
    public async Task A_missing_row_version_on_activate_and_deactivate_is_a_validation_error_on_rowVersion()
    {
        var company = await SeedCompanyAsync();

        FailedOnField(await Activate<CompanyDto>(company.Id, null), RowVersionToken.Field);
        FailedOnField(await Deactivate<CompanyDto>(company.Id, null), RowVersionToken.Field);
    }

    [IntegrationFact]
    public async Task A_malformed_row_version_is_a_validation_error_on_rowVersion()
    {
        var company = await SeedCompanyAsync();

        FailedOnField(await Delete<CompanyDto>(company.Id, "not-base64!"), RowVersionToken.Field);
        FailedOnField(await Delete<CompanyDto>(company.Id, Convert.ToBase64String(new byte[3])), RowVersionToken.Field);
    }

    [IntegrationFact]
    public async Task The_row_version_of_another_record_is_not_accepted()
    {
        var first = await SeedCompanyAsync("A", "Test Company A");
        var second = await SeedCompanyAsync("B", "Test Company B");
        Succeeded(await Update(second.Id, Requests.CompanyUpdate(second, city: "Mandalay")));

        var result = await Delete<CompanyDto>(first.Id, Requests.Version(second.RowVersion));

        Assert.Equal(RowVersionToken.Stale, Failed(result, ErrorType.Conflict));
    }

    [IntegrationFact]
    public async Task Every_write_on_a_missing_record_is_not_found()
    {
        const int missing = 999_999;
        var version = Convert.ToBase64String(new byte[8]);

        Failed(await Delete<CompanyDto>(missing, version), ErrorType.NotFound);
        Failed(await Activate<BranchDto>(missing, version), ErrorType.NotFound);
        Failed(await Deactivate<PositionDto>(missing, version), ErrorType.NotFound);
        Failed(await Get<CompanyDto>(missing), ErrorType.NotFound);
    }
}
