namespace Apex.Hrms.Domain.Common;

/// <summary>Base of every organization record: a unique code and an Active/Inactive status.</summary>
public abstract class CodedRecord : AuditedEntity
{
    public string Code { get; private set; } = string.Empty;

    public RecordStatus Status { get; private set; }

    public bool IsActive => Status == RecordStatus.Active;

    protected static void CheckCode(Violations violations, string? code) =>
        violations
            .Require("code", "Code", code)
            .AddIf(
                !string.IsNullOrWhiteSpace(code) && !RecordCodeRules.IsValid(code.Trim()),
                "code",
                RecordCodeRules.InvalidMessage);

    protected void AssignCodeAndStatus(string code, RecordStatus status)
    {
        Code = TextNormalization.Clean(code);
        Status = status;
    }
}
