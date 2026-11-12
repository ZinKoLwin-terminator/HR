using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Domain.Employees;

/// <summary>
/// A do-not-rehire entry (hr.EmployeeBlacklist): a former employee HR does not want hired again. The reason is sensitive
/// (never written to the activity feed). An entry is active until it is lifted; a lifted entry stays as history and the
/// employee can be put on the list again. Which employee may be listed (only former employees) and the one-active-entry
/// rule need other records, so the Application layer checks them.
/// </summary>
public sealed class EmployeeBlacklistEntry : AuditedEntity
{
    public const int ReasonMaxLength = 500;
    public const int NameMaxLength = 130;

    private EmployeeBlacklistEntry()
    {
    }

    public int EmployeeId { get; private set; }

    public string Reason { get; private set; } = string.Empty;

    public DateOnly BlacklistedOn { get; private set; }

    public int BlacklistedByUserId { get; private set; }

    /// <summary>Display name of the user at the time (kept even if the user is later removed).</summary>
    public string BlacklistedByName { get; private set; } = string.Empty;

    public DateOnly? LiftedOn { get; private set; }

    public int? LiftedByUserId { get; private set; }

    public string? LiftedByName { get; private set; }

    public string? LiftReason { get; private set; }

    public bool IsActive => LiftedOn is null;

    public static Result<EmployeeBlacklistEntry> Create(int employeeId, string? reason, DateOnly today, int userId, string userName)
    {
        var valid = ValidateReason(reason, "reason", "Reason");
        if (valid.IsFailure)
        {
            return Result.Failure<EmployeeBlacklistEntry>(valid.Error);
        }

        return Result.Success(new EmployeeBlacklistEntry
        {
            EmployeeId = employeeId,
            Reason = reason!.Trim(),
            BlacklistedOn = today,
            BlacklistedByUserId = userId,
            BlacklistedByName = Truncate(userName),
        });
    }

    /// <summary>Lifts an active entry. The reason is required; lifting a lifted entry is a conflict.</summary>
    public Result Lift(string? reason, DateOnly today, int userId, string userName)
    {
        if (!IsActive)
        {
            return Result.Failure(AppError.Conflict("This do-not-rehire entry has already been lifted."));
        }

        var valid = ValidateReason(reason, "reason", "Reason");
        if (valid.IsFailure)
        {
            return valid;
        }

        LiftedOn = today < BlacklistedOn ? BlacklistedOn : today;
        LiftedByUserId = userId;
        LiftedByName = Truncate(userName);
        LiftReason = reason!.Trim();
        return Result.Success();
    }

    /// <summary>A reason is required and at most 500 characters after trimming (400 on <c>reason</c>).</summary>
    public static Result ValidateReason(string? reason, string field = "reason", string label = "Reason")
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result.Failure(AppError.Validation($"{label} is required.", field, $"{label} is required."));
        }

        return reason.Trim().Length > ReasonMaxLength
            ? Result.Failure(AppError.Validation(
                $"{label} must be at most {ReasonMaxLength} characters.", field, $"{label} must be at most {ReasonMaxLength} characters."))
            : Result.Success();
    }

    private static string Truncate(string name) => name.Length <= NameMaxLength ? name : name[..NameMaxLength];
}

/// <summary>
/// How a candidate's contact details are compared with a listed employee's: emails ignore case and surrounding spaces,
/// phones compare their digits only, national ids ignore case and surrounding spaces. A blank value never matches.
/// </summary>
public static class BlacklistMatching
{
    public static string? NormalizeEmail(string? value) => NormalizeText(value);

    public static string? NormalizeNationalId(string? value) => NormalizeText(value);

    /// <summary>The digits of a phone number, or null when it has none.</summary>
    public static string? NormalizePhone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var digits = new string([.. value.Where(char.IsAsciiDigit)]);
        return digits.Length == 0 ? null : digits;
    }

    private static string? NormalizeText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
}
