using Apex.Hrms.Application.Grievances;
using Apex.Hrms.Application.Tests.Fakes;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Grievances;
using Apex.Hrms.TestSupport;

namespace Apex.Hrms.Application.Tests.Grievances;

/// <summary>Who is told what, what the access log keeps, and the file rules.</summary>
public sealed class GrievanceNotificationAuditFileTests : GrievanceTestBase
{
    // ---- Notifications ----

    [Fact]
    public async Task A_named_submitter_hears_about_visible_steps_and_an_anonymous_one_never_does()
    {
        await SeedAsync();
        var named = await AcknowledgedAsync();
        var anonymous = await AcknowledgedAsync(anonymous: true);

        var toSam = Notifications.Sent.Where(sent => sent.UserId == SamUser).ToList();
        Assert.Single(toSam);
        Assert.Equal($"Grievance {named.CaseNo}", toSam[0].Content.Title);
        Assert.Equal($"/grievances/my/{named.Id}", toSam[0].Content.Link);
        Assert.DoesNotContain(Notifications.Sent, sent => sent.Content.Link == $"/grievances/my/{anonymous.Id}");
    }

    [Fact]
    public async Task The_investigator_hears_about_the_assignment_and_the_reviewers_about_a_proposal()
    {
        await SeedAsync();
        var proposed = await ProposedAsync();

        Assert.Contains(Notifications.Sent, sent => sent.UserId == IvanUser && sent.Content.Link == $"/grievances/investigations/{proposed.Id}");
        var reviewers = Notifications.Sent.Where(sent => sent.Content.Link == $"/grievances/reviews/{proposed.Id}").Select(sent => sent.UserId).Order();
        Assert.Equal([AdminUser, RitaUser], reviewers);
    }

    [Fact]
    public async Task The_subject_is_never_told_and_the_text_is_the_case_number_and_the_event_only()
    {
        await SeedAsync();
        var awaiting = await AwaitingAsync();
        AsHana();
        await Ok(new TriageGrievanceCommand(awaiting.Id, V(Version(awaiting.Id)), W.Category.Id, "Medium", Stored(awaiting.Id).TargetResolutionDate, W.Sid.Id, null, false));
        var before = Notifications.Sent.Count;
        AsSam();
        Later();
        ResultAssert.Succeeded(await My.SendMessageAsync(awaiting.Id, new MyGrievanceMessageRequest(V(Version(awaiting.Id)), "TST My answer.", null), default));

        // Management (Sid, the subject, holds management View and is left out) and the active investigator, who is waiting for it.
        var after = Notifications.Sent.Skip(before).ToList();
        Assert.Equal(new[] { AdminUser, HanaUser, IvanUser }.Order(), after.Select(sent => sent.UserId).Order());
        Assert.Equal($"/grievances/investigations/{awaiting.Id}", after.Single(sent => sent.UserId == IvanUser).Content.Link);
        Assert.Equal("Grievance " + Stored(awaiting.Id).CaseNo + ": the employee responded.", after.Single(sent => sent.UserId == IvanUser).Content.Message);
        Assert.All(Notifications.Sent, sent =>
        {
            Assert.DoesNotContain("TST", sent.Content.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("Harassment", sent.Content.Message, StringComparison.Ordinal);
            Assert.StartsWith("Grievance GRV-", sent.Content.Title, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task A_submitters_own_event_does_not_leave_the_submitter_out_of_the_management_notice()
    {
        // Leaving Hana out of "a grievance was submitted" would tell the other managers that she submitted it.
        await SeedAsync();
        AsHana();
        var draft = ResultAssert.Succeeded(await My.CreateDraftAsync(DraftRequest(W.Category.Id, anonymous: true), default));
        Later();
        var submitted = ResultAssert.Succeeded(await My.SubmitAsync(draft.Id, new SubmitGrievanceRequest(V(draft.RowVersion), true), default));

        Assert.Contains(Notifications.Sent, sent => sent.UserId == HanaUser && sent.Content.Title == $"Grievance {submitted.CaseNo}");
    }

    // ---- Access log ----

    [Fact]
    public async Task Case_team_reads_are_logged_and_a_submitters_own_reads_are_not()
    {
        await SeedAsync();
        var acknowledged = await AcknowledgedAsync(anonymous: true);
        var logged = Audit.Entries.Count;

        AsSam();
        ResultAssert.Succeeded(await My.GetAsync(acknowledged.Id, default));
        ResultAssert.Succeeded(await My.QueryAsync(new(0, 20, null, null, new MyGrievanceFilter()), default));
        Assert.Equal(logged, Audit.Entries.Count);

        AsHana();
        await TeamGetAsync(acknowledged.Id);
        ResultAssert.Succeeded(await Cases.HistoryAsync(acknowledged.Id, default));
        var reads = Audit.Entries.Skip(logged).ToList();
        Assert.Equal([GrievanceAuditAction.ViewCase, GrievanceAuditAction.ViewHistory], reads.Select(entry => entry.Action));
        Assert.All(reads, entry =>
        {
            Assert.Equal(HanaUser, entry.ActorUserId);
            Assert.Equal(GrievanceActorRole.Management, entry.ActorRole);
            Assert.Equal(acknowledged.Id, entry.CaseId);
        });

        var page = ResultAssert.Succeeded(await Cases.AuditAsync(acknowledged.Id, new(0, 50, null, null, new GrievanceAuditFilter()), default));
        Assert.Equal("ViewHistory", page.Items[0].Action);
        Assert.All(page.Items.Where(item => item.ActorRole == "Submitter"), item => Assert.Null(item.ActorUserId));
    }

    [Fact]
    public async Task Notes_messages_and_actions_are_logged_with_codes_only()
    {
        await SeedAsync();
        var investigating = await UnderInvestigationAsync();
        AsIvan();
        await Ok(new AddGrievanceNoteCommand(investigating.Id, V(Version(investigating.Id)), "Note", "TST private observation", null, null));
        await Ok(new SendGrievanceCaseMessageCommand(investigating.Id, V(Version(investigating.Id)), "TST we will call you"));
        AsHana();
        var action = ResultAssert.Succeeded(await Work.AddCorrectiveActionAsync(investigating.Id, new GrievanceCorrectiveActionCreateRequest(V(Version(investigating.Id)), "Training", "TST rota training", null, null), default));
        ResultAssert.Succeeded(await Work.UpdateCorrectiveActionAsync(action.Id, new GrievanceCorrectiveActionUpdateRequest(V(action.RowVersion), "Training", "TST rota training", null, null, Host.Clock.Today, "Implemented"), default));

        Assert.Contains(Audit.Entries, entry => entry.Action == GrievanceAuditAction.AddNote && entry.Detail == "kind:Note" && entry.ActorRole == GrievanceActorRole.Investigator);
        Assert.Contains(Audit.Entries, entry => entry.Action == GrievanceAuditAction.SendMessage && entry.Detail == "to-submitter");
        Assert.Contains(Audit.Entries, entry => entry.Action == GrievanceAuditAction.CorrectiveAction && entry.Detail == $"action:{action.Id};status:Implemented");
        Assert.All(Audit.Entries, entry => Assert.DoesNotContain("TST", entry.Detail ?? string.Empty, StringComparison.Ordinal));
        Assert.DoesNotContain(Host.ActivityLog.Messages, message => message.Contains("GRV-", StringComparison.Ordinal) || message.Contains("rota", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_note_does_not_change_the_case_row_version_but_a_team_message_does()
    {
        await SeedAsync();
        var investigating = await UnderInvestigationAsync();
        var before = Version(investigating.Id);
        AsIvan();

        await Ok(new AddGrievanceNoteCommand(investigating.Id, "AAAAAAAAAAA=", "Note", "TST note on an old version", null, null));
        Assert.True(before.AsSpan().SequenceEqual(Version(investigating.Id)));

        var messaged = await Ok(new SendGrievanceCaseMessageCommand(investigating.Id, V(before), "TST hello"));
        Assert.False(before.AsSpan().SequenceEqual(messaged.RowVersion));
    }

    // ---- Files ----

    [Fact]
    public async Task A_submitters_upload_is_shared_keeps_no_user_and_never_changes_the_case_row()
    {
        await SeedAsync();
        var investigating = await UnderInvestigationAsync();
        var before = Version(investigating.Id);
        AsSam();

        var uploaded = ResultAssert.Succeeded(await My.UploadAttachmentAsync(investigating.Id, new GrievanceFileUpload(V(before), "rota.docx", EvidenceSamples.Docx()), default));

        Assert.True(before.AsSpan().SequenceEqual(uploaded.CaseRowVersion));
        Assert.True(before.AsSpan().SequenceEqual(Version(investigating.Id)));
        var stored = Host.Repository<GrievanceAttachment>().Items.Single();
        Assert.Equal(GrievanceAttachmentVisibility.Shared, stored.Visibility);
        Assert.Null(stored.UploadedByUserId);
        Assert.Equal(GrievanceActorRole.Submitter, stored.UploadedByRole);
        Assert.Equal("You", uploaded.Attachment.UploadedBy);
        var download = ResultAssert.Succeeded(await My.DownloadAttachmentAsync(uploaded.Attachment.Id, default));
        Assert.Equal(EvidenceSamples.Docx().Length, (await download.ReadAllAsync()).Length);
    }

    [Fact]
    public async Task Files_are_checked_by_content_and_a_case_takes_at_most_ten()
    {
        await SeedAsync();
        var submitted = await SubmittedAsync();
        AsSam();
        var version = V(Version(submitted.Id));

        ResultAssert.Failed(await My.UploadAttachmentAsync(submitted.Id, new GrievanceFileUpload(version, "page.pdf", EvidenceSamples.Html()), default), ErrorType.Validation);
        ResultAssert.Failed(await My.UploadAttachmentAsync(submitted.Id, new GrievanceFileUpload(version, "big.pdf", EvidenceSamples.Pdf(10 * 1024 * 1024 + 1)), default), ErrorType.Validation);
        for (var index = 0; index < GrievanceAttachment.MaxSubmitterFilesPerCase; index++)
        {
            ResultAssert.Succeeded(await My.UploadAttachmentAsync(submitted.Id, new GrievanceFileUpload(version, $"file{index}.pdf", EvidenceSamples.Pdf()), default));
        }

        ResultAssert.Failed(await My.UploadAttachmentAsync(submitted.Id, new GrievanceFileUpload(version, "eleven.pdf", EvidenceSamples.Pdf()), default), ErrorType.Conflict, "You can add at most 10 files to a grievance.");
        Assert.Equal(GrievanceAttachment.MaxSubmitterFilesPerCase, Files.Files.Count);
        Assert.All(Host.UnitOfWork.Locks.Where(held => held is not null), held => Assert.Equal($"grv-files-{submitted.Id}", held!.Name));
    }

    [Fact]
    public async Task A_shared_team_file_moves_the_submitters_badge_and_only_management_adds_a_restricted_one()
    {
        await SeedAsync();
        var investigating = await UnderInvestigationAsync();
        AsSam();
        Later();
        Assert.True((await My.MarkSeenAsync(investigating.Id, default)).IsSuccess);

        AsIvan();
        Later();
        ResultAssert.Failed(await Work.UploadAttachmentAsync(investigating.Id, "Restricted", new GrievanceFileUpload(V(Version(investigating.Id)), "r.pdf", EvidenceSamples.Pdf()), default), ErrorType.Forbidden);
        ResultAssert.FailedOnField(await Work.UploadAttachmentAsync(investigating.Id, "Public", new GrievanceFileUpload(V(Version(investigating.Id)), "r.pdf", EvidenceSamples.Pdf()), default), "visibility");
        var shared = ResultAssert.Succeeded(await Work.UploadAttachmentAsync(investigating.Id, "Shared", new GrievanceFileUpload(V(Version(investigating.Id)), "letter.pdf", EvidenceSamples.Pdf()), default));

        Assert.True(shared.CaseRowVersion.AsSpan().SequenceEqual(Version(investigating.Id)));
        AsSam();
        Assert.True(ResultAssert.Succeeded(await My.GetAsync(investigating.Id, default)).HasUnreadUpdate);
    }

    [Fact]
    public async Task Only_a_shared_case_team_file_needs_the_current_row_version()
    {
        // A Shared file changes the case row (the submitter's unread badge), so its row version must be current; a CaseTeam or
        // Restricted file leaves the case row as it is, like a note, and an old row version only has to be well formed.
        await SeedAsync();
        var investigating = await UnderInvestigationAsync();
        var old = V(Version(investigating.Id));
        AsIvan();
        await Ok(new AddGrievanceNoteCommand(investigating.Id, old, "Note", "TST note", null, null));
        await Ok(new SendGrievanceCaseMessageCommand(investigating.Id, old, "TST hello"));
        Assert.NotEqual(old, V(Version(investigating.Id)));

        var internalFile = ResultAssert.Succeeded(await Work.UploadAttachmentAsync(investigating.Id, "CaseTeam", new GrievanceFileUpload(old, "internal.pdf", EvidenceSamples.Pdf()), default));
        ResultAssert.Failed(await Work.UploadAttachmentAsync(investigating.Id, "Shared", new GrievanceFileUpload(old, "letter.pdf", EvidenceSamples.Pdf()), default), ErrorType.Conflict);

        Assert.True(internalFile.CaseRowVersion.AsSpan().SequenceEqual(Version(investigating.Id)));
        Assert.Single(Host.Repository<GrievanceAttachment>().Items);
        Assert.Single(Files.Files);
    }

    [Fact]
    public async Task Downloads_by_the_case_team_are_logged_and_a_submitter_only_opens_shared_files()
    {
        await SeedAsync();
        var investigating = await UnderInvestigationAsync();
        AsIvan();
        Later();
        var internalFile = ResultAssert.Succeeded(await Work.UploadAttachmentAsync(investigating.Id, "CaseTeam", new GrievanceFileUpload(V(Version(investigating.Id)), "internal.pdf", EvidenceSamples.Pdf()), default));

        var download = ResultAssert.Succeeded(await Cases.DownloadAttachmentAsync(internalFile.Attachment.Id, default));
        await download.Content.DisposeAsync();
        Assert.Equal($"attachment:{internalFile.Attachment.Id}", Audit.Entries.Last().Detail);

        AsSam();
        ResultAssert.Failed(await My.DownloadAttachmentAsync(internalFile.Attachment.Id, default), ErrorType.NotFound);
        AsRita();
        ResultAssert.Failed(await Cases.DownloadAttachmentAsync(internalFile.Attachment.Id, default), ErrorType.NotFound);
    }

    [Fact]
    public async Task The_retention_years_come_from_the_settings()
    {
        using var shortRetention = new ShortRetention();
        await shortRetention.RunAsync();
    }

    private sealed class ShortRetention() : GrievanceTestBase(retentionYears: 3)
    {
        public async Task RunAsync()
        {
            await SeedAsync();
            var closed = await ClosedAsync();
            Assert.Equal(Host.Clock.Today.AddYears(3), closed.RetentionUntil);
        }
    }
}
