namespace Apex.Hrms.Api.Auth;

internal sealed record LoginRequest(string? Email, string? Password, bool RememberMe);

internal sealed record ForgotPasswordRequest(string? Email);

internal sealed record ResetPasswordRequest(string? Email, string? Token, string? NewPassword);

/// <summary>The signed-in account. <c>permissions</c> are "module:Action" keys such as <c>employees:View</c>.</summary>
internal sealed record MeResponse(
    int Id,
    int? EmployeeId,
    string DisplayName,
    string Email,
    string RoleName,
    IReadOnlyList<string> Permissions);

internal sealed record MessageResponse(string Message);
