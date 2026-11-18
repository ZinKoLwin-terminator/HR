using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Grievances;

namespace Apex.Hrms.Domain.Tests.Grievances;

/// <summary>
/// The grievance records outside the golden transitions: enum texts, drafts, case numbers, the submitter link and read receipt, the
/// history entry rules, files, notes, corrective actions, the access log, triage and the unread badge. Fictional data only.
/// </summary>
public sealed class GrievanceEntitiesTests
{
    private static readonly DateOnly Today = GrievanceScenario.Today;
    private static readonly DateTime Now = GrievanceScenario.Now;
    private static readonly string StorageKey = new('a', 32);
    private static readonly byte[] Hash = new byte[32];

    private static void AssertTexts<T>(TextMap<T> map, params string[] expected)
        where T : struct, Enum
    {
        var values = Enum.GetValues<T>();
        Assert.Equal(expected, values.Select(map.ToText));
        Assert.All(expected, text => Assert.True(map.TryParse(text, out _)));
        Assert.False(map.TryParse(expected[0].ToUpperInvariant() + "x", out _));
        Assert.False(map.TryParse("0", out _));
    }

    // ---- Texts (as the CHECK constraints of the grv schema and the UI contract) ----

    [Fact]
    public void The_status_texts_are_the_twelve_keys()
    {
        AssertTexts(
            GrievanceText.Status,
            "Draft", "Submitted", "Acknowledged", "UnderReview", "Assigned", "UnderInvestigation",
            "AwaitingEmployeeResponse", "ResolutionProposed", "Resolved", "Closed", "Rejected", "Reopened");
        Assert.False(GrievanceText.Status.TryParse("Under Review", out _));
    }

    [Fact]
    public void The_history_action_texts_match_the_ui_contract()
    {
        AssertTexts(
            GrievanceText.HistoryAction,
            "Submit", "Acknowledge", "StartReview", "Assign", "Unassign", "StartInvestigation", "RequestInfo", "Respond",
            "ProposeResolution", "ApproveResolution", "RejectResolution", "Reject", "Close", "Reopen", "Escalate", "Archive");
    }

    [Fact]
    public void The_audit_action_texts_match_the_ui_contract()
    {
        AssertTexts(
            GrievanceText.AuditAction,
            "ViewCase", "ViewHistory", "DownloadAttachment", "UploadAttachment", "Export", "Assign", "Unassign",
            "StatusChange", "Triage", "AddNote", "SendMessage", "CorrectiveAction", "AccessRefused");
    }

    [Fact]
    public void The_other_texts()
    {
        AssertTexts(GrievanceText.Priority, "Low", "Medium", "High", "Critical");
        AssertTexts(GrievanceText.ActorRole, "Submitter", "Management", "Investigator", "Reviewer", "System");
        AssertTexts(GrievanceText.AssignmentRole, "Lead", "Member");
        AssertTexts(GrievanceText.NoteKind, "Note", "Interview", "FollowUp", "Finding", "Recommendation");
        AssertTexts(GrievanceText.MessageDirection, "ToSubmitter", "FromSubmitter");
        AssertTexts(GrievanceText.MessageKind, "Message", "InfoRequest", "InfoResponse");
        AssertTexts(GrievanceText.Visibility, "Shared", "CaseTeam", "Restricted");
        AssertTexts(GrievanceText.Decision, "Pending", "Approved", "Rejected");
        AssertTexts(GrievanceText.CorrectiveActionStatus, "Proposed", "Approved", "Rejected", "Implemented");
    }

    // ---- Categories ----

    [Fact]
    public void A_category_checks_its_code_name_and_target_days()
    {
        var bad = GrievanceCategory.Validate(new GrievanceCategoryDetails("bad code", "", null, true, 0, -1));

        Assert.Equal(["code", "defaultTargetDays", "name", "sortOrder"], bad.Error.FieldErrors!.Keys.Order(StringComparer.Ordinal));
        Assert.True(GrievanceCategory.Validate(new GrievanceCategoryDetails("SAFETY", "Workplace Safety", null, true, 7, 4)).IsSuccess);
    }

    // ---- Drafts ----

    [Fact]
    public void A_draft_has_no_number_and_keeps_the_trimmed_text()
    {
        var draft = GrievanceCase.CreateDraft(
            GrievanceScenario.DraftDetails() with { Subject = "  Repeated unfair shift assignments  ", Location = "  " },
            GrievanceScenario.Category(),
            Today).Value;

        Assert.Equal(GrievanceStatus.Draft, draft.Status);
        Assert.Null(draft.CaseNo);
        Assert.Equal("Repeated unfair shift assignments", draft.Subject);
        Assert.Null(draft.Location);
        Assert.Null(draft.SubmittedAtUtc);
    }

    [Fact]
    public void A_draft_checks_every_text_and_the_incident_date()
    {
        var details = GrievanceScenario.DraftDetails() with
        {
            Subject = "   ",
            Description = new string('x', GrievanceCase.DescriptionMaxLength + 1),
            IncidentDate = Today.AddDays(1),
            Location = new string('x', 201),
            InvolvedPersonText = new string('x', 201),
        };

        var result = GrievanceCase.ValidateDraft(details, GrievanceScenario.Category(), Today);

        Assert.Equal(
            ["description", "incidentDate", "involvedPersonText", "location", "subject"],
            result.Error.FieldErrors!.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void A_draft_may_be_unfinished_but_submitting_needs_the_minimum_lengths()
    {
        var unfinished = GrievanceScenario.DraftDetails() with { Subject = "Shif", Description = string.Empty };
        var draft = GrievanceCase.CreateDraft(unfinished, GrievanceScenario.Category(), Today);
        Assert.True(draft.IsSuccess);

        var submitted = draft.Value.Submit(
            GrievanceScenario.Category(), true, GrievanceCase.FormatCaseNo(2026, 1), null, GrievanceScenario.Submitter, Today, Now);

        Assert.Equal(["description", "subject"], submitted.Error.FieldErrors!.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(GrievanceStatus.Draft, draft.Value.Status);
    }

    [Fact]
    public void A_named_submitter_is_recorded_without_ids_or_name()
    {
        var scenario = GrievanceScenario.Reach(new ScenarioOptions(GrievanceStatus.Submitted));

        var recorded = scenario.Case.RecordedActor(GrievanceScenario.Submitter);

        Assert.Equal(GrievanceActor.NamedSubmitter, recorded);
        Assert.Null(recorded.UserId);
        Assert.Null(recorded.EmployeeId);
        Assert.Equal(GrievanceActor.NamedSubmitterName, recorded.Name);
        Assert.Throws<ArgumentException>(() => GrievanceStatusHistoryEntry.Create(
            1, GrievanceStatus.Draft, GrievanceStatus.Submitted, GrievanceHistoryAction.Submit, GrievanceScenario.Submitter, null, true, Now));
        Assert.Throws<ArgumentException>(() => GrievanceStatusHistoryEntry.Create(
            1, GrievanceStatus.Submitted, GrievanceStatus.Submitted, GrievanceHistoryAction.Escalate,
            new GrievanceActor(20, 200, GrievanceActor.NamedSubmitterName, GrievanceActorRole.Management), "Reason.", false, Now));
        Assert.Null(GrievanceAuditEntry.Create(1, GrievanceAuditAction.SendMessage, GrievanceScenario.Submitter, null, Now).ActorUserId);
    }

    [Fact]
    public void A_draft_needs_an_active_category_that_allows_anonymity_when_it_is_anonymous()
    {
        var details = GrievanceScenario.DraftDetails(isAnonymous: true);

        var result = GrievanceCase.ValidateDraft(details, GrievanceScenario.Category(active: false, allowsAnonymous: false), Today);

        Assert.Equal(["categoryId", "isAnonymous"], result.Error.FieldErrors!.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Only_a_draft_can_be_edited()
    {
        var scenario = GrievanceScenario.Reach(new ScenarioOptions(GrievanceStatus.Submitted));

        var result = scenario.Case.UpdateDraft(GrievanceScenario.DraftDetails(), GrievanceScenario.Category(), Today);

        Assert.Equal(ErrorType.Conflict, result.Error.Type);
    }

    [Fact]
    public void A_draft_can_switch_to_anonymous_while_it_is_a_draft()
    {
        var scenario = GrievanceScenario.NewDraft();

        Assert.True(scenario.Case.UpdateDraft(GrievanceScenario.DraftDetails(isAnonymous: true), GrievanceScenario.Category(), Today).IsSuccess);
        Assert.True(scenario.Case.IsAnonymous);
    }

    // ---- Case numbers ----

    [Theory]
    [InlineData(2026, 1, "GRV-2026-00001")]
    [InlineData(2026, 99999, "GRV-2026-99999")]
    [InlineData(2027, 123456, "GRV-2027-123456")]
    public void Case_numbers_have_at_least_five_digits(int year, long number, string expected)
    {
        Assert.Equal(expected, GrievanceCase.FormatCaseNo(year, number));
        Assert.True(GrievanceCase.IsValidCaseNo(expected));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("GRV-2026-0001")]
    [InlineData("grv-2026-00001")]
    [InlineData("GRV-2026-0000A")]
    [InlineData("LV-2026-00001")]
    [InlineData("GRV-2026-000000000001")]
    public void Other_case_numbers_are_refused(string? caseNo) => Assert.False(GrievanceCase.IsValidCaseNo(caseNo));

    [Fact]
    public void A_malformed_case_number_is_a_programming_error()
    {
        var scenario = GrievanceScenario.NewDraft();

        Assert.Throws<ArgumentException>(() =>
            scenario.Case.Submit(GrievanceScenario.Category(), true, "GRV-1", null, GrievanceScenario.Submitter, Today, Now));
    }

    // ---- Submitter link ----

    [Fact]
    public void A_named_submitter_is_matched_by_employee_and_an_anonymous_one_by_pseudonym_only()
    {
        byte[] mine = [.. Enumerable.Repeat((byte)7, 32)];
        byte[] other = [.. Enumerable.Repeat((byte)8, 32)];

        var named = GrievanceCaseSubmitter.Named(1, 100);
        var anonymous = GrievanceCaseSubmitter.Anonymous(1, mine);

        Assert.True(named.Is(100, other));
        Assert.False(named.Is(101, mine));
        Assert.True(anonymous.Is(999, mine));
        Assert.False(anonymous.Is(100, other));
        Assert.Null(anonymous.EmployeeId);
        Assert.True(anonymous.IsAnonymous);
    }

    [Fact]
    public void An_anonymous_submitter_keeps_a_copy_of_a_32_byte_pseudonym()
    {
        byte[] pseudonym = [.. Enumerable.Repeat((byte)7, 32)];
        var submitter = GrievanceCaseSubmitter.Anonymous(1, pseudonym);
        pseudonym[0] = 0;

        Assert.Equal(7, submitter.SubmitterPseudonym![0]);
        Assert.Throws<ArgumentException>(() => GrievanceCaseSubmitter.Anonymous(1, new byte[16]));
    }

    [Fact]
    public void The_read_receipt_only_moves_forward_and_drives_the_unread_badge()
    {
        var submitter = GrievanceCaseSubmitter.Named(1, 100);

        Assert.False(submitter.HasUnseenUpdate(null));
        Assert.True(submitter.HasUnseenUpdate(Now));
        submitter.MarkSeen(Now);
        submitter.MarkSeen(Now.AddHours(-1));

        Assert.Equal(Now, submitter.SubmitterSeenAtUtc);
        Assert.False(submitter.HasUnseenUpdate(Now));
        Assert.True(submitter.HasUnseenUpdate(Now.AddMinutes(1)));
    }

    // ---- Unread badge and the case row ----

    [Fact]
    public void A_plain_submitter_message_and_a_response_do_not_touch_the_unread_badge()
    {
        var plain = GrievanceScenario.Reach(new ScenarioOptions(GrievanceStatus.UnderReview));
        var badge = plain.Case.LastSubmitterUpdateAtUtc;
        Assert.True(plain.Case.PostSubmitterMessage("More detail.", GrievanceScenario.Submitter, Now.AddHours(1)).IsSuccess);
        Assert.Equal(badge, plain.Case.LastSubmitterUpdateAtUtc);

        var awaiting = GrievanceScenario.Reach(new ScenarioOptions(GrievanceStatus.AwaitingEmployeeResponse) { ReturnStatus = GrievanceStatus.UnderReview });
        badge = awaiting.Case.LastSubmitterUpdateAtUtc;
        Assert.True(awaiting.Case.Respond("The answer.", 5, GrievanceScenario.Submitter, Now.AddHours(1)).IsSuccess);
        Assert.Equal(badge, awaiting.Case.LastSubmitterUpdateAtUtc);
    }

    [Fact]
    public void A_team_message_updates_the_unread_badge_and_a_proposal_does_not()
    {
        var scenario = GrievanceScenario.Reach(new ScenarioOptions(GrievanceStatus.UnderReview));
        var later = Now.AddHours(1);

        scenario.Apply(scenario.Case.ProposeResolution("TST proposal.", GrievanceScenario.Management, later));
        Assert.Equal(Now, scenario.Case.LastSubmitterUpdateAtUtc);

        Assert.True(scenario.Case.PostTeamMessage("We are reviewing it.", GrievanceScenario.Management, later).IsSuccess);
        Assert.Equal(later, scenario.Case.LastSubmitterUpdateAtUtc);
    }

    [Fact]
    public void An_anonymous_submitters_messages_keep_no_ids()
    {
        var scenario = GrievanceScenario.Reach(new ScenarioOptions(GrievanceStatus.UnderReview) { IsAnonymous = true });

        var message = scenario.Case.PostSubmitterMessage("More detail.", GrievanceScenario.Submitter, Now).Value;

        Assert.Null(message.AuthorUserId);
        Assert.Null(message.AuthorEmployeeId);
        Assert.Equal(GrievanceActor.AnonymousSubmitterName, message.AuthorName);
        Assert.Equal(GrievanceActorRole.Submitter, message.AuthorRole);
    }

    [Fact]
    public void The_response_replies_to_the_request()
    {
        var scenario = GrievanceScenario.Reach(new ScenarioOptions(GrievanceStatus.AwaitingEmployeeResponse) { ReturnStatus = GrievanceStatus.UnderReview });

        var step = scenario.Case.Respond("The answer.", 5, GrievanceScenario.Submitter, Now).Value;

        Assert.Equal(5, step.Message!.InReplyToId);
    }

    [Fact]
    public void Submitter_steps_and_team_steps_refuse_the_wrong_kind_of_actor()
    {
        var draft = GrievanceScenario.NewDraft();
        var review = GrievanceScenario.Reach(new ScenarioOptions(GrievanceStatus.UnderReview));
        var system = new GrievanceActor(null, null, "System", GrievanceActorRole.System);

        Assert.Throws<ArgumentException>(() => draft.Case.Submit(GrievanceScenario.Category(), true, GrievanceScenario.CaseNo, null, GrievanceScenario.Management, Today, Now));
        Assert.Throws<ArgumentException>(() => review.Case.Escalate("Reason.", GrievanceScenario.Submitter, Now));
        Assert.Throws<ArgumentException>(() => review.Case.Escalate("Reason.", system, Now));
    }

    // ---- History entries ----

    [Theory]
    [InlineData(GrievanceHistoryAction.Reject)]
    [InlineData(GrievanceHistoryAction.Reopen)]
    [InlineData(GrievanceHistoryAction.Escalate)]
    [InlineData(GrievanceHistoryAction.RejectResolution)]
    [InlineData(GrievanceHistoryAction.RequestInfo)]
    public void These_history_actions_need_a_reason(GrievanceHistoryAction action)
    {
        var result = GrievanceStatusHistoryEntry.Create(1, GrievanceStatus.UnderReview, GrievanceStatus.UnderReview, action, GrievanceScenario.Management, " ", false, Now);

        Assert.Equal(["reason"], result.Error.FieldErrors!.Keys);
        Assert.True(GrievanceStatusHistoryEntry.RequiresReason(action));
    }

    [Fact]
    public void Nobody_else_can_be_recorded_under_the_anonymous_name()
    {
        var pretender = new GrievanceActor(10, 100, GrievanceActor.AnonymousSubmitterName, GrievanceActorRole.Submitter);

        Assert.Throws<ArgumentException>(() => GrievanceStatusHistoryEntry.Create(
            1, GrievanceStatus.Draft, GrievanceStatus.Submitted, GrievanceHistoryAction.Submit, pretender, null, true, Now));
    }

    // ---- Files ----

    [Fact]
    public void A_submitters_file_is_always_shared_and_an_anonymous_one_keeps_no_user()
    {
        var scenario = GrievanceScenario.Reach(new ScenarioOptions(GrievanceStatus.UnderReview) { IsAnonymous = true });
        var uploader = scenario.Case.RecordedActor(GrievanceScenario.Submitter);

        var file = GrievanceAttachment.Create(0, null, null, GrievanceAttachmentVisibility.Restricted, uploader, "rota.pdf", "application/pdf", 100, StorageKey, Hash, Now).Value;

        Assert.Equal(GrievanceAttachmentVisibility.Shared, file.Visibility);
        Assert.Null(file.UploadedByUserId);
        Assert.Equal(GrievanceActorRole.Submitter, file.UploadedByRole);
    }

    [Fact]
    public void A_file_checks_its_size_key_hash_parent_and_visibility()
    {
        var result = GrievanceAttachment.Create(
            0, 1, 2, GrievanceAttachmentVisibility.Shared, GrievanceScenario.Investigator, "notes.pdf", "application/pdf",
            GrievanceAttachment.MaxSizeBytes + 1, "../escape", new byte[5], Now);

        Assert.Equal(
            ["file", "noteId", "sha256", "storageKey", "visibility"],
            result.Error.FieldErrors!.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Removing_a_file_keeps_the_row_and_the_first_time()
    {
        var file = GrievanceAttachment.Create(0, null, null, GrievanceAttachmentVisibility.CaseTeam, GrievanceScenario.Management, "a.pdf", "application/pdf", 1, StorageKey, Hash, Now).Value;

        file.Remove(Now.AddHours(1));
        file.Remove(Now.AddHours(2));

        Assert.Equal(Now.AddHours(1), file.RemovedAtUtc);
    }

    [Fact]
    public void Files_are_added_to_a_draft_or_an_open_case_only()
    {
        Assert.True(GrievanceScenario.NewDraft().Case.EnsureFilesOpen().IsSuccess);
        Assert.True(GrievanceScenario.Reach(new ScenarioOptions(GrievanceStatus.UnderReview)).Case.EnsureFilesOpen().IsSuccess);
        Assert.Equal(ErrorType.Conflict, GrievanceScenario.Reach(new ScenarioOptions(GrievanceStatus.Closed)).Case.EnsureFilesOpen().Error.Type);
    }

    // ---- Notes ----

    [Fact]
    public void An_interview_needs_its_date_and_other_notes_have_none()
    {
        var interview = GrievanceInvestigationNote.Create(0, InvestigationNoteKind.Interview, "Met the witness.", null, "Witness A", GrievanceScenario.Investigator, Today, Now);
        var finding = GrievanceInvestigationNote.Create(0, InvestigationNoteKind.Finding, "The rota was changed 6 times.", Today, null, GrievanceScenario.Investigator, Today, Now);
        var future = GrievanceInvestigationNote.Create(0, InvestigationNoteKind.Interview, "Planned.", Today.AddDays(1), null, GrievanceScenario.Investigator, Today, Now);

        Assert.Equal(["interviewDate"], interview.Error.FieldErrors!.Keys);
        Assert.Equal(["kind"], finding.Error.FieldErrors!.Keys);
        Assert.Equal(["interviewDate"], future.Error.FieldErrors!.Keys);
    }

    [Fact]
    public void Reviewers_see_findings_and_recommendations_only()
    {
        GrievanceInvestigationNote Note(InvestigationNoteKind kind) =>
            GrievanceInvestigationNote.Create(0, kind, "Text.", kind == InvestigationNoteKind.Interview ? Today : null, null, GrievanceScenario.Investigator, Today, Now).Value;

        Assert.True(Note(InvestigationNoteKind.Finding).IsVisibleToReviewers);
        Assert.True(Note(InvestigationNoteKind.Recommendation).IsVisibleToReviewers);
        Assert.False(Note(InvestigationNoteKind.Note).IsVisibleToReviewers);
        Assert.False(Note(InvestigationNoteKind.Interview).IsVisibleToReviewers);
        Assert.False(Note(InvestigationNoteKind.FollowUp).IsVisibleToReviewers);
    }

    [Fact]
    public void The_submitter_never_writes_a_note()
    {
        Assert.Throws<ArgumentException>(() => GrievanceInvestigationNote.Create(
            0, InvestigationNoteKind.Note, "Text.", null, null, GrievanceScenario.Submitter, Today, Now));
    }

    [Fact]
    public void Notes_are_added_to_an_open_case_only()
    {
        Assert.True(GrievanceScenario.Reach(new ScenarioOptions(GrievanceStatus.UnderInvestigation)).Case.EnsureInvestigationOpen().IsSuccess);
        Assert.Equal(ErrorType.Conflict, GrievanceScenario.Reach(new ScenarioOptions(GrievanceStatus.Resolved)).Case.EnsureInvestigationOpen().Error.Type);
    }

    // ---- Corrective actions ----

    [Fact]
    public void A_corrective_action_is_implemented_exactly_when_it_has_a_completion_date()
    {
        var missing = GrievanceCorrectiveAction.Validate(new CorrectiveActionDetails("Training", "Rota training for team leads.", null, null, null, CorrectiveActionStatus.Implemented));
        var early = GrievanceCorrectiveAction.Validate(new CorrectiveActionDetails("Training", "Rota training for team leads.", null, null, Today, CorrectiveActionStatus.Approved));
        var blank = GrievanceCorrectiveAction.Validate(new CorrectiveActionDetails(" ", " ", null, null, null, CorrectiveActionStatus.Proposed));

        Assert.Equal(["completedOn"], missing.Error.FieldErrors!.Keys);
        Assert.Equal(["completedOn"], early.Error.FieldErrors!.Keys);
        Assert.Equal(["actionType", "description"], blank.Error.FieldErrors!.Keys.Order(StringComparer.Ordinal));
        Assert.True(GrievanceCorrectiveAction.Create(1, new CorrectiveActionDetails("Mediation", "Facilitated meeting.", "HR Business Partner", Today, Today, CorrectiveActionStatus.Implemented)).IsSuccess);
    }

    // ---- Access log ----

    [Fact]
    public void The_access_log_keeps_no_user_for_the_anonymous_submitter_and_codes_only()
    {
        var entry = GrievanceAuditEntry.Create(1, GrievanceAuditAction.ViewCase, GrievanceActor.AnonymousSubmitter, null, Now);

        Assert.Null(entry.ActorUserId);
        Assert.Equal(GrievanceActorRole.Submitter, entry.ActorRole);
        Assert.Throws<ArgumentException>(() => GrievanceAuditEntry.Create(1, GrievanceAuditAction.Export, GrievanceScenario.Management, new string('x', 501), Now));
    }

    // ---- Triage, overdue, legal hold ----

    [Fact]
    public void Triage_keeps_the_target_out_of_the_past_and_the_submitter_out_of_the_subject()
    {
        var scenario = GrievanceScenario.Reach(new ScenarioOptions(GrievanceStatus.UnderReview));

        var result = scenario.Case.Triage(GrievancePriority.High, Today.AddDays(-1), GrievanceScenario.NamedSubmitterEmployeeId, null, GrievanceScenario.NamedSubmitterEmployeeId, Today);

        Assert.Equal(["subjectEmployeeId", "targetResolutionDate"], result.Error.FieldErrors!.Keys.Order(StringComparer.Ordinal));
        Assert.True(scenario.Case.Triage(GrievancePriority.High, Today, 500, 3, GrievanceScenario.NamedSubmitterEmployeeId, Today).IsSuccess);
        Assert.Equal(500, scenario.Case.SubjectEmployeeId);
    }

    [Fact]
    public void An_acknowledged_case_keeps_its_target_date_at_triage()
    {
        var scenario = GrievanceScenario.Reach(new ScenarioOptions(GrievanceStatus.UnderReview));

        var result = scenario.Case.Triage(GrievancePriority.High, null, null, null, GrievanceScenario.NamedSubmitterEmployeeId, Today);

        Assert.Equal(["targetResolutionDate"], result.Error.FieldErrors!.Keys);
    }

    [Fact]
    public void Reclassifying_needs_an_active_category_that_suits_an_anonymous_case()
    {
        var scenario = GrievanceScenario.Reach(new ScenarioOptions(GrievanceStatus.UnderReview) { IsAnonymous = true });
        var named = OtherCategory(7, allowsAnonymous: false);
        var inactive = OtherCategory(8, active: false);
        var fitting = OtherCategory(9);

        Assert.Equal(["categoryId"], scenario.Case.Reclassify(named).Error.FieldErrors!.Keys);
        Assert.Equal(["categoryId"], scenario.Case.Reclassify(inactive).Error.FieldErrors!.Keys);
        Assert.True(scenario.Case.Reclassify(GrievanceScenario.Category(active: false)).IsSuccess);
        Assert.True(scenario.Case.Reclassify(fitting).IsSuccess);
        Assert.Equal(9, scenario.Case.CategoryId);
        Assert.Equal(ErrorType.Conflict, GrievanceScenario.Reach(new ScenarioOptions(GrievanceStatus.Closed)).Case.Reclassify(fitting).Error.Type);
    }

    [Fact]
    public void A_closed_or_rejected_case_gets_a_retention_date_that_reopening_clears()
    {
        var closed = GrievanceScenario.Reach(new ScenarioOptions(GrievanceStatus.Closed));
        var open = GrievanceScenario.Reach(new ScenarioOptions(GrievanceStatus.UnderReview));

        closed.Case.ApplyRetention(Today, 7);

        Assert.Equal(new DateOnly(2033, 11, 18), closed.Case.RetentionUntil);
        Assert.Throws<InvalidOperationException>(() => open.Case.ApplyRetention(Today, 7));
        Assert.True(closed.Case.Reopen("TST reopened.", [.. closed.ActiveAssignments], GrievanceScenario.Management, Now).IsSuccess);
        Assert.Null(closed.Case.RetentionUntil);
    }

    [Fact]
    public void After_a_reopening_without_a_new_lead_a_proposal_sent_back_returns_to_review()
    {
        var scenario = GrievanceScenario.Reach(new ScenarioOptions(GrievanceStatus.Reopened) { HasLead = true });
        Assert.False(scenario.HasActiveLead);

        scenario.Apply(scenario.Case.StartReview(GrievanceScenario.Management, Now));
        var proposed = scenario.Apply(scenario.Case.ProposeResolution("TST second resolution.", GrievanceScenario.Management, Now));
        scenario.Apply(scenario.Case.RejectResolution(proposed.Proposal!, "TST not enough.", scenario.HasActiveLead, GrievanceScenario.Reviewer, Now));

        Assert.Equal(GrievanceStatus.UnderReview, scenario.Case.Status);
    }

    [Fact]
    public void An_immediate_escalation_and_a_step_comment_are_recorded()
    {
        var scenario = GrievanceScenario.Reach(new ScenarioOptions(GrievanceStatus.Acknowledged));

        var escalated = scenario.Case.Escalate("TST urgent.", GrievanceScenario.Management, Now, isImmediate: true).Value;
        var started = scenario.Case.StartReview(GrievanceScenario.Management, Now, "TST looking into it.").Value;

        Assert.True(escalated.Escalation!.IsImmediate);
        Assert.Equal("TST looking into it.", started.Entry.Reason);
    }

    private static GrievanceCategory OtherCategory(int id, bool active = true, bool allowsAnonymous = true)
    {
        var category = GrievanceScenario.Category(active, allowsAnonymous);
        typeof(Entity).GetProperty(nameof(Entity.Id))!.SetValue(category, id);
        return category;
    }

    [Fact]
    public void Triage_of_a_closed_case_is_refused()
    {
        var scenario = GrievanceScenario.Reach(new ScenarioOptions(GrievanceStatus.Closed));

        Assert.Equal(ErrorType.Conflict, scenario.Case.Triage(GrievancePriority.Low, null, null, null, null, Today).Error.Type);
    }

    [Fact]
    public void An_open_case_past_its_target_is_overdue_and_a_resolved_one_is_not()
    {
        var open = GrievanceScenario.Reach(new ScenarioOptions(GrievanceStatus.UnderReview));
        var resolved = GrievanceScenario.Reach(new ScenarioOptions(GrievanceStatus.Resolved));
        var target = open.Case.TargetResolutionDate!.Value;

        Assert.False(open.Case.IsOverdue(target));
        Assert.True(open.Case.IsOverdue(target.AddDays(1)));
        Assert.False(resolved.Case.IsOverdue(target.AddDays(1)));
    }

    [Fact]
    public void A_legal_hold_can_be_placed_on_an_archived_case_but_not_on_a_draft()
    {
        var archived = GrievanceScenario.Reach(new ScenarioOptions(GrievanceStatus.Closed) { Archived = true });

        Assert.True(archived.Case.SetLegalHold(true).IsSuccess);
        Assert.Equal(ErrorType.Conflict, GrievanceScenario.NewDraft().Case.SetLegalHold(true).Error.Type);
    }

    [Fact]
    public void Reopening_clears_the_resolved_and_closed_times_but_keeps_the_resolution()
    {
        var scenario = GrievanceScenario.Reach(new ScenarioOptions(GrievanceStatus.Reopened));

        Assert.Null(scenario.Case.ResolvedAtUtc);
        Assert.Null(scenario.Case.ClosedAtUtc);
        Assert.Equal("TST resolution on the way.", scenario.Case.ResolutionSummary);
    }
}
