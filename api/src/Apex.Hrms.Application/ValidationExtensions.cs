using System.Text.Json;
using System.Text.RegularExpressions;
using Apex.Hrms.Domain.Common;
using FluentValidation;
using FluentValidation.Results;

namespace Apex.Hrms.Application;

public static class ValidationExtensions
{
    private static readonly Regex PhonePattern = new(
        @"^\+?[0-9][0-9\s-]{6,18}[0-9]$",
        RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    /// <summary>Validates and converts failures into a Validation result keyed by camelCase field.</summary>
    public static async Task<Result> CheckAsync<T>(
        this IValidator<T> validator,
        T request,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        return validation.IsValid ? Result.Success() : Result.Failure(validation.ToError());
    }

    public static IRuleBuilderOptions<T, string?> RequiredText<T>(this IRuleBuilder<T, string?> rule, int maxLength) =>
        rule.NotEmpty().WithMessage("{PropertyName} is required.")
            .MaximumLength(maxLength).WithMessage("{PropertyName} must be at most {MaxLength} characters.");

    public static IRuleBuilderOptions<T, string?> OptionalText<T>(this IRuleBuilder<T, string?> rule, int maxLength) =>
        rule.MaximumLength(maxLength).WithMessage("{PropertyName} must be at most {MaxLength} characters.");

    public static IRuleBuilderOptions<T, string?> RequiredCode<T>(this IRuleBuilder<T, string?> rule, int maxLength) =>
        rule.RequiredText(maxLength)
            .Must(code => RecordCodeRules.IsValid(code?.Trim())).WithMessage(RecordCodeRules.InvalidMessage);

    public static IRuleBuilderOptions<T, string?> RequiredStatus<T>(this IRuleBuilder<T, string?> rule) =>
        rule.NotEmpty().WithMessage("Status is required.")
            .Must(status => RecordStatusText.TryParse(status, out _)).WithMessage("Status must be Active or Inactive.");

    public static IRuleBuilderOptions<T, string?> OptionalPhone<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(phone => string.IsNullOrWhiteSpace(phone) || PhonePattern.IsMatch(phone.Trim()))
            .WithMessage("Enter a valid phone number.");

    private static AppError ToError(this ValidationResult validation)
    {
        var violations = new Violations();
        foreach (var failure in validation.Errors)
        {
            violations.Add(JsonNamingPolicy.CamelCase.ConvertName(failure.PropertyName), failure.ErrorMessage);
        }

        return violations.ToError();
    }
}
