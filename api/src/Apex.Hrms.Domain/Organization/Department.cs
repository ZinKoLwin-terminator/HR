using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Domain.Organization;

/// <summary>The editable fields of a department. Status changes through Activate and Deactivate.</summary>
public sealed record DepartmentDetails(
    int CompanyId,
    int BranchId,
    string Code,
    string Name,
    string? Description,
    int? HeadEmployeeId,
    string? CostCenter)
{
    /// <summary>Trims text and turns blank optional text into null. Idempotent.</summary>
    public DepartmentDetails Normalized() => this with
    {
        Code = TextNormalization.Clean(Code),
        Name = TextNormalization.Clean(Name),
        Description = TextNormalization.CleanOptional(Description),
        CostCenter = TextNormalization.CleanOptional(CostCenter),
    };
}

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

    public static Result<Department> Create(DepartmentDetails details, RecordStatus status)
    {
        var department = new Department();
        var result = department.Update(details);
        if (result.IsFailure)
        {
            return Result.Failure<Department>(result.Error);
        }

        department.SetInitialStatus(status);
        return Result.Success(department);
    }

    /// <summary>Checks the rules that need no other record, without changing anything.</summary>
    public static Result Validate(DepartmentDetails details)
    {
        var normalized = details.Normalized();
        var violations = new Violations();
        CheckCode(violations, normalized.Code);
        violations.Require("name", "Department name", normalized.Name);
        return violations.ToResult();
    }

    /// <summary>Applies the details, or changes nothing when any rule is violated. Status is left untouched.</summary>
    public Result Update(DepartmentDetails details)
    {
        var valid = Validate(details);
        if (valid.IsFailure)
        {
            return valid;
        }

        var normalized = details.Normalized();
        AssignCode(normalized.Code);
        CompanyId = normalized.CompanyId;
        BranchId = normalized.BranchId;
        Name = normalized.Name;
        Description = normalized.Description;
        HeadEmployeeId = normalized.HeadEmployeeId;
        CostCenter = normalized.CostCenter;
        return Result.Success();
    }
}
