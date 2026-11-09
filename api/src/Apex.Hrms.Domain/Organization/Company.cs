using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Domain.Organization;

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
    DateOnly FoundedDate,
    RecordStatus Status);

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

    public static Result<Company> Create(CompanyDetails details, DateOnly today)
    {
        var company = new Company();
        var result = company.Update(details, today);
        return result.IsFailure ? Result.Failure<Company>(result.Error) : Result.Success(company);
    }

    /// <summary>Applies the details, or changes nothing when any rule is violated.</summary>
    public Result Update(CompanyDetails details, DateOnly today)
    {
        var violations = new Violations();
        CheckCode(violations, details.Code);
        violations
            .Require("name", "Company name", details.Name)
            .Require("legalName", "Legal name", details.LegalName)
            .Require("industry", "Industry", details.Industry)
            .Require("email", "Email", details.Email)
            .Require("phone", "Phone", details.Phone)
            .Require("city", "City", details.City)
            .Require("country", "Country", details.Country)
            .AddIf(details.FoundedDate > today, "foundedDate", "Founded date cannot be in the future.");
        if (violations.Any)
        {
            return violations.ToResult();
        }

        AssignCodeAndStatus(details.Code, details.Status);
        Name = TextNormalization.Clean(details.Name);
        LegalName = TextNormalization.Clean(details.LegalName);
        RegistrationNumber = TextNormalization.CleanOptional(details.RegistrationNumber);
        TaxId = TextNormalization.CleanOptional(details.TaxId);
        Industry = TextNormalization.Clean(details.Industry);
        Email = TextNormalization.Clean(details.Email);
        Phone = TextNormalization.Clean(details.Phone);
        Website = TextNormalization.CleanOptional(details.Website);
        Address = TextNormalization.CleanOptional(details.Address);
        City = TextNormalization.Clean(details.City);
        Country = TextNormalization.Clean(details.Country);
        FoundedDate = details.FoundedDate;
        return Result.Success();
    }
}
