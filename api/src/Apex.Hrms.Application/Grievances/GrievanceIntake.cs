using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Employees;
using Apex.Hrms.Domain.Grievances;
using Apex.Hrms.Domain.Organization;
using FluentValidation;

namespace Apex.Hrms.Application.Grievances;

/// <summary>
/// HR management records a grievance an employee raised outside the system (in person, by phone, by email, by letter). Rules worth
/// knowing:
/// <list type="bullet">
/// <item>Management Create (checked by the endpoint and again here). The case is created and submitted in one transaction: it never
/// exists as a draft anybody could see.</item>
/// <item>Always named: the chosen employee becomes the submitter (grv.CaseSubmitter by employee id), so the case is in THEIR My
/// Grievances and their placement is copied, exactly as if they had submitted it. HR never records an anonymous grievance: HR knows who
/// raised it, and a pseudonym written by somebody else would link the employee to it anyway.</item>
/// <item>The employee must be current (not exited, resigned or terminated) and must not be the caller: HR raises their own grievance
/// under My Grievances, like everybody else.</item>
/// <item>The Submit history entry names the HR user (Management) and says how the grievance was received; it is visible to the
/// employee. The access log records a StatusChange "intake:Draft&gt;Submitted"; management is told (not the caller) and so is the
/// employee.</item>
/// </list>
/// Logs never carry the employee, the subject or the description.
/// </summary>
internal sealed class GrievanceIntake(
    GrievanceAccess access,
    GrievanceAuditTrail audit,
    GrievanceNotifications notifications,
    GrievanceCaseNumbers numbers,
    GrievanceCaseSteps steps,
    IGrievanceSubmitterDirectory submitters,
    IRepository<GrievanceCase> cases,
    IRepository<GrievanceCategory> categories,
    IRepository<Department> departments,
    IRepository<Employee> employees,
    IRepository<GrievanceStatusHistoryEntry> history,
    IValidator<GrievanceIntakeRequest> validator,
    IUnitOfWork unitOfWork,
    IClock clock) : IGrievanceIntake
{
    public const string NotYourself = "Use My Grievances to raise your own grievance.";

    public const string ChooseCurrentEmployee = "Choose an active employee.";

    public const string ManagementCreateOnly = "Only HR management with the create permission can record a grievance for an employee.";

    public async Task<Result<GrievanceCaseDetailDto>> RecordAsync(GrievanceIntakeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!access.HoldsManagement || !access.Has(GrievanceAccess.ManagementModule, "Create"))
        {
            return Result.Failure<GrievanceCaseDetailDto>(AppError.Forbidden(ManagementCreateOnly));
        }

        var valid = await validator.CheckAsync(request, cancellationToken);
        if (valid.IsFailure)
        {
            return Result.Failure<GrievanceCaseDetailDto>(valid.Error);
        }

        if (request.EmployeeId == access.EmployeeId)
        {
            return Result.Failure<GrievanceCaseDetailDto>(AppError.Validation(NotYourself, "employeeId", NotYourself));
        }

        var employee = await employees.FindAsync(request.EmployeeId, cancellationToken);
        if (employee is null || !IsCurrent(employee))
        {
            return Result.Failure<GrievanceCaseDetailDto>(AppError.Validation(ChooseCurrentEmployee, "employeeId", ChooseCurrentEmployee));
        }

        var category = await GrievanceDraftReferences.CheckAsync(categories, departments, request, cancellationToken);
        if (category.IsFailure)
        {
            return Result.Failure<GrievanceCaseDetailDto>(category.Error);
        }

        var today = clock.Today;
        var created = GrievanceCase.CreateDraft(GrievanceDraftReferences.Details(request), category.Value, today);
        if (created.IsFailure)
        {
            return Result.Failure<GrievanceCaseDetailDto>(created.Error);
        }

        // Everything the submission checks runs before a case number is consumed; the domain checks again when it records.
        var grievance = created.Value;
        var submittable = grievance.CheckSubmission(category.Value, request.DeclarationAccepted, today);
        if (submittable.IsFailure)
        {
            return Result.Failure<GrievanceCaseDetailDto>(submittable.Error);
        }

        var caseNo = await numbers.NextFreeAsync(cancellationToken);
        if (caseNo.IsFailure)
        {
            return Result.Failure<GrievanceCaseDetailDto>(caseNo.Error);
        }

        // The submitter row and the history entry are keyed by the case id, which the database assigns: the draft is saved first,
        // inside the same transaction, so it is never committed as a draft.
        var transaction = await unitOfWork.BeginTransactionAsync(null, cancellationToken);
        if (transaction.IsFailure)
        {
            return Result.Failure<GrievanceCaseDetailDto>(transaction.Error);
        }

        await using var scope = transaction.Value;
        cases.Add(grievance);
        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        if (saved.IsFailure)
        {
            return Result.Failure<GrievanceCaseDetailDto>(saved.Error);
        }

        submitters.Add(GrievanceCaseSubmitter.Named(grievance.Id, employee.Id));
        var placement = new GrievancePlacement(employee.CompanyId, employee.BranchId, employee.DepartmentId, employee.PositionId);
        var receivedVia = GrievanceText.IntakeChannel.Parse(request.ReceivedVia!);
        var now = clock.UtcNow.UtcDateTime;
        var step = grievance.RecordForEmployee(
            category.Value, request.DeclarationAccepted, caseNo.Value, placement, receivedVia, access.TeamActor(GrievanceActorRole.Management), today, now);
        if (step.IsFailure)
        {
            return Result.Failure<GrievanceCaseDetailDto>(step.Error);
        }

        history.Add(step.Value.Entry);
        cases.MarkModified(grievance);
        audit.Team(grievance.Id, GrievanceAuditAction.StatusChange, GrievanceActorRole.Management, $"intake:{GrievanceAuditTrail.Transition(step.Value.Entry.FromStatus, step.Value.Entry.ToStatus)}");
        var told = await notifications.ToManagementAsync(grievance, "a grievance was recorded by HR", byCaseTeam: true, cancellationToken);
        if (told.IsSuccess)
        {
            told = await notifications.ToNamedSubmitterAsync(grievance, "HR recorded a grievance for you", cancellationToken);
        }

        if (told.IsFailure)
        {
            return Result.Failure<GrievanceCaseDetailDto>(told.Error);
        }

        saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        if (saved.IsFailure)
        {
            return Result.Failure<GrievanceCaseDetailDto>(saved.Error);
        }

        await scope.CommitAsync(cancellationToken);
        return Result.Success(await steps.DetailAsync(grievance, cancellationToken));
    }

    /// <summary>As the case-team employee picker offers them: not exited, not resigned, not terminated.</summary>
    private static bool IsCurrent(Employee employee) =>
        employee.ExitDate is null
        && employee.Status != EmploymentStatus.Resigned
        && employee.Status != EmploymentStatus.Terminated;
}
