using System.Text.Json;
using System.Text.RegularExpressions;
using Apex.Hrms.Application.UseCases;
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

    // Length and format rules look at the trimmed text, because the domain trims before it stores.

    public static IRuleBuilderOptions<T, string?> RequiredText<T>(this IRuleBuilder<T, string?> rule, int maxLength) =>
        rule.NotEmpty().WithMessage("{PropertyName} is required.")
            .OptionalText(maxLength);

    public static IRuleBuilderOptions<T, string?> OptionalText<T>(this IRuleBuilder<T, string?> rule, int maxLength) =>
        rule.Must(text => text is null || text.Trim().Length <= maxLength)
            .WithMessage($"{{PropertyName}} must be at most {maxLength} characters.");

    public static IRuleBuilderOptions<T, string?> RequiredCode<T>(this IRuleBuilder<T, string?> rule, int maxLength) =>
        rule.RequiredText(maxLength)
            .Must(code => RecordCodeRules.IsValid(code?.Trim())).WithMessage(RecordCodeRules.InvalidMessage);

    /// <summary>The status a new record starts with; only create requests carry one.</summary>
    public static IRuleBuilderOptions<T, string?> RequiredStatus<T>(this IRuleBuilder<T, string?> rule) =>
        rule.NotEmpty().WithMessage("Status is required.")
            .Must(status => RecordStatusText.TryParse(status, out _)).WithMessage("Status must be Active or Inactive.");

    /// <summary>The row version is required on every update; a missing or malformed one is a field error on <c>rowVersion</c>.</summary>
    public static IRuleBuilderOptionsConditions<T, string?> RequiredRowVersion<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Custom((value, context) =>
        {
            var decoded = RowVersionToken.Decode(value);
            if (decoded.IsFailure)
            {
                context.AddFailure(decoded.Error.FieldErrors![RowVersionToken.Field][0]);
            }
        });

    /// <summary>A blank value passes (pair it with <c>RequiredText</c> when the email is mandatory).</summary>
    public static IRuleBuilderOptionsConditions<T, string?> ValidEmail<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Custom((value, context) =>
        {
            if (!string.IsNullOrWhiteSpace(value) && !IsEmail(value.Trim()))
            {
                context.AddFailure("Enter a valid email address.");
            }
        });

    public static IRuleBuilderOptions<T, string?> OptionalPhone<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(phone => string.IsNullOrWhiteSpace(phone) || PhonePattern.IsMatch(phone.Trim()))
            .WithMessage("Enter a valid phone number.");

    /// <summary>The same deliberately loose check as ASP.NET Core: exactly one @, with text on both sides.</summary>
    private static bool IsEmail(string email)
    {
        var at = email.IndexOf('@', StringComparison.Ordinal);
        return at > 0 && at != email.Length - 1 && at == email.LastIndexOf('@');
    }

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
