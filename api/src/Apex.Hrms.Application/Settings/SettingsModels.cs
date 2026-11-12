using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Security;
using Apex.Hrms.Domain.Settings;

namespace Apex.Hrms.Application.Settings;

// ---- Users ----

/// <summary>
/// Mirrors AppUser in settings.models.ts. <c>Status</c> is Active, Inactive or Locked (Locked also while the sign-in
/// lockout after failed attempts lasts). <c>EmployeeId</c> is null for the built-in administrator. No credentials of any
/// kind are part of a response.
/// </summary>
public sealed record UserDto(
    int Id,
    int? EmployeeId,
    string DisplayName,
    string Email,
    int RoleId,
    string Status,
    DateTime? LastLoginOn,
    byte[] RowVersion) : IIdentified;

public sealed record UserFilter(int? RoleId = null, string? Status = null);

/// <summary>
/// POST body. <c>Status</c> is Active (default) or Inactive. The initial password is hashed at once and never returned,
/// logged or stored in plain text.
/// </summary>
public sealed record UserCreateRequest(int EmployeeId, int RoleId, string? Status, string? InitialPassword)
{
    /// <summary>
    /// The compiler-generated ToString prints every property, which would print <see cref="InitialPassword"/>. This override
    /// leaves the password out, so it cannot reach a log, an exception message or a test failure.
    /// </summary>
    private bool PrintMembers(System.Text.StringBuilder builder)
    {
        builder.Append(System.Globalization.CultureInfo.InvariantCulture, $"EmployeeId = {EmployeeId}, RoleId = {RoleId}, Status = {Status}");
        return true;
    }
}

/// <summary>PUT body: only the role changes. The linked employee never does; status moves are commands.</summary>
public sealed record UserUpdateRequest(int RoleId, string? RowVersion) : IVersionedRequest;

public sealed record UnlockUserCommand(int Id, string? RowVersion) : IEntityCommand<UnlockUserCommand>
{
    public static UnlockUserCommand From(int id, string? rowVersion) => new(id, rowVersion);
}

public sealed record MessageDto(string Message);

// ---- Roles ----

/// <summary>Mirrors Role in settings.models.ts.</summary>
public sealed record RoleDto(int Id, string Name, string Description, bool IsSystem, string Status, byte[] RowVersion) : IIdentified;

public sealed record RoleFilter(string? Status = null);

public interface IRoleFields
{
    string Name { get; }

    string? Description { get; }
}

public sealed record RoleCreateRequest(string Name, string? Description, string? Status) : IRoleFields, ICreateRequest<RoleDetails>
{
    public RoleDetails ToDetails() => new(Name.Trim(), Description?.Trim());

    public RecordStatus ParseStatus() => Status is null ? RecordStatus.Active : Enum.Parse<RecordStatus>(Status);
}

public sealed record RoleUpdateRequest(string Name, string? Description, string? RowVersion) : IRoleFields, IDetailsRequest<RoleDetails>, IVersionedRequest
{
    public RoleDetails ToDetails() => new(Name.Trim(), Description?.Trim());
}

// ---- Role permissions ----

/// <summary>A module of the matrix with the actions that exist for it ("View" always first).</summary>
public sealed record PermissionModuleDto(string Key, string Label, IReadOnlyList<string> Actions);

/// <summary>
/// The permissions of one role: module key to granted actions. <c>Id</c> equals the role id. <c>RowVersion</c> is the row
/// version of the role, which changes with every save of its permissions.
/// </summary>
public sealed record RolePermissionsDto(int Id, int RoleId, IReadOnlyDictionary<string, IReadOnlyList<string>> Grants, byte[] RowVersion) : IIdentified;

/// <summary>The whole matrix: the modules with their actions, and the grants of every role.</summary>
public sealed record PermissionMatrixDto(IReadOnlyList<PermissionModuleDto> Modules, IReadOnlyList<RolePermissionsDto> Roles);

/// <summary>PUT body of a role's permissions: module key to actions.</summary>
public sealed record RolePermissionsUpdateRequest(Dictionary<string, string[]?>? Grants, string? RowVersion) : IVersionedRequest;

// ---- General settings ----

public sealed record GeneralSettingsDto(
    int Id,
    string OrganizationName,
    string ContactEmail,
    string Timezone,
    string DateFormat,
    string CurrencyCode,
    int FiscalYearStartMonth,
    bool EmailNotifications,
    bool WeeklyDigest,
    byte[] RowVersion) : IIdentified;

public sealed record GeneralSettingsUpdateRequest(
    string? OrganizationName,
    string? ContactEmail,
    string? Timezone,
    string? DateFormat,
    string? CurrencyCode,
    int FiscalYearStartMonth,
    bool EmailNotifications,
    bool WeeklyDigest,
    string? RowVersion) : IVersionedRequest
{
    public GeneralSettingsDetails ToDetails() => new(
        OrganizationName?.Trim() ?? string.Empty,
        ContactEmail ?? string.Empty,
        Timezone ?? string.Empty,
        DateFormat ?? string.Empty,
        CurrencyCode ?? string.Empty,
        FiscalYearStartMonth,
        EmailNotifications,
        WeeklyDigest);
}

// ---- Handlers of the shapes the generic use cases do not cover ----

public interface IUserPasswordResetHandler
{
    /// <summary>Sends the password reset email to the user. Nothing about the password is returned.</summary>
    Task<Result<MessageDto>> HandleAsync(int id, CancellationToken cancellationToken);
}

public interface IPermissionMatrixHandler
{
    Task<Result<PermissionMatrixDto>> GetMatrixAsync(CancellationToken cancellationToken);

    Task<Result<RolePermissionsDto>> GetForRoleAsync(int roleId, CancellationToken cancellationToken);

    Task<Result<RolePermissionsDto>> SaveAsync(int roleId, RolePermissionsUpdateRequest request, CancellationToken cancellationToken);
}

public interface IGeneralSettingsHandler
{
    Task<Result<GeneralSettingsDto>> GetAsync(CancellationToken cancellationToken);

    Task<Result<GeneralSettingsDto>> SaveAsync(GeneralSettingsUpdateRequest request, CancellationToken cancellationToken);
}

// ---- Ports implemented by Infrastructure ----

/// <summary>
/// The sign-in side of an account (the identity user in auth.User): everything that needs the Identity password hasher or
/// the lockout and reset token machinery. No method returns a password, a hash or a token.
/// </summary>
public interface IAccountLogins
{
    /// <summary>
    /// Creates the identity user with the given password, hashed by the Identity password hasher, and returns its id.
    /// A password that breaks the password policy, or an email that is already taken, is a Validation failure.
    /// </summary>
    Task<Result<string>> CreateAsync(string email, string password, CancellationToken cancellationToken);

    /// <summary>
    /// Removes an identity user (the delete of an account). It writes through the shared database context, so call it inside the
    /// <see cref="Apex.Hrms.Application.Abstractions.IUnitOfWorkTransaction"/> of the delete: nothing is compensated by hand.
    /// </summary>
    Task DeleteAsync(string subject, CancellationToken cancellationToken);

    /// <summary>
    /// Changes the Identity security stamp, which ends every open session of the user at its next request. Call it inside the
    /// transaction of the change that made the sessions unwelcome (an employee who left).
    /// </summary>
    Task InvalidateSessionsAsync(string subject, CancellationToken cancellationToken);

    /// <summary>Clears the sign-in lockout and the count of failed attempts.</summary>
    Task UnlockAsync(string subject, CancellationToken cancellationToken);

    /// <summary>Of these identity users, the ones whose sign-in is locked out at the moment.</summary>
    Task<IReadOnlySet<string>> LockedOutAsync(IReadOnlyCollection<string> subjects, CancellationToken cancellationToken);

    /// <summary>Mails a password reset token to the address of the identity user (the same mail as forgot-password).</summary>
    Task SendPasswordResetAsync(string subject, CancellationToken cancellationToken);
}

/// <summary>The modules and actions of the permission matrix (sec.PermissionModule and sec.PermissionModuleAction) and the grants of the roles.</summary>
public interface IPermissionStore
{
    Task<IReadOnlyList<PermissionModuleInfo>> ModulesAsync(CancellationToken cancellationToken);

    /// <summary>The grants of every role, or of one role, as module key and action.</summary>
    Task<IReadOnlyList<RoleGrant>> GrantsAsync(int? roleId, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces the grants of the role and saves the role (which was loaded with its row version, so a concurrent change is a
    /// Conflict) together with the pending activity entry, all in one transaction. View grants are written first.
    /// </summary>
    Task<Result> ReplaceGrantsAsync(AppRole role, IReadOnlyCollection<(string ModuleKey, string Action)> grants, CancellationToken cancellationToken);
}
