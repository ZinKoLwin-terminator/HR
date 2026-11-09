using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Domain.Organization;

/// <summary>The editable fields of a company. Status is not part of it: it changes through Activate and Deactivate.</summary>
public sealed record CompanyDetails(
    string Code,
    string Name,
    string LegalName,
    string? RegistrationNumber,
    string? TaxId,
    string Industry,
    string Email,
    string Phone,
    string? Website,
    string? Address,
    string City,
    string Country,
    DateOnly FoundedDate)
{
    /// <summary>Trims text and turns blank optional text into null. Idempotent.</summary>
    public CompanyDetails Normalized() => this with
    {
        Code = TextNormalization.Clean(Code),
        Name = TextNormalization.Clean(Name),
        LegalName = TextNormalization.Clean(LegalName),
        RegistrationNumber = TextNormalization.CleanOptional(RegistrationNumber),
        TaxId = TextNormalization.CleanOptional(TaxId),
        Industry = TextNormalization.Clean(Industry),
        Email = TextNormalization.Clean(Email),
        Phone = TextNormalization.Clean(Phone),
        Website = TextNormalization.CleanOptional(Website),
        Address = TextNormalization.CleanOptional(Address),
        City = TextNormalization.Clean(City),
        Country = TextNormalization.Clean(Country),
    };
}

public sealed class Company : CodedRecord
{
    private Company()
    {
    }

    public string Name { get; private set; } = string.Empty;

    public string LegalName { get; private set; } = string.Empty;

    public string? RegistrationNumber { get; private set; }

    public string? TaxId { get; private set; }

    public string Industry { get; private set; } = string.Empty;

    public string Email { get; private set; } = string.Empty;

    public string Phone { get; private set; } = string.Empty;

    public string? Website { get; private set; }

    public string? Address { get; private set; }

    public string City { get; private set; } = string.Empty;

    public string Country { get; private set; } = string.Empty;

    public DateOnly FoundedDate { get; private set; }

    public static Result<Company> Create(CompanyDetails details, RecordStatus status, DateOnly today)
    {
        var company = new Company();
        var result = company.Update(details, today);
        if (result.IsFailure)
        {
            return Result.Failure<Company>(result.Error);
        }

        company.SetInitialStatus(status);
        return Result.Success(company);
    }

    /// <summary>Checks the rules that need no other record, without changing anything.</summary>
    public static Result Validate(CompanyDetails details, DateOnly today)
    {
        var normalized = details.Normalized();
        var violations = new Violations();
        CheckCode(violations, normalized.Code);
        violations
            .Require("name", "Company name", normalized.Name)
            .Require("legalName", "Legal name", normalized.LegalName)
            .Require("industry", "Industry", normalized.Industry)
            .Require("email", "Email", normalized.Email)
            .Require("phone", "Phone", normalized.Phone)
            .Require("city", "City", normalized.City)
            .Require("country", "Country", normalized.Country)
            .AddIf(normalized.FoundedDate > today, "foundedDate", "Founded date cannot be in the future.");
        return violations.ToResult();
    }

    /// <summary>Applies the details, or changes nothing when any rule is violated. Status is left untouched.</summary>
    public Result Update(CompanyDetails details, DateOnly today)
    {
        var valid = Validate(details, today);
        if (valid.IsFailure)
        {
            return valid;
        }

        var normalized = details.Normalized();
        AssignCode(normalized.Code);
        Name = normalized.Name;
        LegalName = normalized.LegalName;
        RegistrationNumber = normalized.RegistrationNumber;
        TaxId = normalized.TaxId;
        Industry = normalized.Industry;
        Email = normalized.Email;
        Phone = normalized.Phone;
        Website = normalized.Website;
        Address = normalized.Address;
        City = normalized.City;
        Country = normalized.Country;
        FoundedDate = normalized.FoundedDate;
        return Result.Success();
    }
}
