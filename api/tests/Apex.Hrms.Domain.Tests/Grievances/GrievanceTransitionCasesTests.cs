using System.Globalization;
using System.Text.Json;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Grievances;

namespace Apex.Hrms.Domain.Tests.Grievances;

/// <summary>
/// Runs every shared case of docs/test-data/grievance-transition-cases.json against <see cref="GrievanceCase"/>'s workflow. The file
/// documents its own shape in <c>_comment</c>; the Angular demo mock is meant to run the same file.
/// </summary>
public sealed class GrievanceTransitionCasesTests
{
    private static readonly string[] Actions =
    [
        "submit", "acknowledge", "reject", "startReview", "assign", "unassign", "startInvestigation", "requestInfo", "respond",
        "postSubmitterMessage", "postTeamMessage", "proposeResolution", "approveResolution", "rejectResolution", "close", "reopen",
        "escalate", "archive",
    ];

    private static List<JsonElement> LoadCases()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "grievance-transition-cases.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return [.. document.RootElement.GetProperty("cases").EnumerateArray().Select(element => element.Clone())];
    }

    public static TheoryData<string> CaseNames()
    {
        var names = new TheoryData<string>();
        foreach (var shared in LoadCases())
        {
            names.Add(shared.GetProperty("name").GetString()!);
        }

        return names;
    }

    [Fact]
    public void The_file_has_unique_names_known_actions_and_covers_every_action_and_status()
    {
        var cases = LoadCases();
        var names = cases.Select(shared => shared.GetProperty("name").GetString()).ToList();
        var actions = cases.Select(shared => shared.GetProperty("action").GetString()).ToList();
        var statuses = cases.Select(shared => shared.GetProperty("case").GetProperty("status").GetString()).ToHashSet();

        Assert.Equal(names.Count, names.Distinct().Count());
        Assert.All(actions, action => Assert.Contains(action, Actions));
        Assert.All(Actions, action => Assert.Contains(action, actions));
        Assert.All(Enum.GetNames<GrievanceStatus>(), status => Assert.Contains(status, statuses));
        Assert.All(cases, shared => Assert.True(
            shared.TryGetProperty("expected", out _) ^ shared.TryGetProperty("expectedError", out _),
            shared.GetProperty("name").GetString()));
    }

    [Theory]
    [MemberData(nameof(CaseNames))]
    public void The_shared_case_gives_the_expected_result(string name)
    {
        var shared = LoadCases().Single(candidate => candidate.GetProperty("name").GetString() == name);
        var scenario = GrievanceScenario.Reach(Options(shared.GetProperty("case")));
        var before = Fingerprint(scenario);
        var input = shared.TryGetProperty("input", out var given) ? given : default;
        var actor = GrievanceScenario.ActorNamed(shared.GetProperty("actor").GetString()!);

        var (outcome, step, message) = Run(scenario, shared.GetProperty("action").GetString()!, input, actor);

        if (shared.TryGetProperty("expectedError", out var expectedError))
        {
            Assert.True(outcome.IsFailure, "The step was expected to fail.");
            Assert.Equal(Enum.Parse<ErrorType>(expectedError.GetProperty("type").GetString()!), outcome.Error.Type);
            if (expectedError.TryGetProperty("fields", out var fields))
            {
                Assert.Equal(
                    fields.EnumerateArray().Select(field => field.GetString()!).Order(StringComparer.Ordinal),
                    (outcome.Error.FieldErrors?.Keys ?? []).Order(StringComparer.Ordinal));
            }

            Assert.Equal(before, Fingerprint(scenario));
            return;
        }

        Assert.True(outcome.IsSuccess, outcome.IsFailure ? outcome.Error.Message : null);
        AssertExpected(shared.GetProperty("expected"), scenario, step, message);
    }

    private static ScenarioOptions Options(JsonElement state) =>
        new(Status(state.GetProperty("status").GetString())!.Value)
        {
            IsAnonymous = Bool(state, "isAnonymous"),
            ReturnStatus = Status(Text(state, "returnStatus")),
            HasLead = Bool(state, "hasLead"),
            Members = state.TryGetProperty("members", out var members) ? [.. members.EnumerateArray().Select(member => member.GetInt32())] : [],
            ProposedBy = Text(state, "proposedBy"),
            ReopenedFrom = Status(Text(state, "reopenedFrom")),
            IsEscalated = Bool(state, "isEscalated"),
            LegalHold = Bool(state, "legalHold"),
            Archived = Bool(state, "archived"),
            SubjectEmployeeId = state.TryGetProperty("subjectEmployeeId", out var subject) && subject.ValueKind == JsonValueKind.Number ? subject.GetInt32() : null,
            ReassignedAfterReopen = Bool(state, "reassignedAfterReopen"),
        };

    private static (Result Outcome, GrievanceStep? Step, GrievanceCommunication? Message) Run(
        GrievanceScenario scenario,
        string action,
        JsonElement input,
        GrievanceActor actor)
    {
        var grievance = scenario.Case;
        var now = GrievanceScenario.Now;
        Result<GrievanceStep> stepResult;
        switch (action)
        {
            case "submit":
                stepResult = grievance.Submit(
                    GrievanceScenario.Category(
                        active: BoolOr(input, "categoryActive", true),
                        allowsAnonymous: BoolOr(input, "categoryAllowsAnonymous", true)),
                    BoolOr(input, "declarationAccepted", true),
                    GrievanceScenario.CaseNo,
                    GrievanceScenario.Placement,
                    actor,
                    GrievanceScenario.Today,
                    now);
                break;
            case "acknowledge":
                stepResult = grievance.Acknowledge(
                    GrievanceText.Priority.Parse(Text(input, "priority")),
                    Text(input, "targetResolutionDate") is { } target ? DateOnly.Parse(target, CultureInfo.InvariantCulture) : null,
                    GrievanceScenario.Category(defaultTargetDays: (short)input.GetProperty("defaultTargetDays").GetInt32()),
                    actor,
                    GrievanceScenario.Today,
                    now);
                break;
            case "reject":
                stepResult = grievance.Reject(Text(input, "reason"), actor, now);
                break;
            case "startReview":
                stepResult = grievance.StartReview(actor, now);
                break;
            case "assign":
                stepResult = grievance.Assign(
                    input.GetProperty("investigatorEmployeeId").GetInt32(),
                    GrievanceText.AssignmentRole.Parse(Text(input, "role")),
                    scenario.ActiveAssignments,
                    scenario.NamedSubmitter,
                    Text(input, "reason"),
                    actor,
                    now);
                break;
            case "unassign":
                var employee = input.GetProperty("investigatorEmployeeId").GetInt32();
                stepResult = grievance.Unassign(scenario.ActiveAssignments.Single(assignment => assignment.InvestigatorEmployeeId == employee), null, actor, now);
                break;
            case "startInvestigation":
                stepResult = grievance.StartInvestigation(actor, now);
                break;
            case "requestInfo":
                stepResult = grievance.RequestInfo(Text(input, "message"), actor, now);
                break;
            case "respond":
                stepResult = grievance.Respond(Text(input, "message"), null, actor, now);
                break;
            case "postSubmitterMessage":
                return Message(grievance.PostSubmitterMessage(Text(input, "message"), actor, now));
            case "postTeamMessage":
                return Message(grievance.PostTeamMessage(Text(input, "message"), actor, now));
            case "proposeResolution":
                stepResult = grievance.ProposeResolution(Text(input, "summary"), actor, now);
                break;
            case "approveResolution":
                stepResult = grievance.ApproveResolution(
                    scenario.Proposal ?? BorrowedProposal(), Bool(input, "visibleToSubmitter"), Text(input, "comment"), actor, now);
                break;
            case "rejectResolution":
                stepResult = grievance.RejectResolution(
                    scenario.Proposal ?? BorrowedProposal(), Text(input, "reason"), scenario.HasActiveLead, actor, now);
                break;
            case "close":
                stepResult = grievance.Close(actor, now);
                break;
            case "reopen":
                stepResult = grievance.Reopen(Text(input, "reason"), [.. scenario.ActiveAssignments], actor, now);
                break;
            case "escalate":
                stepResult = grievance.Escalate(Text(input, "reason"), actor, now);
                break;
            case "archive":
                stepResult = grievance.Archive(actor, now);
                break;
            default:
                throw new ArgumentException($"Unknown action '{action}'.", nameof(action));
        }

        return stepResult.IsSuccess ? (stepResult, stepResult.Value, stepResult.Value.Message) : (stepResult, null, null);
    }

    private static (Result, GrievanceStep?, GrievanceCommunication?) Message(Result<GrievanceCommunication> result) =>
        (result, null, result.IsSuccess ? result.Value : null);

    /// <summary>A pending proposal of the same case id (0) from another scenario, for a decision on a case that has none.</summary>
    private static GrievanceResolutionProposal BorrowedProposal() =>
        GrievanceScenario.Reach(new ScenarioOptions(GrievanceStatus.ResolutionProposed)).Proposal!;

    private static void AssertExpected(JsonElement expected, GrievanceScenario scenario, GrievanceStep? step, GrievanceCommunication? message)
    {
        var grievance = scenario.Case;
        foreach (var property in expected.EnumerateObject())
        {
            var value = property.Value;
            switch (property.Name)
            {
                case "status":
                    Assert.Equal(value.GetString(), GrievanceText.Status.ToText(grievance.Status));
                    break;
                case "returnStatus":
                    Assert.Equal(value.GetString(), grievance.ReturnStatus is { } returnStatus ? GrievanceText.Status.ToText(returnStatus) : null);
                    break;
                case "isEscalated":
                    Assert.Equal(value.GetBoolean(), grievance.IsEscalated);
                    break;
                case "archived":
                    Assert.Equal(value.GetBoolean(), grievance.IsArchived);
                    break;
                case "hasPlacement":
                    Assert.Equal(value.GetBoolean(), grievance.CompanyId is not null);
                    Assert.Equal(value.GetBoolean(), grievance.PositionId is not null);
                    break;
                case "priority":
                    Assert.Equal(value.GetString(), grievance.Priority is { } priority ? GrievanceText.Priority.ToText(priority) : null);
                    break;
                case "targetResolutionDate":
                    Assert.Equal(value.GetString(), grievance.TargetResolutionDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                    break;
                case "rejectionReason":
                    Assert.Equal(value.GetString(), grievance.RejectionReason);
                    break;
                case "resolutionVisibleToSubmitter":
                    Assert.Equal(value.GetBoolean(), grievance.ResolutionVisibleToSubmitter);
                    Assert.NotNull(grievance.ResolutionSummary);
                    break;
                case "proposalDecision":
                    Assert.Equal(value.GetString(), GrievanceText.Decision.ToText(step!.Proposal!.Decision));
                    break;
                case "assignment":
                    Assert.Equal(value.GetProperty("investigatorEmployeeId").GetInt32(), step!.Assignment!.InvestigatorEmployeeId);
                    Assert.Equal(value.GetProperty("role").GetString(), GrievanceText.AssignmentRole.ToText(step.Assignment.Role));
                    Assert.True(step.Assignment.IsActive);
                    break;
                case "endedAssignment":
                    Assert.Equal(value.ValueKind == JsonValueKind.Null ? null : value.GetInt32(), step!.EndedAssignment?.InvestigatorEmployeeId);
                    Assert.NotEqual(true, step.EndedAssignment?.IsActive);
                    break;
                case "endedAssignments":
                    Assert.Equal(value.EnumerateArray().Select(id => id.GetInt32()), step!.EndedAssignments.Select(assignment => assignment.InvestigatorEmployeeId));
                    Assert.All(step.EndedAssignments, assignment => Assert.False(assignment.IsActive));
                    break;
                case "message":
                    AssertMessage(value, message);
                    break;
                case "history":
                    AssertHistory(value, step?.History ?? []);
                    break;
                default:
                    Assert.Fail($"Unknown expected field '{property.Name}'.");
                    break;
            }
        }
    }

    private static void AssertMessage(JsonElement expected, GrievanceCommunication? message)
    {
        if (expected.ValueKind == JsonValueKind.Null)
        {
            Assert.Null(message);
            return;
        }

        Assert.NotNull(message);
        Assert.Equal(expected.GetProperty("direction").GetString(), GrievanceText.MessageDirection.ToText(message.Direction));
        Assert.Equal(expected.GetProperty("kind").GetString(), GrievanceText.MessageKind.ToText(message.Kind));
    }

    private static void AssertHistory(JsonElement expected, IReadOnlyList<GrievanceStatusHistoryEntry> actual)
    {
        var entries = expected.EnumerateArray().ToList();
        Assert.Equal(entries.Count, actual.Count);
        for (var i = 0; i < entries.Count; i++)
        {
            var want = entries[i];
            var got = actual[i];
            Assert.Equal(want.GetProperty("action").GetString(), GrievanceText.HistoryAction.ToText(got.Action));
            Assert.Equal(want.GetProperty("fromStatus").GetString(), got.FromStatus is { } from ? GrievanceText.Status.ToText(from) : null);
            Assert.Equal(want.GetProperty("toStatus").GetString(), GrievanceText.Status.ToText(got.ToStatus));
            Assert.Equal(want.GetProperty("actorRole").GetString(), GrievanceText.ActorRole.ToText(got.ActorRole));
            Assert.Equal(want.GetProperty("visibleToSubmitter").GetBoolean(), got.VisibleToSubmitter);
            Assert.Equal(want.GetProperty("reason").GetString(), got.Reason);
            Assert.Equal(GrievanceScenario.Now, got.OccurredAtUtc);
            if (want.TryGetProperty("actorName", out var name))
            {
                Assert.Equal(name.GetString(), got.ActorName);
            }

            if (want.TryGetProperty("actorUserId", out var user))
            {
                Assert.Equal(user.ValueKind == JsonValueKind.Null ? null : user.GetInt32(), got.ActorUserId);
            }

            if (want.TryGetProperty("actorEmployeeId", out var employee))
            {
                Assert.Equal(employee.ValueKind == JsonValueKind.Null ? null : employee.GetInt32(), got.ActorEmployeeId);
            }
        }
    }

    /// <summary>Everything a step may change, to prove that a refused step changed nothing.</summary>
    private static string Fingerprint(GrievanceScenario scenario)
    {
        var grievance = scenario.Case;
        return string.Join(
            '|',
            grievance.Status,
            grievance.ReturnStatus,
            grievance.IsEscalated,
            grievance.ArchivedAtUtc,
            grievance.Priority,
            grievance.TargetResolutionDate,
            grievance.RejectionReason,
            grievance.ResolutionSummary,
            grievance.ResolvedAtUtc,
            grievance.ClosedAtUtc,
            grievance.LastSubmitterUpdateAtUtc,
            grievance.CaseNo,
            grievance.CompanyId,
            string.Join(',', scenario.ActiveAssignments.Select(assignment => $"{assignment.InvestigatorEmployeeId}:{assignment.Role}:{assignment.IsActive}")),
            scenario.Proposal?.Decision);
    }

    private static GrievanceStatus? Status(string? text) => text is null ? null : GrievanceText.Status.Parse(text);

    private static bool Bool(JsonElement element, string name) => BoolOr(element, name, false);

    private static bool BoolOr(JsonElement element, string name, bool fallback) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : fallback;

    /// <summary>The text of a field; a '&lt;field&gt;Length' entry stands for that many letters 'x'.</summary>
    private static string? Text(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (element.TryGetProperty(name + "Length", out var length))
        {
            return new string('x', length.GetInt32());
        }

        return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }
}
