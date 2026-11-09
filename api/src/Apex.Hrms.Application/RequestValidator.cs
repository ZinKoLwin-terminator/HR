using System.Linq.Expressions;
using FluentValidation;

namespace Apex.Hrms.Application;

/// <summary>
/// Base of every request validator: the first failing rule per property is reported. Validators own the shape of the
/// input (required, length, format, measured after trimming); the domain owns the business rules.
/// </summary>
internal abstract class RequestValidator<T> : AbstractValidator<T>
{
    protected RequestValidator() => RuleLevelCascadeMode = CascadeMode.Stop;

    /// <summary>A text rule whose messages name the field as the domain messages do (for example "Company name").</summary>
    protected IRuleBuilderInitial<T, string?> RuleForText(Expression<Func<T, string?>> property, string? label = null)
    {
        var rule = RuleFor(property);
        return label is null ? rule : rule.Configure(configured => configured.SetDisplayName(label));
    }
}
