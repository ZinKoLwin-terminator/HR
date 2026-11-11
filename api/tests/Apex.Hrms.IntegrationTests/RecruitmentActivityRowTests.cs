using Apex.Hrms.IntegrationTests.Support;

namespace Apex.Hrms.IntegrationTests;

/// <summary>
/// Recruitment changes write their activity row in the same save, under the recruitment category. The actor is the
/// tagged test user, so the cleanup of <see cref="DatabaseTest"/> removes these rows with the rest.
/// </summary>
public sealed partial class RecruitmentTests
{
    private async Task<(string Category, string ActorName)> ReadActivityRowAsync(string message)
    {
        var count = await Sql.ScalarAsync<int>(
            "SELECT COUNT(*) FROM app.ActivityLog WHERE ActorName LIKE @tag AND Message = @message",
            ("@tag", Tag + "%"),
            ("@message", message));
        Assert.Equal(1, count);
        var category = await Sql.ScalarAsync<string>(
            "SELECT Category FROM app.ActivityLog WHERE ActorName LIKE @tag AND Message = @message",
            ("@tag", Tag + "%"),
            ("@message", message));
        return (category, Tag);
    }

    [IntegrationFact]
    public async Task Moving_a_stage_writes_one_recruitment_activity_row_and_a_candidate_row_uses_the_same_category()
    {
        var (manager, _, department) = await SeedPeopleAsync();
        var job = await SeedOpenJobAsync(department, manager, "Engineer");
        var candidate = await SeedCandidateAsync();
        var application = Succeeded(await Create(Requests.JobApplication(job.Id, candidate.Id)));

        Succeeded(await MoveStage(application, "Screening"));

        var label = $"{candidate.FirstName} {candidate.LastName} for {job.Title}";
        Assert.Equal("recruitment", (await ReadActivityRowAsync($"{label} moved to Screening")).Category);
        Assert.Equal("recruitment", (await ReadActivityRowAsync($"Candidate \"{candidate.FirstName} {candidate.LastName}\" created")).Category);
        Assert.Equal("recruitment", (await ReadActivityRowAsync($"Job opening \"{job.Code} {job.Title}\" set to Open")).Category);
    }
}
