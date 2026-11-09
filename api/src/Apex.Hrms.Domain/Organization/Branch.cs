using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Domain.Organization;

public enum BranchType
{
    HeadOffice,
    Branch,
    RegionalOffice,
    Warehouse,
}

/// <summary>Converts <see cref="BranchType"/> to and from the texts used by the API and database.</summary>
public static class BranchTypeText
{
    private static readonly Dictionary<BranchType, string> Texts = new Dictionary<BranchType, string>
    {
        [BranchType.HeadOffice] = "Head Office",
        [BranchType.Branch] = "Branch",
        [BranchType.RegionalOffice] = "Regional Office",
        [BranchType.Warehouse] = "Warehouse",
    };

    public static string ToText(BranchType type) => Texts[type];

    /// <exception cref="ArgumentException">The text is not a known branch type.</exception>
    public static BranchType Parse(string? text) =>
        TryParse(text, out var type) ? type : throw new ArgumentException("Unknown branch type.", nameof(text));

    public static bool TryParse(string? text, out BranchType type)
    {
        foreach (var (candidate, candidateText) in Texts)
        {
            if (candidateText == text)
            {
                type = candidate;
                return true;
            }
        }

        type = default;
        return false;
    }
}

/// <summary>The editable fields of a branch. Status changes through Activate and Deactivate.</summary>
public sealed record BranchDetails(
    int CompanyId,
    string Code,
    string Name,
    BranchType Type,
    string? Email,
    string? Phone,
    string? Address,
    string City,
    string Country,
    int? ManagerEmployeeId,
    DateOnly OpenedDate)
{
    /// <summary>Trims text and turns blank optional text into null. Idempotent.</summary>
    public BranchDetails Normalized() => this with
    {
        Code = TextNormalization.Clean(Code),
        Name = TextNormalization.Clean(Name),
        Email = TextNormalization.CleanOptional(Email),
        Phone = TextNormalization.CleanOptional(Phone),
        Address = TextNormalization.CleanOptional(Address),
        City = TextNormalization.Clean(City),
        Country = TextNormalization.Clean(Country),
    };
}

public sealed class Branch : CodedRecord
{
    private Branch()
    {
    }

    public int CompanyId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public BranchType Type { get; private set; }

    public string? Email { get; private set; }

    public string? Phone { get; private set; }

    public string? Address { get; private set; }

    public string City { get; private set; } = string.Empty;

    public string Country { get; private set; } = string.Empty;

    public int? ManagerEmployeeId { get; private set; }

    public DateOnly OpenedDate { get; private set; }

    public static Result<Branch> Create(BranchDetails details, RecordStatus status, DateOnly today)
    {
        var branch = new Branch();
        var result = branch.Update(details, today);
        if (result.IsFailure)
        {
            return Result.Failure<Branch>(result.Error);
        }

        branch.SetInitialStatus(status);
        return Result.Success(branch);
    }

    /// <summary>Checks the rules that need no other record, without changing anything.</summary>
    public static Result Validate(BranchDetails details, DateOnly today)
    {
        var normalized = details.Normalized();
        var violations = new Violations();
        CheckCode(violations, normalized.Code);
        violations
            .Require("name", "Branch name", normalized.Name)
            .Require("city", "City", normalized.City)
            .Require("country", "Country", normalized.Country)
            .AddIf(normalized.OpenedDate > today, "openedDate", "Opened date cannot be in the future.");
        return violations.ToResult();
    }

    /// <summary>Applies the details, or changes nothing when any rule is violated. Status is left untouched.</summary>
    public Result Update(BranchDetails details, DateOnly today)
    {
        var valid = Validate(details, today);
        if (valid.IsFailure)
        {
            return valid;
        }

        var normalized = details.Normalized();
        AssignCode(normalized.Code);
        CompanyId = normalized.CompanyId;
        Name = normalized.Name;
        Type = normalized.Type;
        Email = normalized.Email;
        Phone = normalized.Phone;
        Address = normalized.Address;
        City = normalized.City;
        Country = normalized.Country;
        ManagerEmployeeId = normalized.ManagerEmployeeId;
        OpenedDate = normalized.OpenedDate;
        return Result.Success();
    }
}
