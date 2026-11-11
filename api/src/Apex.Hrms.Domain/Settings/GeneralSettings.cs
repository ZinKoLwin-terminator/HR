using System.Text.RegularExpressions;
using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Domain.Settings;

/// <summary>The values of the general settings a user can edit (already trimmed where text).</summary>
public sealed record GeneralSettingsDetails(
    string OrganizationName,
    string ContactEmail,
    string Timezone,
    string DateFormat,
    string CurrencyCode,
    int FiscalYearStartMonth,
    bool EmailNotifications,
    bool WeeklyDigest);

/// <summary>The organization-wide settings (cfg.GeneralSettings, one row with Id 1, created by the post-deployment script).</summary>
public sealed partial class GeneralSettings : AuditedEntity
{
    public const int SingletonId = 1;
    public const int OrganizationNameMaxLength = 80;
    public const int ContactEmailMaxLength = 120;

    public const string ValidationSummary = "Please fix the highlighted settings.";

    /// <summary>TIMEZONES of settings.models.ts.</summary>
    public static IReadOnlyList<string> Timezones { get; } =
        ["Asia/Yangon", "Asia/Bangkok", "Asia/Singapore", "Asia/Kolkata", "Europe/London", "America/New_York", "UTC"];

    /// <summary>DATE_FORMATS of settings.models.ts.</summary>
    public static IReadOnlyList<string> DateFormats { get; } = ["yyyy-MM-dd", "dd/MM/yyyy", "MM/dd/yyyy", "dd MMM yyyy"];

    private GeneralSettings()
    {
    }

    public string OrganizationName { get; private set; } = string.Empty;

    public string ContactEmail { get; private set; } = string.Empty;

    public string Timezone { get; private set; } = string.Empty;

    public string DateFormat { get; private set; } = string.Empty;

    public string CurrencyCode { get; private set; } = string.Empty;

    public byte FiscalYearStartMonth { get; private set; }

    public bool EmailNotifications { get; private set; }

    public bool WeeklyDigest { get; private set; }

    /// <summary>The rules of GeneralSettingsMockService.save, with its field names and messages.</summary>
    public static Result Validate(GeneralSettingsDetails details)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(details.OrganizationName))
        {
            errors["organizationName"] = ["Organization name is required."];
        }
        else if (details.OrganizationName.Trim().Length > OrganizationNameMaxLength)
        {
            errors["organizationName"] = [$"Organization name must be at most {OrganizationNameMaxLength} characters."];
        }

        if (details.ContactEmail is null || details.ContactEmail.Length > ContactEmailMaxLength || !EmailPattern().IsMatch(details.ContactEmail))
        {
            errors["contactEmail"] = ["Enter a valid email address."];
        }

        if (!Timezones.Contains(details.Timezone, StringComparer.Ordinal))
        {
            errors["timezone"] = ["Choose a listed time zone."];
        }

        if (!DateFormats.Contains(details.DateFormat, StringComparer.Ordinal))
        {
            errors["dateFormat"] = ["Choose a listed format."];
        }

        if (details.CurrencyCode is null || !CurrencyPattern().IsMatch(details.CurrencyCode))
        {
            errors["currencyCode"] = ["Use a 3-letter code such as MMK."];
        }

        if (details.FiscalYearStartMonth is < 1 or > 12)
        {
            errors["fiscalYearStartMonth"] = ["Choose a month."];
        }

        return errors.Count == 0
            ? Result.Success()
            : Result.Failure(AppError.Validation(ValidationSummary, errors));
    }

    public Result Update(GeneralSettingsDetails details)
    {
        var valid = Validate(details);
        if (valid.IsFailure)
        {
            return valid;
        }

        OrganizationName = details.OrganizationName.Trim();
        ContactEmail = details.ContactEmail;
        Timezone = details.Timezone;
        DateFormat = details.DateFormat;
        CurrencyCode = details.CurrencyCode;
        FiscalYearStartMonth = (byte)details.FiscalYearStartMonth;
        EmailNotifications = details.EmailNotifications;
        WeeklyDigest = details.WeeklyDigest;
        return Result.Success();
    }

    // The mock's /^[^\s@]+@[^\s@]+\.[^\s@]+$/ and /^[A-Z]{3}$/.
    [GeneratedRegex(@"^[^\s@]+@[^\s@]+\.[^\s@]+\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 200)]
    private static partial Regex EmailPattern();

    [GeneratedRegex(@"^[A-Z]{3}\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 200)]
    private static partial Regex CurrencyPattern();
}
