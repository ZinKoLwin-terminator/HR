using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Domain.Payroll;

/// <summary>An allowance (adds to pay) or a deduction (takes from it).</summary>
public enum ComponentType
{
    Earning,
    Deduction,
}

/// <summary>How a component's amount is worked out: a fixed amount, or a percentage of basic or of gross pay.</summary>
public enum CalculationMethod
{
    Fixed,
    PercentOfBasic,
    PercentOfGross,
}

/// <summary>The life of a payroll period: Draft, Pending Approval, Approved, Paid. Paid is final.</summary>
public enum PayrollRunStatus
{
    Draft,
    PendingApproval,
    Approved,
    Paid,
}

/// <summary>Whether a payslip line adds to or takes from pay (pay.PayslipLine.LineType).</summary>
public enum PayLineType
{
    Earning,
    Deduction,
}

public static class ComponentTypeText
{
    public static readonly TextMap<ComponentType> Map = new(new Dictionary<ComponentType, string>
    {
        [ComponentType.Earning] = "Earning",
        [ComponentType.Deduction] = "Deduction",
    });

    public static string ToText(ComponentType value) => Map.ToText(value);

    public static ComponentType Parse(string? text) => Map.Parse(text);

    public static bool TryParse(string? text, out ComponentType value) => Map.TryParse(text, out value);
}

public static class CalculationMethodText
{
    public static readonly TextMap<CalculationMethod> Map = new(new Dictionary<CalculationMethod, string>
    {
        [CalculationMethod.Fixed] = "Fixed",
        [CalculationMethod.PercentOfBasic] = "PercentOfBasic",
        [CalculationMethod.PercentOfGross] = "PercentOfGross",
    });

    public static string ToText(CalculationMethod value) => Map.ToText(value);

    public static CalculationMethod Parse(string? text) => Map.Parse(text);

    public static bool TryParse(string? text, out CalculationMethod value) => Map.TryParse(text, out value);
}

public static class PayrollRunStatusText
{
    public static readonly TextMap<PayrollRunStatus> Map = new(new Dictionary<PayrollRunStatus, string>
    {
        [PayrollRunStatus.Draft] = "Draft",
        [PayrollRunStatus.PendingApproval] = "Pending Approval",
        [PayrollRunStatus.Approved] = "Approved",
        [PayrollRunStatus.Paid] = "Paid",
    });

    public static string ToText(PayrollRunStatus value) => Map.ToText(value);

    public static PayrollRunStatus Parse(string? text) => Map.Parse(text);

    public static bool TryParse(string? text, out PayrollRunStatus value) => Map.TryParse(text, out value);
}

public static class PayLineTypeText
{
    public static readonly TextMap<PayLineType> Map = new(new Dictionary<PayLineType, string>
    {
        [PayLineType.Earning] = "Earning",
        [PayLineType.Deduction] = "Deduction",
    });

    public static string ToText(PayLineType value) => Map.ToText(value);

    public static PayLineType Parse(string? text) => Map.Parse(text);
}
