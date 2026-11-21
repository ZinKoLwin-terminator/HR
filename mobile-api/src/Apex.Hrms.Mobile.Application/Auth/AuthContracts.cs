namespace Apex.Hrms.Mobile.Application.Auth;

/// <summary>A use case: one request in, a <see cref="Apex.Hrms.Domain.Common.Result{T}"/> out. Failures are <c>AppError</c>s.</summary>
public interface IUseCase<in TRequest, TResponse>
{
    Task<Apex.Hrms.Domain.Common.Result<TResponse>> HandleAsync(TRequest request, CancellationToken cancellationToken);
}

/// <summary>The value of a use case that answers nothing but success.</summary>
public sealed record Done
{
    private Done()
    {
    }

    public static Done Value { get; } = new();
}

/// <summary>The caller of a signed-in request, read from the validated access token.</summary>
public sealed record MobileCaller(string IdentityUserId, string DeviceId, int AccountId);

/// <summary><c>userName</c> is the employee code (EMP-00421) or the work email.</summary>
public sealed record LoginCommand(string? UserName, string? Password, string? DeviceId, string? DeviceName);

public sealed record RefreshSessionCommand(string? RefreshToken, string? DeviceId);

public sealed record LogoutCommand(MobileCaller Caller);

public sealed record GetMeQuery(MobileCaller Caller);

public sealed record ChangePasswordCommand(MobileCaller Caller, string? CurrentPassword, string? NewPassword);

/// <summary>
/// The signed-in employee as the app shows it. <c>UserId</c> is the sec.AppUser id and <c>EmployeeId</c> the employee code,
/// both as text (hr_employee_app/docs/api_contracts.md §2). <c>MustChangePassword</c> is always false: the HRMS has no forced change.
/// </summary>
public sealed record MobileUserDto(
    string UserId,
    string EmployeeId,
    string FullName,
    string Email,
    string? Department,
    string? Designation,
    string? ProfilePhotoUrl,
    bool MustChangePassword);

/// <summary>The answer of login and refresh. Times are UTC instants.</summary>
public sealed record MobileTokensDto(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAt,
    DateTime RefreshTokenExpiresAt,
    string TokenType,
    MobileUserDto User);

/// <summary>The user plus the role and its "module:Action" permissions, read live.</summary>
public sealed record MobileMeDto(MobileUserDto User, string RoleName, IReadOnlyList<string> Permissions);

/// <summary>Decides on every signed-in request whether the session behind the access token is still valid.</summary>
public interface IMobileAccessValidator
{
    Task<bool> IsValidAsync(MobileCaller caller, CancellationToken cancellationToken);
}

/// <summary>The messages the app shows. Every refused sign-in and refresh gets the same one, whatever the reason.</summary>
public static class MobileAuthMessages
{
    public const string SignInFailed = "Sign-in failed. Check your employee ID or email and password, or contact HR.";
    public const string SessionEnded = "Your session has expired. Please sign in again.";
    public const string EnterCredentials = "Enter your employee ID or work email and password.";
    public const string WrongCurrentPassword = "Your current password is incorrect.";
    public const string ChangeLockedOut = "Too many incorrect attempts. Wait 15 minutes and try again.";
    public const string PasswordNotChanged = "The password could not be changed.";
    public const string TokenType = "Bearer";
}
