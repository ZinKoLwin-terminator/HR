using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Grievances;
using FluentValidation;

namespace Apex.Hrms.Application.Grievances;

// Shape rules of the grievance requests (required, lengths after trimming, enum texts, the row version). The domain keeps the
// business rules (the minimum lengths at submission, dates, statuses, who may decide) and runs again on every change.

internal static class GrievanceRuleExtensions
{
    public const int ReasonMax = GrievanceStatusHistoryEntry.ReasonMaxLength;

    public static IRuleBuilderOptions<T, string?> RequiredReason<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(text => !string.IsNullOrWhiteSpace(text)).WithMessage("A reason is required.")
            .OptionalText(ReasonMax);

    public static IRuleBuilderOptions<T, string?> OneOf<T, TEnum>(this IRuleBuilder<T, string?> rule, TextMap<TEnum> map, string message)
        where TEnum : struct, Enum =>
        rule.Must(text => map.TryParse(text, out _)).WithMessage(message);
}

internal abstract class GrievanceDraftValidator<T> : RequestValidator<T>
    where T : IGrievanceDraftFields
{
    protected GrievanceDraftValidator()
    {
        RuleFor(x => x.CategoryId).GreaterThan(0).WithMessage("Choose a category.");
        RuleForText(x => x.Subject, "Subject").RequiredText(GrievanceCase.SubjectMaxLength);
        RuleForText(x => x.Description, "Description").OptionalText(GrievanceCase.DescriptionMaxLength);
        RuleForText(x => x.Location, "Location").OptionalText(GrievanceCase.ShortTextMaxLength);
        RuleForText(x => x.InvolvedPersonText, "Person involved").OptionalText(GrievanceCase.ShortTextMaxLength);
        RuleFor(x => x.InvolvedDepartmentId).GreaterThan(0).When(x => x.InvolvedDepartmentId is not null).WithMessage("Choose a department.");
    }
}

internal sealed class GrievanceDraftCreateRequestValidator : GrievanceDraftValidator<GrievanceDraftCreateRequest>;

internal sealed class GrievanceDraftUpdateRequestValidator : GrievanceDraftValidator<GrievanceDraftUpdateRequest>
{
    public GrievanceDraftUpdateRequestValidator() => RuleForText(x => x.RowVersion).RequiredRowVersion();
}

internal sealed class GrievanceIntakeRequestValidator : GrievanceDraftValidator<GrievanceIntakeRequest>
{
    public const string ChooseChannel = "Choose how the grievance was received.";

    public const string ConfirmDeclaration = "Confirm that the employee gave this account and agreed that HR records it.";

    public GrievanceIntakeRequestValidator()
    {
        RuleFor(x => x.EmployeeId).GreaterThan(0).WithMessage("Choose an employee.");
        RuleFor(x => x.ReceivedVia).OneOf(GrievanceText.IntakeChannel, ChooseChannel);
        RuleFor(x => x.DeclarationAccepted).Equal(true).WithMessage(ConfirmDeclaration);
    }
}

internal sealed class SubmitGrievanceRequestValidator : RequestValidator<SubmitGrievanceRequest>
{
    public SubmitGrievanceRequestValidator() => RuleForText(x => x.RowVersion).RequiredRowVersion();
}

internal sealed class MyGrievanceMessageRequestValidator : RequestValidator<MyGrievanceMessageRequest>
{
    public MyGrievanceMessageRequestValidator()
    {
        RuleForText(x => x.RowVersion).RequiredRowVersion();
        RuleForText(x => x.Body, "Message").RequiredText(GrievanceCommunication.BodyMaxLength);
    }
}

internal sealed class AcknowledgeGrievanceCommandValidator : RequestValidator<AcknowledgeGrievanceCommand>
{
    public AcknowledgeGrievanceCommandValidator()
    {
        RuleFor(x => x.Priority).OneOf(GrievanceText.Priority, "Choose a priority: Low, Medium, High or Critical.");
        RuleForText(x => x.Comment, "Comment").OptionalText(GrievanceRuleExtensions.ReasonMax);
    }
}

internal sealed class StartGrievanceReviewCommandValidator : RequestValidator<StartGrievanceReviewCommand>
{
    public StartGrievanceReviewCommandValidator() => RuleForText(x => x.Comment, "Comment").OptionalText(GrievanceRuleExtensions.ReasonMax);
}

internal sealed class StartGrievanceInvestigationCommandValidator : RequestValidator<StartGrievanceInvestigationCommand>
{
    public StartGrievanceInvestigationCommandValidator() => RuleForText(x => x.Comment, "Comment").OptionalText(GrievanceRuleExtensions.ReasonMax);
}

internal sealed class CloseGrievanceCommandValidator : RequestValidator<CloseGrievanceCommand>
{
    public CloseGrievanceCommandValidator() => RuleForText(x => x.Comment, "Comment").OptionalText(GrievanceRuleExtensions.ReasonMax);
}

internal sealed class ArchiveGrievanceCommandValidator : RequestValidator<ArchiveGrievanceCommand>
{
    public ArchiveGrievanceCommandValidator() => RuleForText(x => x.Comment, "Comment").OptionalText(GrievanceRuleExtensions.ReasonMax);
}

internal sealed class AssignGrievanceInvestigatorCommandValidator : RequestValidator<AssignGrievanceInvestigatorCommand>
{
    public AssignGrievanceInvestigatorCommandValidator()
    {
        RuleFor(x => x.InvestigatorEmployeeId).GreaterThan(0).WithMessage("Choose an investigator.");
        RuleFor(x => x.Role).OneOf(GrievanceText.AssignmentRole, "Choose the role: Lead or Member.");
        RuleForText(x => x.Reason, "Reason").OptionalText(GrievanceAssignment.ReasonMaxLength);
    }
}

internal sealed class UnassignGrievanceInvestigatorCommandValidator : RequestValidator<UnassignGrievanceInvestigatorCommand>
{
    public UnassignGrievanceInvestigatorCommandValidator()
    {
        RuleFor(x => x.AssignmentId).GreaterThan(0).WithMessage("Choose the assignment to end.");
        RuleForText(x => x.Reason, "Reason").OptionalText(GrievanceRuleExtensions.ReasonMax);
    }
}

internal sealed class RequestGrievanceInfoCommandValidator : RequestValidator<RequestGrievanceInfoCommand>
{
    public RequestGrievanceInfoCommandValidator() =>
        RuleForText(x => x.Message, "Message").RequiredText(GrievanceCommunication.InfoRequestMaxLength);
}

internal sealed class ProposeGrievanceResolutionCommandValidator : RequestValidator<ProposeGrievanceResolutionCommand>
{
    public ProposeGrievanceResolutionCommandValidator() =>
        RuleForText(x => x.Summary, "Resolution").RequiredText(GrievanceResolutionProposal.SummaryMaxLength);
}

internal sealed class ApproveGrievanceResolutionCommandValidator : RequestValidator<ApproveGrievanceResolutionCommand>
{
    public ApproveGrievanceResolutionCommandValidator() =>
        RuleForText(x => x.Comment, "Comment").OptionalText(GrievanceResolutionProposal.CommentMaxLength);
}

internal sealed class RejectGrievanceResolutionCommandValidator : RequestValidator<RejectGrievanceResolutionCommand>
{
    public RejectGrievanceResolutionCommandValidator() => RuleForText(x => x.Reason, "Reason").RequiredReason();
}

internal sealed class RejectGrievanceCommandValidator : RequestValidator<RejectGrievanceCommand>
{
    public RejectGrievanceCommandValidator() => RuleForText(x => x.Reason, "Reason").RequiredReason();
}

internal sealed class ReopenGrievanceCommandValidator : RequestValidator<ReopenGrievanceCommand>
{
    public ReopenGrievanceCommandValidator() => RuleForText(x => x.Reason, "Reason").RequiredReason();
}

internal sealed class EscalateGrievanceCommandValidator : RequestValidator<EscalateGrievanceCommand>
{
    public EscalateGrievanceCommandValidator() => RuleForText(x => x.Reason, "Reason").RequiredReason();
}

internal sealed class TriageGrievanceCommandValidator : RequestValidator<TriageGrievanceCommand>
{
    public TriageGrievanceCommandValidator()
    {
        RuleFor(x => x.CategoryId).GreaterThan(0).WithMessage("Choose a category.");
        RuleFor(x => x.Priority).OneOf(GrievanceText.Priority, "Choose a priority: Low, Medium, High or Critical.").When(x => x.Priority is not null);
        RuleFor(x => x.SubjectEmployeeId).GreaterThan(0).When(x => x.SubjectEmployeeId is not null).WithMessage("Choose an employee.");
        RuleFor(x => x.InvolvedDepartmentId).GreaterThan(0).When(x => x.InvolvedDepartmentId is not null).WithMessage("Choose a department.");
        RuleFor(x => x.LegalHold).NotNull().WithMessage("Say whether the case is on legal hold.");
    }
}

internal sealed class SendGrievanceCaseMessageCommandValidator : RequestValidator<SendGrievanceCaseMessageCommand>
{
    public SendGrievanceCaseMessageCommandValidator() =>
        RuleForText(x => x.Body, "Message").RequiredText(GrievanceCommunication.BodyMaxLength);
}

internal sealed class AddGrievanceNoteCommandValidator : RequestValidator<AddGrievanceNoteCommand>
{
    public AddGrievanceNoteCommandValidator()
    {
        RuleFor(x => x.Kind).OneOf(GrievanceText.NoteKind, "Choose the kind: Note, Interview, FollowUp, Finding or Recommendation.");
        RuleForText(x => x.Body, "Note").RequiredText(GrievanceInvestigationNote.BodyMaxLength);
        RuleForText(x => x.IntervieweeText, "Interviewee").OptionalText(GrievanceInvestigationNote.IntervieweeMaxLength);
    }
}

internal sealed class GrievanceCorrectiveActionCreateRequestValidator : RequestValidator<GrievanceCorrectiveActionCreateRequest>
{
    public GrievanceCorrectiveActionCreateRequestValidator()
    {
        RuleForText(x => x.RowVersion).RequiredRowVersion();
        RuleForText(x => x.ActionType, "Action type").RequiredText(GrievanceCorrectiveAction.ActionTypeMaxLength);
        RuleForText(x => x.Description, "Description").RequiredText(GrievanceCorrectiveAction.DescriptionMaxLength);
        RuleForText(x => x.ResponsibleText, "Responsible").OptionalText(GrievanceCorrectiveAction.ResponsibleMaxLength);
    }
}

internal sealed class GrievanceCorrectiveActionUpdateRequestValidator : RequestValidator<GrievanceCorrectiveActionUpdateRequest>
{
    public GrievanceCorrectiveActionUpdateRequestValidator()
    {
        RuleForText(x => x.RowVersion).RequiredRowVersion();
        RuleForText(x => x.ActionType, "Action type").RequiredText(GrievanceCorrectiveAction.ActionTypeMaxLength);
        RuleForText(x => x.Description, "Description").RequiredText(GrievanceCorrectiveAction.DescriptionMaxLength);
        RuleForText(x => x.ResponsibleText, "Responsible").OptionalText(GrievanceCorrectiveAction.ResponsibleMaxLength);
        RuleFor(x => x.Status).OneOf(GrievanceText.CorrectiveActionStatus, "Choose a status: Proposed, Approved, Rejected or Implemented.");
    }
}
