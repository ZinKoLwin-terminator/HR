using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Domain.Employees;

/// <summary>
/// An employee (hr.Employee). The aggregate owns its salary bank account and its history log. Personal data is kept
/// in properties of its own (see <see cref="PersonalDetails"/>) so the Application layer can keep it out of every
/// response that does not need it.
/// </summary>
public sealed class Employee : AuditedEntity
{
    public const int CodeMaxLength = 12;
    public const int MinimumAge = 18;

    private readonly List<EmploymentHistoryEntry> history = [];

    private Employee()
    {
    }

    public string EmployeeCode { get; private set; } = string.Empty;

    public string FirstName { get; private set; } = string.Empty;

    public string LastName { get; private set; } = string.Empty;

    public Gender Gender { get; private set; }

    public string WorkEmail { get; private set; } = string.Empty;

    public string? PhotoUrl { get; private set; }

    // ---- Personal data ----

    public DateOnly DateOfBirth { get; private set; }

    public MaritalStatus MaritalStatus { get; private set; }

    public string Nationality { get; private set; } = string.Empty;

    public string NationalId { get; private set; } = string.Empty;

    public string? PersonalEmail { get; private set; }

    public string Phone { get; private set; } = string.Empty;

    public string? AlternatePhone { get; private set; }

    public PostalAddress Address { get; private set; } = new(string.Empty, null, string.Empty, null, null, string.Empty);

    public EmergencyContact EmergencyContact { get; private set; } = new(string.Empty, EmergencyRelationship.Other, string.Empty);

    // ---- Placement ----

    public int CompanyId { get; private set; }

    public int BranchId { get; private set; }

    public int DepartmentId { get; private set; }

    public int? SectionId { get; private set; }

    public int PositionId { get; private set; }

    public int JobGradeId { get; private set; }

    public int? ManagerEmployeeId { get; private set; }

    public int? ShiftId { get; private set; }

    // ---- Employment ----

    public EmploymentType EmploymentType { get; private set; }

    public EmploymentStatus Status { get; private set; }

    public DateOnly JoiningDate { get; private set; }

    public DateOnly? ProbationEndDate { get; private set; }

    public DateOnly? ExitDate { get; private set; }

    public EmployeeBankAccount? BankAccount { get; private set; }

    public IReadOnlyCollection<EmploymentHistoryEntry> History => history;

    public bool HasLeft => Status.HasLeft();

    /// <summary>Whole years between the date of birth and <paramref name="today"/>.</summary>
    public static int AgeOn(DateOnly dateOfBirth, DateOnly today)
    {
        var age = today.Year - dateOfBirth.Year;
        return dateOfBirth > today.AddYears(-age) ? age - 1 : age;
    }

    /// <summary>
    /// Creates an employee with a resolved <paramref name="code"/> and records the "Joined" history entry. A new
    /// employee starts in a current status; leaving is recorded with <see cref="Exit"/>.
    /// </summary>
    public static Result<Employee> Create(
        string code,
        EmployeeDetails details,
        PersonalDetails personal,
        EmploymentStatus status,
        ChangeContext context,
        string? positionTitle)
    {
        var employee = new Employee();
        var violations = new Violations()
            .AddIf(status.HasLeft(), "employmentStatus", "A new employee cannot start as Resigned or Terminated.");
        var coded = details with { EmployeeCode = code };
        var valid = Validate(coded);
        var personalValid = ValidatePersonal(personal, context.Today);
        var failure = Merge(violations.ToResult(), valid, personalValid);
        if (failure is not null)
        {
            return Result.Failure<Employee>(failure);
        }

        employee.ApplyDetails(coded.Normalized());
        employee.ApplyPersonal(personal.Normalized());
        employee.Status = status;
        employee.history.Add(EmploymentHistoryEntry.Record(
            HistoryEventType.Joined,
            $"Joined as {positionTitle ?? "employee"}",
            $"{EmploymentTypeText.ToText(employee.EmploymentType)} employee. Record created.",
            context));
        return Result.Success(employee);
    }

    /// <summary>Checks the rules of the employment details that need no other record.</summary>
    public static Result Validate(EmployeeDetails details)
    {
        var normalized = details.Normalized();
        var violations = new Violations();
        if (normalized.EmployeeCode is { } code)
        {
            violations.AddIf(!RecordCodeRules.IsValid(code), "employeeCode", RecordCodeRules.InvalidMessage);
        }

        violations
            .Require("firstName", "First name", normalized.FirstName)
            .Require("lastName", "Last name", normalized.LastName)
            .Require("workEmail", "Work email", normalized.WorkEmail)
            .AddIf(
                normalized.ProbationEndDate is { } probationEnd && probationEnd < normalized.JoiningDate,
                "probationEndDate",
                "The probation cannot end before the joining date.");
        return violations.ToResult();
    }

    /// <summary>Checks the personal data: required values, and that the employee is at least 18 on <paramref name="today"/>.</summary>
    public static Result ValidatePersonal(PersonalDetails personal, DateOnly today)
    {
        var normalized = personal.Normalized();
        return new Violations()
            .AddIf(normalized.DateOfBirth > today, "dateOfBirth", "The date of birth cannot be in the future.")
            .AddIf(
                normalized.DateOfBirth <= today && AgeOn(normalized.DateOfBirth, today) < MinimumAge,
                "dateOfBirth",
                "Employees must be at least 18 years old.")
            .Require("nationality", "Nationality", normalized.Nationality)
            .Require("nationalId", "National ID", normalized.NationalId)
            .Require("phone", "Phone", normalized.Phone)
            .Require("address.line1", "Address line 1", normalized.Address.Line1)
            .Require("address.city", "City", normalized.Address.City)
            .Require("address.country", "Country", normalized.Address.Country)
            .Require("emergencyContact.name", "Emergency contact name", normalized.EmergencyContact.Name)
            .Require("emergencyContact.phone", "Emergency contact phone", normalized.EmergencyContact.Phone)
            .ToResult();
    }

    /// <summary>
    /// Applies the employment details and records a history entry for a position, organization or contract change.
    /// Changes nothing when a rule is violated. Status and exit date are left untouched (see the status commands).
    /// </summary>
    public Result Update(EmployeeDetails details, ChangeContext context, PlacementNotes notes)
    {
        var valid = Validate(details);
        if (valid.IsFailure)
        {
            return valid;
        }

        var normalized = details.Normalized();
        if (ExitDate is { } exit && normalized.JoiningDate > exit)
        {
            return Result.Failure(AppError.Validation(
                "The joining date cannot be after the exit date.",
                "joiningDate",
                "The joining date cannot be after the exit date."));
        }

        var before = (PositionId, DepartmentId, BranchId, EmploymentType);
        ApplyDetails(normalized with { EmployeeCode = normalized.EmployeeCode ?? EmployeeCode });
        RecordPlacementChanges(before, context, notes);
        return Result.Success();
    }

    /// <summary>Applies the personal data, or changes nothing when a rule is violated.</summary>
    public Result UpdatePersonal(PersonalDetails personal, DateOnly today)
    {
        var valid = ValidatePersonal(personal, today);
        if (valid.IsFailure)
        {
            return valid;
        }

        ApplyPersonal(personal.Normalized());
        return Result.Success();
    }

    /// <summary>Moves between the current statuses (Active, Probation, On Leave, Suspended). Same status is a no-op.</summary>
    public Result ChangeStatus(EmploymentStatus status, ChangeContext context)
    {
        if (status.HasLeft())
        {
            return Result.Failure(AppError.Validation(
                "Use the exit command to record that an employee left.", "status", "Choose a current status."));
        }

        if (HasLeft)
        {
            return Result.Failure(AppError.Conflict("The employee has left. Reinstate the employee first."));
        }

        if (Status == status)
        {
            return Result.Success();
        }

        RecordStatusChange(HistoryEventType.StatusChange, status, context);
        Status = status;
        return Result.Success();
    }

    /// <summary>Records that the employee left: status Resigned or Terminated and an exit date on or after the joining date.</summary>
    public Result Exit(DateOnly exitDate, EmploymentStatus status, ChangeContext context)
    {
        if (!status.HasLeft())
        {
            return Result.Failure(AppError.Validation(
                "Status must be Resigned or Terminated.", "status", "Choose Resigned or Terminated."));
        }

        if (HasLeft)
        {
            return Result.Failure(AppError.Conflict("The employee has already left."));
        }

        if (exitDate < JoiningDate)
        {
            return Result.Failure(AppError.Validation(
                "The exit date cannot be before the joining date.",
                "exitDate",
                "The exit date cannot be before the joining date."));
        }

        RecordStatusChange(HistoryEventType.Exit, status, context);
        Status = status;
        ExitDate = exitDate;
        return Result.Success();
    }

    /// <summary>Takes a former employee back into a current status and clears the exit date.</summary>
    public Result Reinstate(EmploymentStatus status, ChangeContext context)
    {
        if (!HasLeft)
        {
            return Result.Failure(AppError.Conflict("The employee has not left."));
        }

        if (status.HasLeft())
        {
            return Result.Failure(AppError.Validation(
                "Choose a current status to reinstate to.", "status", "Choose a current status."));
        }

        history.Add(EmploymentHistoryEntry.Record(
            HistoryEventType.StatusChange,
            $"Reinstated as {EmploymentStatusText.ToText(status)}",
            $"Previously {EmploymentStatusText.ToText(Status)}.",
            context));
        Status = status;
        ExitDate = null;
        return Result.Success();
    }

    /// <summary>Sets or replaces the salary bank account. The caller passes the row version check of the account.</summary>
    public Result<EmployeeBankAccount> SetBankAccount(BankDetails details)
    {
        if (BankAccount is { } existing)
        {
            var applied = existing.Apply(details);
            return applied.IsFailure ? Result.Failure<EmployeeBankAccount>(applied.Error) : Result.Success(existing);
        }

        var created = EmployeeBankAccount.Create(details);
        if (created.IsSuccess)
        {
            BankAccount = created.Value;
        }

        return created;
    }

    private static AppError? Merge(params Result[] results) =>
        results.Where(result => result.IsFailure).Select(result => result.Error).ToList() switch
        {
            [] => null,
            [var only] => only,
            var many => MergeErrors(many),
        };

    private static AppError MergeErrors(List<AppError> errors)
    {
        var violations = new Violations();
        foreach (var (field, messages) in errors.SelectMany(error => error.FieldErrors ?? new Dictionary<string, string[]>()))
        {
            foreach (var message in messages)
            {
                violations.Add(field, message);
            }
        }

        return violations.ToError();
    }

    private void ApplyDetails(EmployeeDetails details)
    {
        EmployeeCode = details.EmployeeCode!;
        FirstName = details.FirstName;
        LastName = details.LastName;
        Gender = details.Gender;
        WorkEmail = details.WorkEmail;
        PhotoUrl = details.PhotoUrl;
        CompanyId = details.CompanyId;
        BranchId = details.BranchId;
        DepartmentId = details.DepartmentId;
        SectionId = details.SectionId;
        PositionId = details.PositionId;
        JobGradeId = details.JobGradeId;
        ManagerEmployeeId = details.ManagerId;
        ShiftId = details.ShiftId;
        EmploymentType = details.EmploymentType;
        JoiningDate = details.JoiningDate;
        ProbationEndDate = details.ProbationEndDate;
    }

    private void ApplyPersonal(PersonalDetails personal)
    {
        DateOfBirth = personal.DateOfBirth;
        MaritalStatus = personal.MaritalStatus;
        Nationality = personal.Nationality;
        NationalId = personal.NationalId;
        PersonalEmail = personal.PersonalEmail;
        Phone = personal.Phone;
        AlternatePhone = personal.AlternatePhone;
        Address = personal.Address;
        EmergencyContact = personal.EmergencyContact;
    }

    private void RecordStatusChange(HistoryEventType type, EmploymentStatus next, ChangeContext context) =>
        history.Add(EmploymentHistoryEntry.Record(
            type,
            $"Status changed to {EmploymentStatusText.ToText(next)}",
            $"Previously {EmploymentStatusText.ToText(Status)}.",
            context));

    private void RecordPlacementChanges(
        (int PositionId, int DepartmentId, int BranchId, EmploymentType EmploymentType) before,
        ChangeContext context,
        PlacementNotes notes)
    {
        if (before.PositionId != PositionId)
        {
            var previous = notes.PreviousPositionTitle ?? $"#{before.PositionId}";
            var current = notes.NewPositionTitle ?? $"#{PositionId}";
            history.Add(EmploymentHistoryEntry.Record(
                notes.IsPromotion ? HistoryEventType.Promotion : HistoryEventType.Transfer,
                $"{(notes.IsPromotion ? "Promoted" : "Moved")}: {previous} → {current}",
                "Position changed.",
                context));
        }
        else if (before.DepartmentId != DepartmentId || before.BranchId != BranchId)
        {
            history.Add(EmploymentHistoryEntry.Record(
                HistoryEventType.Transfer,
                "Organization assignment changed",
                "Branch or department updated.",
                context));
        }

        if (before.EmploymentType != EmploymentType)
        {
            history.Add(EmploymentHistoryEntry.Record(
                HistoryEventType.ContractChange,
                $"Employment type changed to {EmploymentTypeText.ToText(EmploymentType)}",
                $"Previously {EmploymentTypeText.ToText(before.EmploymentType)}.",
                context));
        }
    }
}
