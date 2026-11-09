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
    DateOnly OpenedDate,
    RecordStatus Status);

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

    public static Result<Branch> Create(BranchDetails details, DateOnly today)
    {
        var branch = new Branch();
        var result = branch.Update(details, today);
        return result.IsFailure ? Result.Failure<Branch>(result.Error) : Result.Success(branch);
    }

    /// <summary>Applies the details, or changes nothing when any rule is violated.</summary>
    public Result Update(BranchDetails details, DateOnly today)
    {
        var violations = new Violations();
        CheckCode(violations, details.Code);
        violations
            .Require("name", "Branch name", details.Name)
            .Require("city", "City", details.City)
            .Require("country", "Country", details.Country)
            .AddIf(details.OpenedDate > today, "openedDate", "Opened date cannot be in the future.");
        if (violations.Any)
        {
            return violations.ToResult();
        }

        AssignCodeAndStatus(details.Code, details.Status);
        CompanyId = details.CompanyId;
        Name = TextNormalization.Clean(details.Name);
        Type = details.Type;
        Email = TextNormalization.CleanOptional(details.Email);
        Phone = TextNormalization.CleanOptional(details.Phone);
        Address = TextNormalization.CleanOptional(details.Address);
        City = TextNormalization.Clean(details.City);
        Country = TextNormalization.Clean(details.Country);
        ManagerEmployeeId = details.ManagerEmployeeId;
        OpenedDate = details.OpenedDate;
        return Result.Success();
    }
}
