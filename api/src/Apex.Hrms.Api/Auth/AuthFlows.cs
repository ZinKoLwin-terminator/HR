using System.Globalization;
using System.Security.Claims;
using Apex.Hrms.Api.Http;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Security;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Security;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;

namespace Apex.Hrms.Api.Auth;

/// <summary>
/// The sign-in, sign-out and password reset flows behind <c>/api/auth</c>.
/// Every failed sign-in gets the same generic 401; the real reason is logged (with the identity user id when known)
/// but never the email, the password or a token. Forgot-password answers the same for known and unknown addresses.
/// </summary>
internal sealed partial class AuthFlows(
    UserManager<IdentityUser> users,
    IAccountDirectory accounts,
    IEmailSender email,
    IAntiforgery antiforgery,
    TimeProvider time,
    IConfiguration configuration,
    IHostEnvironment environment,
    ILogger<AuthFlows> logger)
{
    /// <summary>Sliding window of a browser-session cookie; also its idle timeout.</summary>
    public static readonly TimeSpan SessionWindow = TimeSpan.FromMinutes(30);

    /// <summary>Hard end of a browser-session cookie, however active the user is.</summary>
    public static readonly TimeSpan SessionAbsoluteMaximum = TimeSpan.FromHours(8);

    /// <summary>Sliding window of a "remember me" cookie.</summary>
    public static readonly TimeSpan RememberWindow = TimeSpan.FromDays(7);

    /// <summary>Configuration key: the hard end of a "remember me" session in days, however active the user is.</summary>
    public const string RememberMeMaxDaysKey = "Auth:RememberMeMaxDays";

    public const int DefaultRememberMeMaxDays = 30;

    public const int MaxRememberMeMaxDays = 365;

    public const string PasswordResetPageUrlKey = "Auth:PasswordResetUrl";
    public const string GenericSignInFailure = "Sign-in failed. Check your details or contact your administrator.";
    public const string ForgotPasswordAnswer = "If an account exists for this address, a password reset email has been sent.";
    public const string InvalidResetMessage = "The reset link is invalid or has expired. Request a new one.";
    public const string EmailNotConfiguredMessage = "Password reset by email is not configured on this server. Ask an administrator to configure email.";

    // A real hash, so an unknown address costs the same time as a wrong password (no timing based enumeration).
    private static readonly Lazy<string> DummyHash = new(
        () => new PasswordHasher<IdentityUser>().HashPassword(new IdentityUser(), Guid.NewGuid().ToString("N")));

    public IResult IssueAntiforgery(HttpContext http)
    {
        IssueXsrfCookie(http);
        return Results.NoContent();
    }

    public async Task<IResult> LoginAsync(HttpContext http, LoginRequest request, CancellationToken cancellationToken)
    {
        var fieldErrors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            fieldErrors["email"] = ["Enter your email address."];
        }

        if (string.IsNullOrEmpty(request.Password))
        {
            fieldErrors["password"] = ["Enter your password."];
        }

        if (fieldErrors.Count > 0)
        {
            return Results.Problem(ApiProblems.From(AppError.Validation("Enter your email address and password.", fieldErrors)));
        }

        var user = await users.FindByEmailAsync(request.Email!.Trim());
        if (user is null)
        {
            users.PasswordHasher.VerifyHashedPassword(new IdentityUser(), DummyHash.Value, request.Password!);
            LogRefused("UnknownAccount", null);
            return GenericFailure();
        }

        var credentials = await CheckCredentialsAsync(user, request.Password!);
        if (credentials != CredentialCheck.Valid)
        {
            LogRefused(credentials.ToString(), user.Id);
            return GenericFailure();
        }

        var account = await accounts.FindByExternalSubjectAsync(user.Id, includePermissions: true, cancellationToken);
        var refusal = Refusal(account);
        if (refusal is not null)
        {
            LogRefused(refusal, user.Id);
            return GenericFailure();
        }

        var stamp = await users.GetSecurityStampAsync(user);
        var principal = BuildPrincipal(user, account!, stamp);
        var now = time.GetUtcNow();
        var properties = new AuthenticationProperties
        {
            IsPersistent = request.RememberMe,
            AllowRefresh = true,
            IssuedUtc = now,
            ExpiresUtc = now + (request.RememberMe ? RememberWindow : SessionWindow),
        };
        // Both kinds of session have a hard end, enforced by SessionValidator: sliding renewal cannot extend it.
        var absolute = request.RememberMe ? RememberMeAbsoluteMaximum() : SessionAbsoluteMaximum;
        properties.Items[HrmsClaims.AbsoluteExpiryProperty] = (now + absolute).ToString("O", CultureInfo.InvariantCulture);

        await http.SignInAsync(IdentityConstants.ApplicationScheme, principal, properties);
        await accounts.RecordSignInAsync(account!.Id, cancellationToken);

        // The antiforgery token is bound to the user: hand out one for the new identity.
        http.User = principal;
        IssueXsrfCookie(http);
        return Results.Ok(ToMe(account));
    }

    /// <summary>The configured absolute lifetime of a "remember me" session: 30 days by default, 1 to 365 days.</summary>
    public TimeSpan RememberMeAbsoluteMaximum() =>
        int.TryParse(configuration[RememberMeMaxDaysKey], NumberStyles.None, CultureInfo.InvariantCulture, out var days) && days is >= 1 and <= MaxRememberMeMaxDays
            ? TimeSpan.FromDays(days)
            : TimeSpan.FromDays(DefaultRememberMeMaxDays);

    public async Task<IResult> LogoutAsync(HttpContext http)
    {
        await http.SignOutAsync(IdentityConstants.ApplicationScheme);
        http.User = new ClaimsPrincipal(new ClaimsIdentity());
        IssueXsrfCookie(http);
        return Results.NoContent();
    }

    public async Task<IResult> MeAsync(HttpContext http, ICurrentUser currentUser, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } id)
        {
            return Results.Problem(ApiProblems.From(AppError.Unauthorized("Sign in to continue.")));
        }

        var account = await accounts.FindByIdAsync(id, includePermissions: true, cancellationToken);
        if (Refusal(account) is not null)
        {
            await http.SignOutAsync(IdentityConstants.ApplicationScheme);
            return Results.Problem(ApiProblems.From(AppError.Unauthorized("Sign in to continue.")));
        }

        return Results.Ok(ToMe(account!));
    }

    public async Task<IResult> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        // Fail closed first, for every address alike: never silently drop mail, never reveal which addresses exist.
        try
        {
            email.EnsureAvailable();
        }
        catch (EmailNotConfiguredException)
        {
            LogEmailNotConfigured();
            return Results.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Password reset unavailable",
                detail: EmailNotConfiguredMessage);
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return Results.Problem(ApiProblems.From(AppError.Validation("Enter your email address.", "email", "Enter your email address.")));
        }

        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user?.Email is not null)
        {
            var account = await accounts.FindByExternalSubjectAsync(user.Id, includePermissions: false, cancellationToken);
            if (account is { CanSignIn: true })
            {
                var token = await users.GeneratePasswordResetTokenAsync(user);
                try
                {
                    await email.SendAsync(PasswordResetEmail.Build(user.Email, token, configuration[PasswordResetPageUrlKey]), cancellationToken);
                }
#pragma warning disable CA1031 // The answer must not depend on whether the account exists, so a send failure is logged, not surfaced.
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    LogSendFailed(exception.GetType().Name);
                }
#pragma warning restore CA1031
            }
        }

        return Results.Ok(new MessageResponse(ForgotPasswordAnswer));
    }

    public async Task<IResult> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        // Checked before any lookup, for everyone, so the answer does not depend on whether the account exists.
        var fieldErrors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            fieldErrors["email"] = ["Enter your email address."];
        }

        if (string.IsNullOrWhiteSpace(request.Token))
        {
            fieldErrors["token"] = ["The reset token is required."];
        }

        if (SecurityPolicy.PasswordViolation(request.NewPassword) is { } passwordProblem)
        {
            fieldErrors["newPassword"] = [passwordProblem];
        }

        if (fieldErrors.Count > 0)
        {
            return Results.Problem(ApiProblems.From(AppError.Validation("The password could not be reset.", fieldErrors)));
        }

        var user = await users.FindByEmailAsync(request.Email!.Trim());
        if (user is null)
        {
            LogResetRefused("UnknownAccount");
            return InvalidReset();
        }

        var account = await accounts.FindByExternalSubjectAsync(user.Id, includePermissions: false, cancellationToken);
        if (account is not { CanSignIn: true })
        {
            LogResetRefused("AccountCannotSignIn");
            return InvalidReset();
        }

        var result = await users.ResetPasswordAsync(user, request.Token!, request.NewPassword!);
        if (!result.Succeeded)
        {
            LogResetRefused(string.Join(",", result.Errors.Select(error => error.Code)));
            return InvalidReset();
        }

        // Proof of owning the mailbox: lift a lockout caused by failed attempts. ResetPasswordAsync already
        // changed the security stamp, which ends every open session of this user.
        await users.SetLockoutEndDateAsync(user, null);
        await users.ResetAccessFailedCountAsync(user);
        return Results.NoContent();
    }

    private static IResult GenericFailure() =>
        Results.Problem(ApiProblems.From(AppError.Unauthorized(GenericSignInFailure)));

    private static IResult InvalidReset() =>
        Results.Problem(ApiProblems.From(AppError.Validation(InvalidResetMessage, "token", InvalidResetMessage)));

    /// <summary>Why this account may not sign in, or null when it may.</summary>
    private static string? Refusal(AccountSnapshot? account) => account switch
    {
        null => "NoLinkedAccount",
        { Status: AccountStatus.Inactive } => "AccountInactive",
        { Status: AccountStatus.Locked } => "AccountLocked",
        { RoleActive: false } => "RoleInactive",
        { EmployeeLeft: true } => "EmployeeLeft",
        _ => null,
    };

    private async Task<CredentialCheck> CheckCredentialsAsync(IdentityUser user, string password)
    {
        // The same sequence as Identity's SignInManager.CheckPasswordSignInAsync with lockoutOnFailure.
        if (await users.GetLockoutEnabledAsync(user) && await users.IsLockedOutAsync(user))
        {
            return CredentialCheck.LockedOut;
        }

        if (await users.CheckPasswordAsync(user, password))
        {
            await users.ResetAccessFailedCountAsync(user);
            return CredentialCheck.Valid;
        }

        if (await users.GetLockoutEnabledAsync(user))
        {
            await users.AccessFailedAsync(user);
        }

        return CredentialCheck.WrongPassword;
    }

    private static ClaimsPrincipal BuildPrincipal(IdentityUser user, AccountSnapshot account, string? stamp)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, account.DisplayName),
            new(ClaimTypes.Email, account.Email),
            new(HrmsClaims.AccountId, account.Id.ToString(CultureInfo.InvariantCulture)),
            new(HrmsClaims.SecurityStamp, stamp ?? string.Empty),
        };
        if (account.EmployeeId is { } employeeId)
        {
            claims.Add(new Claim(HrmsClaims.EmployeeId, employeeId.ToString(CultureInfo.InvariantCulture)));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, HrmsClaims.AuthenticationType, ClaimTypes.Name, ClaimTypes.Role));
    }

    private static MeResponse ToMe(AccountSnapshot account) =>
        new(account.Id, account.EmployeeId, account.DisplayName, account.Email, account.RoleName, account.Permissions);

    private void IssueXsrfCookie(HttpContext http)
    {
        var tokens = antiforgery.GetAndStoreTokens(http);
        http.Response.Cookies.Append(
            AuthCookies.XsrfTokenCookie,
            tokens.RequestToken!,
            new CookieOptions
            {
                HttpOnly = false, // The frontend must read it to copy it into the header.
                Secure = !environment.IsDevelopment(),
                SameSite = SameSiteMode.Strict,
                Path = "/",
                IsEssential = true,
            });
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Sign-in refused: {Reason}. Identity user: {AuthUserId}.")]
    private partial void LogRefused(string reason, string? authUserId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Password reset refused: {Reason}.")]
    private partial void LogResetRefused(string reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "Password reset is unavailable: email sending is not configured (Email:Smtp settings).")]
    private partial void LogEmailNotConfigured();

    [LoggerMessage(Level = LogLevel.Error, Message = "The password reset mail could not be sent. Exception type: {ExceptionType}.")]
    private partial void LogSendFailed(string exceptionType);

    private enum CredentialCheck
    {
        Valid,
        WrongPassword,
        LockedOut,
    }
}
