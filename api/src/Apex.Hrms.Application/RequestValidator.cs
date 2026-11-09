using FluentValidation;

namespace Apex.Hrms.Application;

/// <summary>Base of every request validator: the first failing rule per property is reported.</summary>
internal abstract class RequestValidator<T> : AbstractValidator<T>
{
    protected RequestValidator() => RuleLevelCascadeMode = CascadeMode.Stop;
}
