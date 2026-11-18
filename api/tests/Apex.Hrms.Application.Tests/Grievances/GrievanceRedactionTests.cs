using Apex.Hrms.Application.Grievances;
using Apex.Hrms.Application.Tests.Fakes;
using Apex.Hrms.Domain.Grievances;
using Apex.Hrms.TestSupport;

namespace Apex.Hrms.Application.Tests.Grievances;

/// <summary>What each caller may read: the anonymous submitter is never on any object, the submitter never sees the case team's work.</summary>
public sealed class GrievanceRedactionTests : GrievanceTestBase
{
    private static PageRequest<GrievanceCaseFilter> All() => new(0, 50, null, null, new GrievanceCaseFilter());

    [Fact]
    public async Task An_anonymous_case_never_exposes_its_submitter_in_any_case_team_object()
    {
        await SeedAsync();
        var awaiting = await AwaitingAsync(anonymous: true);
        AsSam();
        Later();
        ResultAssert.Succeeded(await My.SendMessageAsync(awaiting.Id, new MyGrievanceMessageRequest(V(Version(awaiting.Id)), "TST The 3rd and the 10th.", null), default));
        Later();
        ResultAssert.Succeeded(await My.UploadAttachmentAsync(awaiting.Id, new GrievanceFileUpload(V(Version(awaiting.Id)), "rota.pdf", EvidenceSamples.Pdf()), default));

        foreach (var reader in new Action[] { AsHana, AsIvan })
        {
            reader();
            var detail = await TeamGetAsync(awaiting.Id);
            var history = ResultAssert.Succeeded(await Cases.HistoryAsync(awaiting.Id, default));
            var list = reader == AsHana
                ? ResultAssert.Succeeded(await Cases.QueryAsync(All(), default))
                : ResultAssert.Succeeded(await Cases.InvestigationsAsync(All(), default));

            Assert.Null(detail.Submitter);
            Assert.Equal("Anonymous", detail.SubmitterLabel);
            Assert.Null(detail.DepartmentId);
            Assert.Null(detail.DepartmentName);
            Assert.All(history.Where(entry => entry.ActorRole == "Submitter"), entry =>
            {
                Assert.Equal(GrievanceActor.AnonymousSubmitterName, entry.ActorName);
                Assert.Null(entry.ActorUserId);
                Assert.Null(entry.ActorEmployeeId);
            });
            Assert.Contains(detail.Messages, message => message.AuthorName == GrievanceActor.AnonymousSubmitterName);
            Assert.Equal("Anonymous", list.Items.Single().SubmitterLabel);
            foreach (var json in new[] { Json(detail), Json(history), Json(list) })
            {
                Assert.DoesNotContain("Sam", json, StringComparison.Ordinal);
                Assert.DoesNotContain("Submitterson", json, StringComparison.Ordinal);
                Assert.DoesNotContain("EMP-0001", json, StringComparison.Ordinal);
            }
        }

        // The stored rows carry no trace of Sam either.
        Assert.All(Host.Repository<GrievanceStatusHistoryEntry>().Items.Where(entry => entry.ActorRole == GrievanceActorRole.Submitter), entry =>
            Assert.True(entry.ActorUserId is null && entry.ActorEmployeeId is null && entry.ActorName == GrievanceActor.AnonymousSubmitterName));
        Assert.All(Host.Repository<GrievanceAttachment>().Items, file => Assert.Null(file.UploadedByUserId));
        Assert.All(Audit.Entries.Where(entry => entry.ActorRole == GrievanceActorRole.Submitter), entry => Assert.Null(entry.ActorUserId));
        Assert.Null(Submitters.Rows.Single(row => row.CaseId == awaiting.Id).EmployeeId);
    }

    [Fact]
    public async Task A_named_submitter_is_recorded_without_ids_and_shown_to_the_case_team_by_name()
    {
        await SeedAsync();
        var acknowledged = await AcknowledgedAsync();
        Assert.All(Host.Repository<GrievanceStatusHistoryEntry>().Items.Where(entry => entry.ActorRole == GrievanceActorRole.Submitter), entry =>
            Assert.True(entry.ActorUserId is null && entry.ActorEmployeeId is null && entry.ActorName == GrievanceActor.NamedSubmitterName));

        AsHana();
        var detail = await TeamGetAsync(acknowledged.Id);
        var submit = ResultAssert.Succeeded(await Cases.HistoryAsync(acknowledged.Id, default)).Single(entry => entry.Action == "Submit");

        Assert.Equal("Sam Submitterson", detail.Submitter!.DisplayName);
        Assert.Equal(W.Org.Department.Id, detail.Submitter.DepartmentId);
        Assert.Equal("Sam Submitterson", detail.SubmitterLabel);
        Assert.Equal("Sam Submitterson", submit.ActorName);
        Assert.Equal(W.Sam.Id, submit.ActorEmployeeId);
        Assert.Null(submit.ActorUserId);
    }

    [Fact]
    public async Task The_submitter_never_sees_notes_priority_team_names_or_internal_files()
    {
        await SeedAsync();
        var investigating = await UnderInvestigationAsync();
        AsIvan();
        await Ok(new AddGrievanceNoteCommand(investigating.Id, V(Version(investigating.Id)), "Finding", "TST Rota changed 9 times.", null, null));
        Later();
        ResultAssert.Succeeded(await Work.UploadAttachmentAsync(investigating.Id, "CaseTeam", new GrievanceFileUpload(V(Version(investigating.Id)), "internal.pdf", EvidenceSamples.Pdf()), default));
        Later();
        ResultAssert.Succeeded(await Work.UploadAttachmentAsync(investigating.Id, "Shared", new GrievanceFileUpload(V(Version(investigating.Id)), "letter.pdf", EvidenceSamples.Pdf()), default));
        await Ok(new SendGrievanceCaseMessageCommand(investigating.Id, V(Version(investigating.Id)), "TST We are looking into it."));

        AsSam();
        var mine = ResultAssert.Succeeded(await My.GetAsync(investigating.Id, default));
        var json = Json(mine);

        Assert.Equal(["letter.pdf"], mine.Attachments.Select(file => file.FileName));
        Assert.All(mine.Timeline.Where(entry => entry.Action != "Submit"), entry => Assert.Equal("HR team", entry.Actor));
        Assert.Equal("HR team", mine.Messages.Single().Actor);
        Assert.DoesNotContain("Ivan", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Hana", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Rota changed", json, StringComparison.Ordinal);
        Assert.DoesNotContain("priority", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("internal.pdf", json, StringComparison.Ordinal);
        Assert.Null(mine.ResolutionSummary);
        Assert.Null(mine.RejectionReason);
    }

    [Fact]
    public async Task The_resolution_reaches_the_submitter_only_when_made_visible()
    {
        await SeedAsync();
        var shown = await ResolvedAsync(visible: true);
        var hidden = await ResolvedAsync(visible: false);

        AsSam();
        Assert.Equal("TST Mediation and a fixed rota.", ResultAssert.Succeeded(await My.GetAsync(shown.Id, default)).ResolutionSummary);
        Assert.Null(ResultAssert.Succeeded(await My.GetAsync(hidden.Id, default)).ResolutionSummary);
    }

    [Fact]
    public async Task Each_tier_reads_only_its_sections()
    {
        await SeedAsync();
        var investigating = await UnderInvestigationAsync();
        AsIvan();
        await Ok(new AddGrievanceNoteCommand(investigating.Id, V(Version(investigating.Id)), "Interview", "TST Spoke to the supervisor.", Host.Clock.Today, "TST Supervisor"));
        await Ok(new AddGrievanceNoteCommand(investigating.Id, V(Version(investigating.Id)), "Finding", "TST The rota was changed unfairly.", null, null));
        await Ok(new SendGrievanceCaseMessageCommand(investigating.Id, V(Version(investigating.Id)), "TST Thank you for your patience."));
        Later();
        ResultAssert.Succeeded(await Work.UploadAttachmentAsync(investigating.Id, "CaseTeam", new GrievanceFileUpload(V(Version(investigating.Id)), "team.pdf", EvidenceSamples.Pdf()), default));
        AsHana();
        Later();
        ResultAssert.Succeeded(await Work.UploadAttachmentAsync(investigating.Id, "Restricted", new GrievanceFileUpload(V(Version(investigating.Id)), "restricted.pdf", EvidenceSamples.Pdf()), default));
        Later();
        ResultAssert.Succeeded(await Work.UploadAttachmentAsync(investigating.Id, "Shared", new GrievanceFileUpload(V(Version(investigating.Id)), "shared.pdf", EvidenceSamples.Pdf()), default));
        ResultAssert.Succeeded(await Work.AddCorrectiveActionAsync(investigating.Id, new GrievanceCorrectiveActionCreateRequest(V(Version(investigating.Id)), "Training", "TST Rota training for supervisors.", null, null), default));
        await Ok(new EscalateGrievanceCommand(investigating.Id, V(Version(investigating.Id)), "TST Needs review.", false));

        var management = await TeamGetAsync(investigating.Id);
        AsIvan();
        var investigator = await TeamGetAsync(investigating.Id);
        AsRita();
        var reviewer = await TeamGetAsync(investigating.Id);

        Assert.Equal(3, management.Attachments.Count);
        Assert.Single(management.CorrectiveActions);
        Assert.Equal(2, management.Notes.Count);

        Assert.Equal(["shared.pdf", "team.pdf"], investigator.Attachments.Select(file => file.FileName).Order());
        Assert.Empty(investigator.CorrectiveActions);
        Assert.Equal(2, investigator.Notes.Count);
        Assert.Single(investigator.Messages);

        Assert.Equal(["shared.pdf"], reviewer.Attachments.Select(file => file.FileName));
        Assert.Equal(["Finding"], reviewer.Notes.Select(note => note.Kind));
        Assert.Empty(reviewer.Messages);
        Assert.Empty(reviewer.CorrectiveActions);
        Assert.Equal(["Reviewer"], reviewer.ViewerRoles);

        var restricted = management.Attachments.Single(file => file.FileName == "restricted.pdf").Id;
        AsIvan();
        ResultAssert.Failed(await Cases.DownloadAttachmentAsync(restricted, default), Domain.Common.ErrorType.NotFound);
    }

    [Fact]
    public async Task A_reviewer_does_not_read_the_text_of_an_information_request()
    {
        await SeedAsync();
        var awaiting = await AwaitingAsync();
        AsHana();
        await Ok(new EscalateGrievanceCommand(awaiting.Id, V(Version(awaiting.Id)), "TST Urgent.", false));

        AsRita();
        var request = ResultAssert.Succeeded(await Cases.HistoryAsync(awaiting.Id, default)).Single(entry => entry.Action == "RequestInfo");

        Assert.Null(request.Reason);
    }
}
