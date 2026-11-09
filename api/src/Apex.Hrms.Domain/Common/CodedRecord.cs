namespace Apex.Hrms.Domain.Common;

/// <summary>Base of every organization record: a unique code and an Active/Inactive status.</summary>
public abstract class CodedRecord : AuditedEntity
{
    public string Code { get; private set; } = string.Empty;

    public RecordStatus Status { get; private set; }

    public bool IsActive => Status == RecordStatus.Active;

    /// <summary>Makes the record active. Activating an active record is a no-op, so the call is idempotent.</summary>
    public Result Activate() => ChangeStatus(RecordStatus.Active);

    /// <summary>Makes the record inactive. Inactive records cannot receive new children. Idempotent.</summary>
    public Result Deactivate() => ChangeStatus(RecordStatus.Inactive);

    protected static void CheckCode(Violations violations, string? code) =>
        violations
            .Require("code", "Code", code)
            .AddIf(
                !string.IsNullOrWhiteSpace(code) && !RecordCodeRules.IsValid(code.Trim()),
                "code",
                RecordCodeRules.InvalidMessage);

    protected void AssignCode(string code) => Code = TextNormalization.Clean(code);

    /// <summary>Sets the status a new record starts with. Later changes go through Activate and Deactivate.</summary>
    protected void SetInitialStatus(RecordStatus status) => Status = status;

    private Result ChangeStatus(RecordStatus status)
    {
        Status = status;
        return Result.Success();
    }
}
