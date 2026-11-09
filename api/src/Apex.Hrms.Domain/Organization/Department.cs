using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Domain.Organization;

public sealed record DepartmentDetails(
    int CompanyId,
    int BranchId,
    string Code,
    string Name,
    string? Description,
    int? HeadEmployeeId,
    string? CostCenter,
    RecordStatus Status);

public sealed class Department : CodedRecord
{
    private Department()
    {
    }

    public int CompanyId { get; private set; }

    public int BranchId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public int? HeadEmployeeId { get; private set; }

    public string? CostCenter { get; private set; }

    public static Result<Department> Create(DepartmentDetails details)
    {
        var department = new Department();
        var result = department.Update(details);
        return result.IsFailure ? Result.Failure<Department>(result.Error) : Result.Success(department);
    }

    /// <summary>Applies the details, or changes nothing when any rule is violated.</summary>
    public Result Update(DepartmentDetails details)
    {
        var violations = new Violations();
        CheckCode(violations, details.Code);
        violations.Require("name", "Department name", details.Name);
        if (violations.Any)
        {
            return violations.ToResult();
        }

        AssignCodeAndStatus(details.Code, details.Status);
        CompanyId = details.CompanyId;
        BranchId = details.BranchId;
        Name = TextNormalization.Clean(details.Name);
        Description = TextNormalization.CleanOptional(details.Description);
        HeadEmployeeId = details.HeadEmployeeId;
        CostCenter = TextNormalization.CleanOptional(details.CostCenter);
        return Result.Success();
    }
}
