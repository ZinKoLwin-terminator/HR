using Apex.Hrms.Application.Grievances;
using Apex.Hrms.Application.Tests.Fakes;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Grievances;

namespace Apex.Hrms.Application.Tests.Grievances;

/// <summary>The step pipeline (400, 404, 403, 409, in that order), assignment conflicts, the submitter's messages and the read receipt.</summary>
public sealed class GrievanceCommandTests : GrievanceTestBase
{
    [Fact]
    public async Task A_malformed_row_version_is_a_400_before_anything_else()
    {
        await SeedAsync();
        AsIvan();

        ResultAssert.FailedOnField(await Run(new AcknowledgeGrievanceCommand(999, "not-a-version", "High", null, null)), "rowVersion");
        ResultAssert.FailedOnField(await Run(new AcknowledgeGrievanceCommand(999, null, "High", null, null)), "rowVersion");
    }

    [Fact]
    public async Task A_case_outside_the_scope_is_a_404_and_a_wrong_capacity_a_403_that_is_logged()
    {
        await SeedAsync();
        var assigned = await AssignedAsync();

        AsOlga();
        ResultAssert.Failed(await Run(new StartGrievanceInvestigationCommand(assigned.Id, V(Version(assigned.Id)), null)), ErrorType.NotFound);

        AsIvan();
        ResultAssert.Failed(await Run(new RejectGrievanceCommand(assigned.Id, V(Version(assigned.Id)), "TST no")), ErrorType.Forbidden);
        var refused = Audit.Entries.Last();
        Assert.Equal(GrievanceAuditAction.AccessRefused, refused.Action);
        Assert.Equal(IvanUser, refused.ActorUserId);
        Assert.Equal(GrievanceStatus.Assigned, Stored(assigned.Id).Status);
    }

    [Fact]
    public async Task A_stale_row_version_is_a_409_and_a_wrong_status_too()
    {
        await SeedAsync();
        var acknowledged = await AcknowledgedAsync();
        var old = V(Version(acknowledged.Id));
        AsHana();
        await Ok(new StartGrievanceReviewCommand(acknowledged.Id, old, "TST taking it"));

        ResultAssert.Failed(await Run(new StartGrievanceReviewCommand(acknowledged.Id, old, null)), ErrorType.Conflict);
        ResultAssert.Failed(await Run(new StartGrievanceInvestigationCommand(acknowledged.Id, V(Version(acknowledged.Id)), null)), ErrorType.Conflict);
        ResultAssert.FailedOnField(await Run(new RejectGrievanceCommand(acknowledged.Id, V(Version(acknowledged.Id)), "  ")), "reason");
    }

    [Fact]
    public async Task Every_step_answers_with_a_new_row_version_and_writes_its_history_and_a_status_change_entry()
    {
        await SeedAsync();
        var submitted = await SubmittedAsync();
        var before = Version(submitted.Id);
        AsHana();

        var acknowledged = await Ok(new AcknowledgeGrievanceCommand(submitted.Id, V(before), "High", null, "TST internal"));

        Assert.False(before.AsSpan().SequenceEqual(acknowledged.RowVersion));
        Assert.Equal("Acknowledged", acknowledged.Status);
        Assert.Equal(Host.Clock.Today.AddDays(14), acknowledged.TargetResolutionDate);
        var entry = Audit.Entries.Last();
        Assert.Equal(GrievanceAuditAction.StatusChange, entry.Action);
        Assert.Equal("Submitted>Acknowledged", entry.Detail);
        Assert.Equal(GrievanceActorRole.Management, entry.ActorRole);
    }

    // ---- Assignment and the conflict of interest ----

    [Fact]
    public async Task Assigning_the_subject_or_the_named_submitter_is_refused_with_one_generic_message()
    {
        // The rule (see GrievanceAccess): the subject is refused on every case, the submitter of a NAMED case too, with the same
        // Validation error on investigatorEmployeeId that never says which applied; the refusal is logged as AccessRefused.
        await SeedAsync();
        var acknowledged = await AcknowledgedAsync();
        AsHana();
        await Ok(new TriageGrievanceCommand(acknowledged.Id, V(Version(acknowledged.Id)), W.Category.Id, "Medium", acknowledged.TargetResolutionDate, W.Sid.Id, null, false));

        var subject = ResultAssert.FailedOnField(await Run(new AssignGrievanceInvestigatorCommand(acknowledged.Id, V(Version(acknowledged.Id)), W.Sid.Id, "Lead", null)), "investigatorEmployeeId");
        var submitter = ResultAssert.FailedOnField(await Run(new AssignGrievanceInvestigatorCommand(acknowledged.Id, V(Version(acknowledged.Id)), W.Sam.Id, "Lead", null)), "investigatorEmployeeId");

        Assert.Equal(GrievanceCase.ConflictOfInterestMessage, subject.FieldErrors!["investigatorEmployeeId"][0]);
        Assert.Equal(subject.FieldErrors["investigatorEmployeeId"], submitter.FieldErrors!["investigatorEmployeeId"]);
        Assert.Equal(subject.Message, submitter.Message);
        Assert.Equal(2, Audit.Entries.Count(entry => entry.Action == GrievanceAuditAction.AccessRefused && entry.Detail == "assign:conflict"));
        Assert.Equal(GrievanceStatus.Acknowledged, Stored(acknowledged.Id).Status);
    }

    [Fact]
    public async Task Assigning_the_anonymous_submitter_is_not_refused_and_they_simply_cannot_open_the_case()
    {
        // No oracle: the assignment succeeds like any other; the case-team scope then hides the case from its own submitter (404).
        await SeedAsync();
        var acknowledged = await AcknowledgedAsync(anonymous: true);
        AsHana();

        var assigned = await Ok(new AssignGrievanceInvestigatorCommand(acknowledged.Id, V(Version(acknowledged.Id)), W.Sam.Id, "Lead", null));

        Assert.Equal("Assigned", assigned.Status);
        Assert.DoesNotContain(Audit.Entries, entry => entry.Action == GrievanceAuditAction.AccessRefused);
        As(W.Sam, SamUser, "Sam Submitterson", [.. SelfGrants, .. InvestigationGrants]);
        ResultAssert.Failed(await Cases.GetAsync(acknowledged.Id, default), ErrorType.NotFound);
        Assert.Empty(ResultAssert.Succeeded(await Cases.InvestigationsAsync(new(0, 20, null, null, new GrievanceCaseFilter()), default)).Items);
    }

    [Fact]
    public async Task Only_an_employee_who_can_investigate_is_assigned_and_never_twice()
    {
        await SeedAsync();
        var acknowledged = await AcknowledgedAsync();
        AsHana();

        ResultAssert.FailedOnField(await Run(new AssignGrievanceInvestigatorCommand(acknowledged.Id, V(Version(acknowledged.Id)), W.Rita.Id, "Lead", null)), "investigatorEmployeeId");
        ResultAssert.FailedOnField(await Run(new AssignGrievanceInvestigatorCommand(acknowledged.Id, V(Version(acknowledged.Id)), W.Ivan.Id, "Boss", null)), "role");
        var assigned = await Ok(new AssignGrievanceInvestigatorCommand(acknowledged.Id, V(Version(acknowledged.Id)), W.Ivan.Id, "Lead", null));
        ResultAssert.Failed(await Run(new AssignGrievanceInvestigatorCommand(assigned.Id, V(assigned.RowVersion), W.Ivan.Id, "Member", null)), ErrorType.Conflict);
        var assign = Audit.Entries.Last(entry => entry.Action == GrievanceAuditAction.Assign);
        Assert.Equal($"employee:{W.Ivan.Id};role:Lead;Acknowledged>Assigned", assign.Detail);
    }

    [Fact]
    public async Task Only_the_lead_proposes_while_under_investigation_and_the_proposer_never_decides()
    {
        await SeedAsync();
        var assigned = await AssignedAsync();
        AsHana();
        var withMember = await Ok(new AssignGrievanceInvestigatorCommand(assigned.Id, V(Version(assigned.Id)), W.Olga.Id, "Member", null));
        AsIvan();
        var investigating = await Ok(new StartGrievanceInvestigationCommand(withMember.Id, V(Version(withMember.Id)), null));

        AsOlga();
        ResultAssert.Failed(await Run(new ProposeGrievanceResolutionCommand(investigating.Id, V(Version(investigating.Id)), "TST")), ErrorType.Forbidden);
        AsHana();
        ResultAssert.Failed(await Run(new ProposeGrievanceResolutionCommand(investigating.Id, V(Version(investigating.Id)), "TST")), ErrorType.Forbidden);

        // Ivan both proposes and (with review grants) tries to approve: the domain refuses deciding one's own proposal.
        As(W.Ivan, IvanUser, "Ivan Investigator", [.. SelfGrants, .. InvestigationGrants, .. ReviewGrants]);
        var proposed = await Ok(new ProposeGrievanceResolutionCommand(investigating.Id, V(Version(investigating.Id)), "TST Fix the rota."));
        ResultAssert.Failed(await Run(new ApproveGrievanceResolutionCommand(proposed.Id, V(Version(proposed.Id)), true, null)), ErrorType.Forbidden);
    }

    [Fact]
    public async Task Reopening_a_closed_case_needs_management_edit_and_review_approve()
    {
        await SeedAsync();
        var closed = await ClosedAsync();

        AsHana();
        ResultAssert.Failed(await Run(new ReopenGrievanceCommand(closed.Id, V(Version(closed.Id)), "TST new facts")), ErrorType.Forbidden);

        // The actor of the golden case "reopen: a closed case is reopened with a reason": management Edit AND review Approve.
        As(W.Hana, HanaUser, "Hana Manager", [.. SelfGrants, .. ManagementGrants, .. ReviewGrants]);
        var reopened = await Ok(new ReopenGrievanceCommand(closed.Id, V(Version(closed.Id)), "TST new facts"));

        Assert.Equal("Reopened", reopened.Status);
        Assert.Null(reopened.RetentionUntil);
    }

    // ---- Triage, retention, legal hold ----

    [Fact]
    public async Task Closing_and_rejecting_set_the_retention_date_and_a_legal_hold_blocks_the_archive()
    {
        await SeedAsync();
        var closed = await ClosedAsync();
        var submitted = await SubmittedAsync();
        AsHana();
        var rejected = await Ok(new RejectGrievanceCommand(submitted.Id, V(Version(submitted.Id)), "TST outside policy"));

        Assert.Equal(Host.Clock.Today.AddYears(7), closed.RetentionUntil);
        Assert.Equal(Host.Clock.Today.AddYears(7), rejected.RetentionUntil);

        var held = await Ok(new TriageGrievanceCommand(closed.Id, V(Version(closed.Id)), closed.CategoryId, closed.Priority, closed.TargetResolutionDate, null, null, true));
        Assert.True(held.LegalHold);
        ResultAssert.Failed(await Run(new ArchiveGrievanceCommand(closed.Id, V(Version(closed.Id)), null)), ErrorType.Conflict);
        ResultAssert.Failed(
            await Run(new TriageGrievanceCommand(closed.Id, V(Version(closed.Id)), closed.CategoryId, "Low", closed.TargetResolutionDate, null, null, true)),
            ErrorType.Conflict,
            "This case is no longer open: only its legal hold can change.");
        Assert.Contains(Audit.Entries, entry => entry.Action == GrievanceAuditAction.Triage && entry.Detail == "legalHold:0>1");
    }

    [Fact]
    public async Task Triage_to_critical_escalates_at_once_and_alerts_the_reviewers()
    {
        await SeedAsync();
        var acknowledged = await AcknowledgedAsync();
        AsHana();

        var triaged = await Ok(new TriageGrievanceCommand(acknowledged.Id, V(Version(acknowledged.Id)), W.Category.Id, "Critical", acknowledged.TargetResolutionDate, null, null, false));

        Assert.True(triaged.IsEscalated);
        Assert.True(triaged.Escalations.Single().IsImmediate);
        Assert.Equal(GrievanceCase.CriticalTriageEscalationReason, triaged.Escalations.Single().Reason);
        Assert.Contains(Notifications.Sent, sent => sent.UserId == RitaUser && sent.Content.Link == $"/grievances/reviews/{acknowledged.Id}");
        Assert.Contains(Audit.Entries, entry => entry.Action == GrievanceAuditAction.Triage && entry.Detail == "priority:Medium>Critical");
    }

    [Fact]
    public async Task An_anonymous_case_cannot_move_to_a_category_that_refuses_anonymity()
    {
        await SeedAsync();
        var acknowledged = await AcknowledgedAsync(anonymous: true);
        AsHana();

        ResultAssert.FailedOnField(
            await Run(new TriageGrievanceCommand(acknowledged.Id, V(Version(acknowledged.Id)), W.NamedOnly.Id, "Medium", acknowledged.TargetResolutionDate, null, null, false)),
            "categoryId");
    }

    // ---- The submitter's messages and read receipt ----

    [Fact]
    public async Task A_message_while_awaiting_the_submitter_is_the_response_and_returns_the_case()
    {
        await SeedAsync();
        var awaiting = await AwaitingAsync();
        var before = Version(awaiting.Id);
        AsSam();
        Later();

        ResultAssert.Failed(await My.SendMessageAsync(awaiting.Id, new MyGrievanceMessageRequest(V(Host.Repository<GrievanceCategory>().Items[0].RowVersion), "TST answer", null), default), ErrorType.Conflict);
        var answered = ResultAssert.Succeeded(await My.SendMessageAsync(awaiting.Id, new MyGrievanceMessageRequest(V(before), "TST The 3rd and the 10th.", null), default));

        Assert.Equal("UnderInvestigation", answered.Status);
        Assert.False(before.AsSpan().SequenceEqual(answered.RowVersion));
        var response = answered.Messages[^1];
        Assert.Equal("InfoResponse", response.Kind);
        Assert.Equal(answered.Messages.Single(message => message.Kind == "InfoRequest").Id, response.InReplyToId);
        Assert.Contains(Notifications.Sent, sent => sent.UserId == HanaUser && sent.Content.Message.Contains("responded", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_plain_message_adds_a_row_and_leaves_the_case_and_its_row_version_alone()
    {
        await SeedAsync();
        var investigating = await UnderInvestigationAsync(anonymous: true);
        var before = Version(investigating.Id);
        var status = Stored(investigating.Id).Status;
        AsSam();

        var sent = ResultAssert.Succeeded(await My.SendMessageAsync(investigating.Id, new MyGrievanceMessageRequest(V(Host.Repository<GrievanceCategory>().Items[0].RowVersion), "TST One more thing.", null), default));

        Assert.True(before.AsSpan().SequenceEqual(sent.RowVersion));
        Assert.Equal(status, Stored(investigating.Id).Status);
        Assert.Equal("Message", sent.Messages.Single().Kind);
        var logged = Audit.Entries.Last();
        Assert.Equal(GrievanceAuditAction.SendMessage, logged.Action);
        Assert.Null(logged.ActorUserId);
        Assert.Equal(GrievanceActorRole.Submitter, logged.ActorRole);
    }

    [Fact]
    public async Task Marking_seen_clears_the_badge_without_touching_the_case()
    {
        await SeedAsync();
        var acknowledged = await AcknowledgedAsync(anonymous: true);
        var before = Version(acknowledged.Id);
        AsSam();
        Assert.True(ResultAssert.Succeeded(await My.GetAsync(acknowledged.Id, default)).HasUnreadUpdate);
        Later();

        Assert.True((await My.MarkSeenAsync(acknowledged.Id, default)).IsSuccess);

        Assert.True(before.AsSpan().SequenceEqual(Version(acknowledged.Id)));
        Assert.False(ResultAssert.Succeeded(await My.GetAsync(acknowledged.Id, default)).HasUnreadUpdate);
        Assert.Equal(0, ResultAssert.Succeeded(await My.DashboardAsync(default)).UnreadUpdates);
        AsOlga();
        ResultAssert.Failed(await My.MarkSeenAsync(acknowledged.Id, default), ErrorType.NotFound);
    }

    // ---- Drafts ----

    [Fact]
    public async Task A_draft_may_be_unfinished_keeps_its_anonymity_and_submitting_needs_the_minimums()
    {
        await SeedAsync();
        AsSam();
        var draft = ResultAssert.Succeeded(await My.CreateDraftAsync(DraftRequest(W.Category.Id, anonymous: true) with { Description = null, Subject = "Shif" }, default));
        Later();

        var named = await My.UpdateDraftAsync(draft.Id, new GrievanceDraftUpdateRequest(V(draft.RowVersion), W.Category.Id, "Shif", null, null, null, null, null, false), default);
        Assert.Equal("Start a new draft to change how you report.", ResultAssert.FailedOnField(named, "isAnonymous").FieldErrors!["isAnonymous"][0]);
        var toNamedOnly = await My.UpdateDraftAsync(draft.Id, new GrievanceDraftUpdateRequest(V(draft.RowVersion), W.NamedOnly.Id, "Shif", null, null, null, null, null, true), default);
        ResultAssert.FailedOnField(toNamedOnly, "isAnonymous");
        var link = Submitters.Rows.Single(row => row.CaseId == draft.Id);
        Assert.Null(link.EmployeeId);
        Assert.NotNull(link.SubmitterPseudonym);

        var refused = ResultAssert.Failed(await My.SubmitAsync(draft.Id, new SubmitGrievanceRequest(V(draft.RowVersion), true), default), ErrorType.Validation);
        Assert.Equal(["description", "subject"], refused.FieldErrors!.Keys.Order(StringComparer.Ordinal));
        ResultAssert.FailedOnField(await My.SubmitAsync(draft.Id, new SubmitGrievanceRequest(V(draft.RowVersion), false), default), "declarationAccepted");
        Assert.Empty(Audit.Entries);
    }

    [Fact]
    public async Task An_employee_keeps_at_most_five_unfinished_drafts()
    {
        await SeedAsync();
        for (var index = 0; index < 5; index++)
        {
            await DraftAsync(anonymous: index % 2 == 0);
        }

        AsSam();
        var refused = ResultAssert.Failed(await My.CreateDraftAsync(DraftRequest(W.Category.Id), default), ErrorType.Validation);

        Assert.Equal("You already have 5 unfinished drafts. Submit or delete one before you start another.", refused.Message);
        AsOlga();
        ResultAssert.Succeeded(await My.CreateDraftAsync(DraftRequest(W.Category.Id), default));
    }

    [Fact]
    public async Task A_decision_needs_the_reviewer_tier_and_never_an_investigator_of_the_case()
    {
        await SeedAsync();
        var proposed = await ProposedAsync();

        // Olga is a member of the case team and also holds the review grants: an investigator of the case never decides it.
        AsHana();
        await Ok(new AssignGrievanceInvestigatorCommand(proposed.Id, V(Version(proposed.Id)), W.Olga.Id, "Member", null));
        As(W.Olga, OlgaUser, "Olga Other", [.. SelfGrants, .. InvestigationGrants, .. ReviewGrants]);
        var investigator = ResultAssert.Failed(await Run(new ApproveGrievanceResolutionCommand(proposed.Id, V(Version(proposed.Id)), true, null)), ErrorType.Forbidden);
        Assert.Equal("An investigator on this case cannot decide its resolution.", investigator.Message);

        // Hana holds review Approve but not review View: she sees the case as management, not as a reviewer.
        As(W.Hana, HanaUser, "Hana Manager", [.. SelfGrants, .. ManagementGrants, "grievance-review:Approve"]);
        ResultAssert.Failed(await Run(new RejectGrievanceResolutionCommand(proposed.Id, V(Version(proposed.Id)), "TST no")), ErrorType.Forbidden);

        AsRita();
        ResultAssert.Succeeded(await Run(new ApproveGrievanceResolutionCommand(proposed.Id, V(Version(proposed.Id)), true, null)));
    }

    [Fact]
    public async Task Submitting_numbers_the_case_copies_a_named_placement_and_tells_management()
    {
        await SeedAsync();
        var named = await SubmittedAsync();
        var anonymous = await SubmittedAsync(anonymous: true);

        Assert.Equal("GRV-2026-00001", named.CaseNo);
        Assert.Equal("GRV-2026-00002", anonymous.CaseNo);
        Assert.Equal(W.Org.Department.Id, Stored(named.Id).DepartmentId);
        Assert.Null(Stored(anonymous.Id).DepartmentId);
        var told = Notifications.Sent.Where(sent => sent.Content.Title == "Grievance GRV-2026-00002").Select(sent => sent.UserId).Order().ToList();
        Assert.Equal(new[] { AdminUser, HanaUser, SidUser }.Order(), told);
        Assert.All(Notifications.Sent, sent => Assert.DoesNotContain("TST", sent.Content.Message, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Deleting_a_draft_removes_it_and_its_files_and_leaves_no_log()
    {
        await SeedAsync();
        var draft = await DraftAsync();
        Later();
        var uploaded = ResultAssert.Succeeded(await My.UploadAttachmentAsync(draft.Id, new GrievanceFileUpload(V(draft.RowVersion), "photo.png", TestSupport.EvidenceSamples.Png()), default));
        Assert.True(draft.RowVersion.AsSpan().SequenceEqual(uploaded.CaseRowVersion));

        Assert.True((await My.DeleteDraftAsync(draft.Id, V(draft.RowVersion), default)).IsSuccess);

        Assert.DoesNotContain(Host.Repository<GrievanceCase>().Items, grievance => grievance.Id == draft.Id);
        Assert.Empty(Files.Files);
        Assert.Empty(Audit.Entries);
        Assert.Equal($"grv-files-{draft.Id}", Host.UnitOfWork.Locks[^1]!.Name);
        var submitted = await SubmittedAsync();
        ResultAssert.Failed(await My.DeleteDraftAsync(submitted.Id, V(Version(submitted.Id)), default), ErrorType.Conflict);
    }

    [Fact]
    public async Task A_draft_that_cannot_be_submitted_is_refused_before_it_takes_a_case_number()
    {
        await SeedAsync();
        AsSam();
        var thin = ResultAssert.Succeeded(await My.CreateDraftAsync(
            new GrievanceDraftCreateRequest(W.Category.Id, "TST Repeated unfair shift changes", "TST", null, null, null, null, false), default));
        Later();

        ResultAssert.Failed(await My.SubmitAsync(thin.Id, new SubmitGrievanceRequest(V(thin.RowVersion), true), default), ErrorType.Validation);
        ResultAssert.FailedOnField(await My.SubmitAsync(thin.Id, new SubmitGrievanceRequest(V(thin.RowVersion), false), default), "declarationAccepted");

        Assert.Equal("GRV-2026-00001", (await SubmittedAsync()).CaseNo);
    }

    // ---- Final review: escalation, reopening, triage conflicts ----

    [Fact]
    public async Task A_case_escalated_before_acknowledgement_is_not_escalated_again_when_acknowledged_as_critical()
    {
        await SeedAsync();
        var submitted = await SubmittedAsync();
        AsHana();
        await Ok(new EscalateGrievanceCommand(submitted.Id, V(Version(submitted.Id)), "TST urgent", false));

        var acknowledged = await Ok(new AcknowledgeGrievanceCommand(submitted.Id, V(Version(submitted.Id)), "Critical", null, null));

        Assert.Equal("Acknowledged", acknowledged.Status);
        Assert.True(acknowledged.IsEscalated);
        Assert.Single(acknowledged.Escalations);
        var history = ResultAssert.Succeeded(await Cases.HistoryAsync(submitted.Id, default));
        Assert.Single(history, entry => entry.Action == "Escalate");
    }

    [Fact]
    public async Task Reopening_ends_every_assignment_and_the_same_lead_is_assigned_again_to_investigate()
    {
        await SeedAsync();
        var closed = await ClosedAsync();
        As(W.Hana, HanaUser, "Hana Manager", [.. SelfGrants, .. ManagementGrants, .. ReviewGrants]);

        var reopened = await Ok(new ReopenGrievanceCommand(closed.Id, V(Version(closed.Id)), "TST new facts"));

        Assert.Equal("Reopened", reopened.Status);
        Assert.Null(reopened.LeadInvestigatorEmployeeId);
        Assert.NotNull(Assert.Single(reopened.Assignments).UnassignedAtUtc);
        var history = ResultAssert.Succeeded(await Cases.HistoryAsync(closed.Id, default));
        var unassigned = history.Single(entry => entry.Action == "Unassign");
        Assert.Equal(("Reopened", "Reopened", false, GrievanceCase.ReopenUnassignReason), (unassigned.FromStatus, unassigned.ToStatus, unassigned.VisibleToSubmitter, unassigned.Reason));
        Assert.Contains(Audit.Entries, entry => entry.Action == GrievanceAuditAction.StatusChange && entry.Detail == "Closed>Reopened;unassigned:1");

        // The ended assignment takes the case out of Ivan's investigations until he is assigned again.
        AsIvan();
        ResultAssert.Failed(await Cases.GetAsync(closed.Id, default), ErrorType.NotFound);

        AsHana();
        var assigned = await Ok(new AssignGrievanceInvestigatorCommand(closed.Id, V(Version(closed.Id)), W.Ivan.Id, "Lead", null));
        Assert.Equal("Assigned", assigned.Status);
        Assert.Equal(W.Ivan.Id, assigned.LeadInvestigatorEmployeeId);
        AsIvan();
        var investigating = await Ok(new StartGrievanceInvestigationCommand(closed.Id, V(Version(closed.Id)), null));
        Assert.Equal("UnderInvestigation", investigating.Status);

        // The submitter's timeline never shows the unassignment.
        AsSam();
        var mine = ResultAssert.Succeeded(await My.GetAsync(closed.Id, default));
        Assert.DoesNotContain(mine.Timeline, entry => entry.Action == "Unassign");
    }

    [Fact]
    public async Task After_a_reopening_without_a_new_lead_a_rejected_proposal_returns_to_review()
    {
        await SeedAsync();
        var closed = await ClosedAsync();
        As(W.Hana, HanaUser, "Hana Manager", [.. SelfGrants, .. ManagementGrants, .. ReviewGrants]);
        await Ok(new ReopenGrievanceCommand(closed.Id, V(Version(closed.Id)), "TST new facts"));
        AsHana();
        await Ok(new StartGrievanceReviewCommand(closed.Id, V(Version(closed.Id)), null));
        await Ok(new ProposeGrievanceResolutionCommand(closed.Id, V(Version(closed.Id)), "TST A second resolution."));

        AsRita();
        var rejected = await Ok(new RejectGrievanceResolutionCommand(closed.Id, V(Version(closed.Id)), "TST not enough"));

        Assert.Equal("UnderReview", rejected.Status);
    }

    [Fact]
    public async Task Triage_cannot_make_an_active_investigator_the_subject()
    {
        await SeedAsync();
        var assigned = await AssignedAsync();
        AsHana();

        var refused = ResultAssert.FailedOnField(
            await Run(new TriageGrievanceCommand(assigned.Id, V(Version(assigned.Id)), W.Category.Id, "Medium", assigned.TargetResolutionDate, W.Ivan.Id, null, false)),
            "subjectEmployeeId");

        Assert.Equal(GrievanceCase.ConflictOfInterestMessage, refused.FieldErrors!["subjectEmployeeId"][0]);
        Assert.Contains(Audit.Entries, entry => entry.Action == GrievanceAuditAction.AccessRefused && entry.Detail == "assign:conflict");
        Assert.Null(Stored(assigned.Id).SubjectEmployeeId);
    }

    [Fact]
    public async Task Triage_needs_the_legal_hold_answer()
    {
        await SeedAsync();
        var acknowledged = await AcknowledgedAsync();
        AsHana();

        ResultAssert.FailedOnField(
            await Run(new TriageGrievanceCommand(acknowledged.Id, V(Version(acknowledged.Id)), W.Category.Id, "Medium", acknowledged.TargetResolutionDate, null, null, null)),
            "legalHold");
    }
}
