using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Employees;
using FluentValidation;

namespace Apex.Hrms.Application.Employees;

/// <summary>Shape rules of the employment fields. Labels match the domain wording.</summary>
internal abstract class EmployeeFieldsValidator<TRequest> : RequestValidator<TRequest>
    where TRequest : IEmployeeFields
{
    protected EmployeeFieldsValidator()
    {
        RuleForText(x => x.FirstName, "First name").RequiredText(60);
        RuleForText(x => x.LastName, "Last name").RequiredText(60);
        RuleForText(x => x.Gender, "Gender")
            .NotEmpty().WithMessage("Gender is required.")
            .Must(text => GenderText.TryParse(text?.Trim(), out _))
            .WithMessage($"Gender must be {GenderText.Map.OneOf}.");
        RuleForText(x => x.WorkEmail, "Work email").RequiredText(120).ValidEmail();
        RuleForText(x => x.PhotoUrl, "Photo").OptionalText(400);
        RuleFor(x => x.CompanyId).GreaterThan(0).WithMessage("Company is required.");
        RuleFor(x => x.BranchId).GreaterThan(0).WithMessage("Branch is required.");
        RuleFor(x => x.DepartmentId).GreaterThan(0).WithMessage("Department is required.");
        RuleFor(x => x.SectionId).GreaterThan(0).WithMessage("Choose an existing section.");
        RuleFor(x => x.PositionId).GreaterThan(0).WithMessage("Position is required.");
        RuleFor(x => x.JobGradeId).GreaterThan(0).WithMessage("Job grade is required.");
        RuleFor(x => x.ManagerId).GreaterThan(0).WithMessage("Choose an existing manager.");
        RuleFor(x => x.ShiftId).GreaterThan(0).WithMessage("Choose an existing shift.");
        RuleForText(x => x.EmploymentType, "Employment type")
            .NotEmpty().WithMessage("Employment type is required.")
            .Must(text => EmploymentTypeText.TryParse(text?.Trim(), out _))
            .WithMessage($"Employment type must be {EmploymentTypeText.Map.OneOf}.");
        RuleFor(x => x.JoiningDate).NotEmpty().WithMessage("Joining date is required.");
    }

    /// <summary>The code is stored upper case, so lower case input is accepted and checked after normalizing.</summary>
    protected static bool IsValidCode(string? code) =>
        string.IsNullOrWhiteSpace(code) || RecordCodeRules.IsValid(code.Trim().ToUpperInvariant());
}

/// <summary>Shape rules of the personal fields (create and personal update).</summary>
internal class PersonalFieldsValidator<TRequest> : RequestValidator<TRequest>
    where TRequest : IPersonalFields
{
    public PersonalFieldsValidator()
    {
        RuleFor(x => x.DateOfBirth).NotEmpty().WithMessage("Date of birth is required.");
        RuleForText(x => x.MaritalStatus, "Marital status")
            .NotEmpty().WithMessage("Marital status is required.")
            .Must(text => MaritalStatusText.TryParse(text?.Trim(), out _))
            .WithMessage($"Marital status must be {MaritalStatusText.Map.OneOf}.");
        RuleForText(x => x.Nationality, "Nationality").RequiredText(60);
        RuleForText(x => x.NationalId, "National ID").RequiredText(40);
        RuleForText(x => x.PersonalEmail, "Personal email").OptionalText(120).ValidEmail();
        RuleForText(x => x.Phone, "Phone").RequiredText(25).OptionalPhone();
        RuleForText(x => x.AlternatePhone, "Alternate phone").OptionalText(25).OptionalPhone();
        RuleFor(x => x.Address).NotNull().WithMessage("Address is required.");
        RuleFor(x => x.Address!).SetValidator(new AddressValidator()).When(x => x.Address is not null);
        RuleFor(x => x.EmergencyContact).NotNull().WithMessage("Emergency contact is required.");
        RuleFor(x => x.EmergencyContact!).SetValidator(new EmergencyContactValidator()).When(x => x.EmergencyContact is not null);
    }
}

internal sealed class AddressValidator : RequestValidator<AddressRequest>
{
    public AddressValidator()
    {
        RuleForText(x => x.Line1, "Address line 1").RequiredText(120);
        RuleForText(x => x.Line2, "Address line 2").OptionalText(120);
        RuleForText(x => x.City, "City").RequiredText(60);
        RuleForText(x => x.State, "State").OptionalText(60);
        RuleForText(x => x.PostalCode, "Postal code").OptionalText(12);
        RuleForText(x => x.Country, "Country").RequiredText(60);
    }
}

internal sealed class EmergencyContactValidator : RequestValidator<EmergencyContactRequest>
{
    public EmergencyContactValidator()
    {
        RuleForText(x => x.Name, "Emergency contact name").RequiredText(80);
        RuleForText(x => x.Relationship, "Relationship")
            .NotEmpty().WithMessage("Relationship is required.")
            .Must(text => EmergencyRelationshipText.TryParse(text?.Trim(), out _))
            .WithMessage($"Relationship must be {EmergencyRelationshipText.Map.OneOf}.");
        RuleForText(x => x.Phone, "Emergency contact phone").RequiredText(25).OptionalPhone();
    }
}

internal sealed class EmployeeCreateRequestValidator : EmployeeFieldsValidator<EmployeeCreateRequest>
{
    public EmployeeCreateRequestValidator()
    {
        RuleForText(x => x.EmployeeCode, "Employee code")
            .OptionalText(Employee.CodeMaxLength)
            .Must(IsValidCode).WithMessage(RecordCodeRules.InvalidMessage);
        RuleForText(x => x.EmploymentStatus, "Employment status")
            .NotEmpty().WithMessage("Employment status is required.")
            .Must(text => EmploymentStatusText.TryParse(text?.Trim(), out var status) && !status.HasLeft())
            .WithMessage("Employment status must be Active, Probation, On Leave or Suspended. Use the exit command when an employee leaves.");

        // The personal rules are shared with the personal update; the child validator reports the same field names.
        RuleFor(x => x).SetValidator(new PersonalFieldsValidator<EmployeeCreateRequest>());
    }
}

internal sealed class EmployeeUpdateRequestValidator : EmployeeFieldsValidator<EmployeeUpdateRequest>
{
    public EmployeeUpdateRequestValidator()
    {
        RuleForText(x => x.EmployeeCode, "Employee code")
            .RequiredText(Employee.CodeMaxLength)
            .Must(IsValidCode).WithMessage(RecordCodeRules.InvalidMessage);
        RuleForText(x => x.RowVersion).RequiredRowVersion();
    }
}

internal sealed class EmployeePersonalUpdateRequestValidator : PersonalFieldsValidator<EmployeePersonalUpdateRequest>
{
    public EmployeePersonalUpdateRequestValidator() => RuleForText(x => x.RowVersion).RequiredRowVersion();
}

internal sealed class BankAccountUpdateRequestValidator : RequestValidator<BankAccountUpdateRequest>
{
    public BankAccountUpdateRequestValidator()
    {
        RuleForText(x => x.BankName, "Bank name").RequiredText(60);
        RuleForText(x => x.BranchName, "Bank branch").OptionalText(60);
        RuleForText(x => x.AccountName, "Account holder name").RequiredText(80);
        RuleForText(x => x.AccountNumber, "Account number").RequiredText(20);
    }
}
