using System.Linq.Expressions;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Activity;
using Apex.Hrms.Application.Organization;
using Apex.Hrms.Application.Security;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Activity;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Employees;
using Apex.Hrms.Domain.Security;
using FluentValidation;

namespace Apex.Hrms.Application.Settings;

internal sealed class UserMapping : IEntityDescriptor<AppAccount>
{
    public string Label => "User";

    public string Describe(AppAccount entity) => entity.DisplayName;

    public static UserDto ToDto(AppAccount account, bool lockedOut) => new(
        account.Id,
        account.EmployeeId,
        account.DisplayName,
        account.Email,
        account.RoleId,
        StatusText(account.Status, lockedOut),
        account.LastLoginAtUtc is { } at ? DateTime.SpecifyKind(at, DateTimeKind.Utc) : null,
        account.RowVersion);

    /// <summary>Locked while the sign-in lockout lasts, unless the account was switched off (Inactive wins).</summary>
    private static string StatusText(AccountStatus status, bool lockedOut) =>
        status == AccountStatus.Active && lockedOut ? nameof(AccountStatus.Locked) : status.ToString();
}

internal sealed class UserQuerySpecification : IQuerySpecification<AppAccount, UserFilter>
{
    public IReadOnlyDictionary<string, Expression<Func<AppAccount, object>>> SortKeys { get; } =
        new Dictionary<string, Expression<Func<AppAccount, object>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = a => a.Id,
            ["displayName"] = a => a.DisplayName,
            ["email"] = a => a.Email,
            ["roleId"] = a => a.RoleId,
            ["status"] = a => a.Status,
            ["lastLoginOn"] = a => a.LastLoginAtUtc!,
        };

    public Expression<Func<AppAccount, bool>> Search(string term) =>
        Predicates.ContainsAny<AppAccount>(term, a => a.DisplayName, a => a.Email);

    public Expression<Func<AppAccount, bool>> Filter(UserFilter filter)
    {
        var predicate = Predicates.Always<AppAccount>();
        if (filter.RoleId is { } roleId)
        {
            predicate = predicate.And(a => a.RoleId == roleId);
        }

        if (FilterPredicates.IsSet(filter.Status))
        {
            // "Locked" matches the stored status only; a sign-in lockout that has not been turned into a status is not filterable.
            predicate = Enum.GetNames<AccountStatus>().Contains(filter.Status, StringComparer.Ordinal) && Enum.TryParse<AccountStatus>(filter.Status, out var status)
                ? predicate.And(a => a.Status == status)
                : Predicates.Never<AppAccount>();
        }

        return predicate;
    }
}

internal sealed class AccountIdentityMapper : IDtoMapper<AppAccount, AppAccount>
{
    public AppAccount ToDto(AppAccount entity) => entity;
}

internal sealed class UserReadHandlers(
    IRepository<AppAccount> repository,
    IAccessScope<AppAccount> scope,
    ICurrentUser user,
    IQuerySpecification<AppAccount, UserFilter> specification,
    EntityLoader<AppAccount> loader,
    IAccountLogins logins) : IQueryHandler<UserDto, UserFilter>, IGetByIdHandler<UserDto>
{
    public async Task<Result<PagedResult<UserDto>>> HandleAsync(PageRequest<UserFilter> request, CancellationToken cancellationToken)
    {
        var inner = new QueryHandler<AppAccount, AppAccount, UserFilter>(repository, scope, user, specification, new AccountIdentityMapper());
        var page = await inner.HandleAsync(request, cancellationToken);
        if (page.IsFailure)
        {
            return Result.Failure<PagedResult<UserDto>>(page.Error);
        }

        var locked = await logins.LockedOutAsync(Subjects(page.Value.Items), cancellationToken);
        return Result.Success(page.Value.Map(account => UserMapping.ToDto(account, IsLocked(account, locked))));
    }

    public async Task<Result<UserDto>> HandleAsync(int id, CancellationToken cancellationToken)
    {
        var loaded = await loader.LoadAsync(id, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result.Failure<UserDto>(loaded.Error);
        }

        return Result.Success(UserMapping.ToDto(loaded.Value, await UserRules.IsLockedAsync(logins, loaded.Value, cancellationToken)));
    }

    private static List<string> Subjects(IEnumerable<AppAccount> accounts) =>
        [.. accounts.Select(account => account.ExternalSubject).OfType<string>()];

    private static bool IsLocked(AppAccount account, IReadOnlySet<string> locked) =>
        account.ExternalSubject is { } subject && locked.Contains(subject);
}

/// <summary>Rules the user commands share.</summary>
internal static class UserRules
{
    public const string OwnAccountMessage = "You cannot change your own role or deactivate your own account.";
    public const string OwnDeleteMessage = "You cannot delete your own account.";
    public const string LastAdministratorMessage = "There must always be one active HR Administrator.";
    public const string LeaverActivationMessage = "This user's employee has left the company, so the account cannot be activated.";

    public static async Task<bool> IsLockedAsync(IAccountLogins logins, AppAccount account, CancellationToken cancellationToken) =>
        account.ExternalSubject is { } subject && (await logins.LockedOutAsync([subject], cancellationToken)).Contains(subject);

    /// <summary>True when the signed-in caller has an account with the HR Administrator role (read live from the database; the permission check already refused an account that cannot sign in).</summary>
    public static async Task<bool> IsAdministratorAsync(
        IRepository<AppAccount> accounts,
        IRepository<AppRole> roles,
        ICurrentUser user,
        CancellationToken cancellationToken)
    {
        if (user.UserId is not { } userId || await accounts.FindAsync(userId, cancellationToken) is not { } account)
        {
            return false;
        }

        return await roles.FindAsync(account.RoleId, cancellationToken) is { IsActive: true, Name: SecurityPolicy.AdministratorRoleName };
    }

    public static Result EnsureNotSelf(AppAccount account, ICurrentUser user, string message) =>
        user.UserId is { } own && own == account.Id
            ? Result.Failure(AppError.Conflict(message))
            : Result.Success();

    /// <summary>An active role: fails as a field error on roleId, like the mock service.</summary>
    public static async Task<Result<AppRole>> FindActiveRoleAsync(IRepository<AppRole> roles, int roleId, CancellationToken cancellationToken)
    {
        var role = await roles.FindAsync(roleId, cancellationToken);
        return role is { IsActive: true }
            ? Result.Success(role)
            : Result.Failure<AppRole>(AppError.Validation("Choose an active role.", "roleId", "Choose an active role."));
    }

    /// <summary>
    /// Runs a change that could take <paramref name="account"/> out of the HR Administrator group (a demotion, a deactivation
    /// or a delete) so that two concurrent changes cannot both pass the last-administrator rule: one transaction holds the
    /// administrator application lock, checks the rule against the committed data, runs <paramref name="apply"/> (the domain
    /// change and its activity entry), saves, runs <paramref name="afterSave"/> (identity store writes that belong to the
    /// same change) and commits. A refusal or a failure at any step rolls everything back.
    /// </summary>
    public static async Task<Result> ChangeAdministratorAsync(
        IUnitOfWork unitOfWork,
        IRepository<AppAccount> accounts,
        IRepository<Employee> employees,
        IRepository<AppRole> roles,
        AppAccount account,
        Action apply,
        Func<Task>? afterSave,
        CancellationToken cancellationToken)
    {
        var begun = await unitOfWork.BeginTransactionAsync(SecurityPolicy.AdministratorLockName, cancellationToken);
        if (begun.IsFailure)
        {
            return Result.Failure(begun.Error);
        }

        await using var transaction = begun.Value;
        var notLast = await EnsureNotLastAdministratorAsync(accounts, roles, employees, account, cancellationToken);
        if (notLast.IsFailure)
        {
            return notLast;
        }

        apply();
        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        if (saved.IsFailure)
        {
            return saved;
        }

        if (afterSave is not null)
        {
            await afterSave();
        }

        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }

    /// <summary>
    /// Fails when <paramref name="account"/> is the only active HR Administrator who can sign in. Call before a change that
    /// would take the account out of that group (a demotion, a deactivation or a delete).
    /// </summary>
    public static async Task<Result> EnsureNotLastAdministratorAsync(
        IRepository<AppAccount> accounts,
        IRepository<AppRole> roles,
        IRepository<Employee> employees,
        AppAccount account,
        CancellationToken cancellationToken)
    {
        if (account.Status != AccountStatus.Active || account.ExternalSubject is null)
        {
            return Result.Success();
        }

        var administrators = await roles.ListAsync(role => role.Name == SecurityPolicy.AdministratorRoleName, cancellationToken);
        if (administrators.Count == 0 || administrators[0].Id != account.RoleId)
        {
            return Result.Success();
        }

        var administratorRoleId = administrators[0].Id;
        var accountId = account.Id;
        var candidates = await accounts.ListAsync(
            other => other.Id != accountId
                && other.Status == AccountStatus.Active
                && other.ExternalSubject != null
                && other.RoleId == administratorRoleId,
            cancellationToken);

        // Only administrators who can sign in count: an account whose employee has an exit date cannot.
        var employeeIds = candidates.Where(other => other.EmployeeId != null).Select(other => other.EmployeeId!.Value).ToList();
        var leftIds = employeeIds.Count == 0
            ? []
            : (await employees.ListAsync(employee => employeeIds.Contains(employee.Id) && employee.ExitDate != null, cancellationToken))
                .Select(employee => employee.Id)
                .ToHashSet();
        var another = candidates.Any(other => other.EmployeeId is not { } employeeId || !leftIds.Contains(employeeId));
        return another ? Result.Success() : Result.Failure(AppError.Conflict(LastAdministratorMessage));
    }
}

internal sealed class UserCreateRequestValidator : RequestValidator<UserCreateRequest>
{
    public UserCreateRequestValidator()
    {
        RuleFor(x => x.EmployeeId).GreaterThan(0).WithMessage("Choose a current employee.");
        RuleFor(x => x.RoleId).GreaterThan(0).WithMessage("Choose an active role.");
        RuleFor(x => x.Status)
            .Must(status => status is null || status is nameof(AccountStatus.Active) or nameof(AccountStatus.Inactive))
            .WithMessage("Status must be Active or Inactive.");
        RuleFor(x => x.InitialPassword)
            .Custom((password, context) =>
            {
                if (SecurityPolicy.PasswordViolation(password) is { } problem)
                {
                    context.AddFailure(problem);
                }
            });
    }
}

internal sealed class UserUpdateRequestValidator : RequestValidator<UserUpdateRequest>
{
    public UserUpdateRequestValidator()
    {
        RuleFor(x => x.RoleId).GreaterThan(0).WithMessage("Choose an active role.");
        RuleForText(x => x.RowVersion).RequiredRowVersion();
    }
}

/// <summary>
/// Creates an account for a current employee that has none. The email is the work email of the employee (lower case); the
/// password is hashed by Identity inside <see cref="IAccountLogins"/> and never leaves it: it is not stored, returned, logged
/// or written to the activity feed (which names the user by display name only).
/// </summary>
internal sealed class CreateUserHandler(
    IValidator<UserCreateRequest> validator,
    IRepository<Employee> employees,
    IRepository<AppAccount> accounts,
    IRepository<AppRole> roles,
    IAccountLogins logins,
    IActivityRecorder activity,
    IUnitOfWork unitOfWork,
    UserMapping mapper) : ICreateHandler<UserCreateRequest, UserDto>
{
    public async Task<Result<UserDto>> HandleAsync(UserCreateRequest request, CancellationToken cancellationToken)
    {
        var valid = await validator.CheckAsync(request, cancellationToken);
        if (valid.IsFailure)
        {
            return Result.Failure<UserDto>(valid.Error);
        }

        var employee = await employees.FindAsync(request.EmployeeId, cancellationToken);
        if (employee is null || employee.ExitDate is not null)
        {
            return Fail("Choose a current employee.", "employeeId", "Choose a current employee.");
        }

        var employeeId = employee.Id;
        if (await accounts.AnyAsync(account => account.EmployeeId == employeeId, cancellationToken))
        {
            return Fail("This employee already has a user account.", "employeeId", "A user already exists for this employee.");
        }

        var role = await UserRules.FindActiveRoleAsync(roles, request.RoleId, cancellationToken);
        if (role.IsFailure)
        {
            return Result.Failure<UserDto>(role.Error);
        }

        var email = employee.WorkEmail.Trim().ToLowerInvariant();
        var displayName = $"{employee.FirstName} {employee.LastName}".Trim();
        if (email.Length == 0 || email.Length > AppAccount.EmailMaxLength || displayName.Length > AppAccount.DisplayNameMaxLength)
        {
            return Fail("The employee's name or work email cannot be used for an account.", "employeeId", "Fix the work email of the employee first.");
        }

        if (await accounts.AnyAsync(account => account.Email == email, cancellationToken))
        {
            return Fail("An account with this email address already exists.", "employeeId", "This work email is already used by another account.");
        }

        // The identity user and the account are one change: a single transaction on the shared database context, so a failed
        // account save rolls the identity user back too and no login can be left without an account.
        var begun = await unitOfWork.BeginTransactionAsync(exclusiveLock: null, cancellationToken);
        if (begun.IsFailure)
        {
            return Result.Failure<UserDto>(begun.Error);
        }

        await using var transaction = begun.Value;
        var login = await logins.CreateAsync(email, request.InitialPassword!, cancellationToken);
        if (login.IsFailure)
        {
            return Result.Failure<UserDto>(login.Error);
        }

        var status = request.Status == nameof(AccountStatus.Inactive) ? AccountStatus.Inactive : AccountStatus.Active;
        var account = AppAccount.Create(employee.Id, displayName, email, role.Value.Id, login.Value, status);
        accounts.Add(account);
        activity.Record(ActivityCategories.System, ActivityMessages.Created(mapper.Label, mapper.Describe(account)));

        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        if (saved.IsFailure)
        {
            return Result.Failure<UserDto>(saved.Error);
        }

        await transaction.CommitAsync(cancellationToken);
        return Result.Success(UserMapping.ToDto(account, lockedOut: false));
    }

    private static Result<UserDto> Fail(string message, string field, string fieldMessage) =>
        Result.Failure<UserDto>(AppError.Validation(message, field, fieldMessage));
}

/// <summary>Changes the role of a user (nothing else: the employee is fixed and the status moves are commands).</summary>
internal sealed class UpdateUserHandler(
    IValidator<UserUpdateRequest> validator,
    EntityLoader<AppAccount> loader,
    IRepository<Employee> employees,
    IRepository<AppAccount> accounts,
    IRepository<AppRole> roles,
    ICurrentUser currentUser,
    IAccountLogins logins,
    IActivityRecorder activity,
    IUnitOfWork unitOfWork,
    UserMapping mapper) : IUpdateHandler<UserUpdateRequest, UserDto>
{
    public async Task<Result<UserDto>> HandleAsync(int id, UserUpdateRequest request, CancellationToken cancellationToken)
    {
        var valid = await validator.CheckAsync(request, cancellationToken);
        if (valid.IsFailure)
        {
            return Result.Failure<UserDto>(valid.Error);
        }

        var loaded = await loader.LoadForChangeAsync(id, request.RowVersion, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result.Failure<UserDto>(loaded.Error);
        }

        var account = loaded.Value;
        var role = await UserRules.FindActiveRoleAsync(roles, request.RoleId, cancellationToken);
        if (role.IsFailure)
        {
            return Result.Failure<UserDto>(role.Error);
        }

        if (request.RoleId != account.RoleId)
        {
            var notSelf = UserRules.EnsureNotSelf(account, currentUser, UserRules.OwnAccountMessage);
            if (notSelf.IsFailure)
            {
                return Result.Failure<UserDto>(notSelf.Error);
            }

            // The rule check and the write run under the administrator lock in one transaction (see ChangeAdministratorAsync).
            var changed = await UserRules.ChangeAdministratorAsync(
                unitOfWork,
                accounts,
                employees,
                roles,
                account,
                () =>
                {
                    account.ChangeRole(request.RoleId);
                    activity.Record(ActivityCategories.System, ActivityMessages.Updated(mapper.Label, mapper.Describe(account)));
                },
                afterSave: null,
                cancellationToken);
            return changed.IsFailure
                ? Result.Failure<UserDto>(changed.Error)
                : Result.Success(UserMapping.ToDto(account, await UserRules.IsLockedAsync(logins, account, cancellationToken)));
        }

        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        return saved.IsFailure
            ? Result.Failure<UserDto>(saved.Error)
            : Result.Success(UserMapping.ToDto(account, await UserRules.IsLockedAsync(logins, account, cancellationToken)));
    }
}

/// <summary>Activate: a locked account must be unlocked first, and the role of the user must be active.</summary>
internal sealed class ActivateUserHandler(
    EntityLoader<AppAccount> loader,
    IRepository<Employee> employees,
    IRepository<AppRole> roles,
    IAccountLogins logins,
    IActivityRecorder activity,
    IUnitOfWork unitOfWork,
    UserMapping mapper) : ICommandHandler<ActivateCommand, UserDto>
{
    public async Task<Result<UserDto>> HandleAsync(ActivateCommand command, CancellationToken cancellationToken)
    {
        var loaded = await loader.LoadForChangeAsync(command.Id, command.RowVersion, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result.Failure<UserDto>(loaded.Error);
        }

        var account = loaded.Value;
        if (account.Status != AccountStatus.Active)
        {
            if (account.EmployeeId is { } employeeId
                && await employees.AnyAsync(employee => employee.Id == employeeId && employee.ExitDate != null, cancellationToken))
            {
                return Result.Failure<UserDto>(AppError.Conflict(UserRules.LeaverActivationMessage));
            }

            var role = await UserRules.FindActiveRoleAsync(roles, account.RoleId, cancellationToken);
            if (role.IsFailure)
            {
                return Result.Failure<UserDto>(role.Error);
            }

            var activated = account.Activate();
            if (activated.IsFailure)
            {
                return Result.Failure<UserDto>(activated.Error);
            }

            activity.Record(ActivityCategories.System, ActivityMessages.Updated(mapper.Label, mapper.Describe(account)));
        }

        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        return saved.IsFailure
            ? Result.Failure<UserDto>(saved.Error)
            : Result.Success(UserMapping.ToDto(account, await UserRules.IsLockedAsync(logins, account, cancellationToken)));
    }
}

/// <summary>Deactivate: not your own account, and never the last active HR Administrator.</summary>
internal sealed class DeactivateUserHandler(
    EntityLoader<AppAccount> loader,
    IRepository<Employee> employees,
    IRepository<AppAccount> accounts,
    IRepository<AppRole> roles,
    ICurrentUser currentUser,
    IAccountLogins logins,
    IActivityRecorder activity,
    IUnitOfWork unitOfWork,
    UserMapping mapper) : ICommandHandler<DeactivateCommand, UserDto>
{
    public async Task<Result<UserDto>> HandleAsync(DeactivateCommand command, CancellationToken cancellationToken)
    {
        var loaded = await loader.LoadForChangeAsync(command.Id, command.RowVersion, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result.Failure<UserDto>(loaded.Error);
        }

        var account = loaded.Value;
        if (account.Status != AccountStatus.Inactive)
        {
            var notSelf = UserRules.EnsureNotSelf(account, currentUser, UserRules.OwnAccountMessage);
            if (notSelf.IsFailure)
            {
                return Result.Failure<UserDto>(notSelf.Error);
            }

            var changed = await UserRules.ChangeAdministratorAsync(
                unitOfWork,
                accounts,
                employees,
                roles,
                account,
                () =>
                {
                    account.Deactivate();
                    activity.Record(ActivityCategories.System, ActivityMessages.Updated(mapper.Label, mapper.Describe(account)));
                },
                afterSave: async () =>
                {
                    // Rotate the security stamp so an open session of the deactivated user ends at its next request.
                    if (account.ExternalSubject is { } subject)
                    {
                        await logins.InvalidateSessionsAsync(subject, cancellationToken);
                    }
                },
                cancellationToken);
            return changed.IsFailure
                ? Result.Failure<UserDto>(changed.Error)
                : Result.Success(UserMapping.ToDto(account, await UserRules.IsLockedAsync(logins, account, cancellationToken)));
        }

        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        return saved.IsFailure
            ? Result.Failure<UserDto>(saved.Error)
            : Result.Success(UserMapping.ToDto(account, await UserRules.IsLockedAsync(logins, account, cancellationToken)));
    }
}

/// <summary>Unlock: clears the lockout after failed sign-ins and turns a Locked status back to Active.</summary>
internal sealed class UnlockUserHandler(
    EntityLoader<AppAccount> loader,
    IAccountLogins logins,
    IActivityRecorder activity,
    IUnitOfWork unitOfWork,
    UserMapping mapper) : ICommandHandler<UnlockUserCommand, UserDto>
{
    public async Task<Result<UserDto>> HandleAsync(UnlockUserCommand command, CancellationToken cancellationToken)
    {
        var loaded = await loader.LoadForChangeAsync(command.Id, command.RowVersion, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result.Failure<UserDto>(loaded.Error);
        }

        var account = loaded.Value;
        var wasLocked = account.Status == AccountStatus.Locked || await UserRules.IsLockedAsync(logins, account, cancellationToken);
        account.Unlock();
        if (wasLocked)
        {
            activity.Record(ActivityCategories.System, ActivityMessages.Updated(mapper.Label, mapper.Describe(account)));
        }

        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        if (saved.IsFailure)
        {
            return Result.Failure<UserDto>(saved.Error);
        }

        if (account.ExternalSubject is { } subject)
        {
            await logins.UnlockAsync(subject, cancellationToken);
        }

        return Result.Success(UserMapping.ToDto(account, lockedOut: false));
    }
}

internal sealed class DeleteUserHandler(
    EntityLoader<AppAccount> loader,
    IRepository<Employee> employees,
    IRepository<AppAccount> accounts,
    IRepository<AppRole> roles,
    ICurrentUser currentUser,
    IAccountLogins logins,
    IActivityRecorder activity,
    IUnitOfWork unitOfWork,
    UserMapping mapper) : IDeleteHandler<UserDto>
{
    public async Task<Result> HandleAsync(int id, string? rowVersion, CancellationToken cancellationToken)
    {
        var loaded = await loader.LoadForChangeAsync(id, rowVersion, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result.Failure(loaded.Error);
        }

        var account = loaded.Value;
        var notSelf = UserRules.EnsureNotSelf(account, currentUser, UserRules.OwnDeleteMessage);
        if (notSelf.IsFailure)
        {
            return notSelf;
        }

        // The account row and the identity user go in one transaction, under the administrator lock: a failure leaves both.
        return await UserRules.ChangeAdministratorAsync(
            unitOfWork,
            accounts,
            employees,
            roles,
            account,
            () =>
            {
                activity.Record(ActivityCategories.System, ActivityMessages.Deleted(mapper.Label, mapper.Describe(account)));
                accounts.Remove(account);
            },
            afterSave: async () =>
            {
                if (account.ExternalSubject is { } subject)
                {
                    await logins.InvalidateSessionsAsync(subject, cancellationToken);
                    await logins.DeleteAsync(subject, cancellationToken);
                }
            },
            cancellationToken);
    }
}

/// <summary>
/// An administrator asks for the password reset email of a user (the same mail forgot-password sends: token, link and mail
/// all stay on the server). It fails closed with <see cref="ErrorType.Unavailable"/> (HTTP 503) when email is not configured,
/// before anything else happens. It changes nothing about the account: not the password, the lock state or the sessions (the
/// reset itself changes the security stamp when the user uses the link).
/// </summary>
internal sealed class UserPasswordResetHandler(
    EntityLoader<AppAccount> loader,
    IRepository<AppRole> roles,
    IRepository<Employee> employees,
    IEmailSender email,
    IAccountLogins logins,
    IActivityRecorder activity,
    IUnitOfWork unitOfWork,
    UserMapping mapper,
    ICurrentUser currentUser) : IUserPasswordResetHandler
{
    public const string OwnAccountMessage = "You cannot send a password reset to your own account. Use Forgot password on the sign-in page.";
    public const string LockedMessage = "Unlock the account before sending a password reset.";
    public const string NotActiveMessage = "Activate the account before sending a password reset.";
    public const string RoleInactiveMessage = "The account's role is inactive. Activate the role before sending a password reset.";
    public const string LeaverMessage = "This user's employee has left the company, so no password reset can be sent.";
    public const string NoLoginMessage = "This account has no email address or sign-in to send a password reset to.";
    public const string NotConfiguredMessage = "Password reset by email is not configured on this server. Ask an administrator to configure email.";
    public const string SendFailedMessage = "The password reset email could not be sent. Try again later.";

    public async Task<Result> HandleAsync(int id, CancellationToken cancellationToken)
    {
        try
        {
            email.EnsureAvailable();
        }
        catch (EmailNotConfiguredException)
        {
            return Result.Failure(AppError.Unavailable(NotConfiguredMessage));
        }

        var loaded = await loader.LoadAsync(id, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result.Failure(loaded.Error);
        }

        var account = loaded.Value;
        var notSelf = UserRules.EnsureNotSelf(account, currentUser, OwnAccountMessage);
        if (notSelf.IsFailure)
        {
            return notSelf;
        }

        if (account.Status == AccountStatus.Locked)
        {
            return Result.Failure(AppError.Conflict(LockedMessage));
        }

        if (account.Status != AccountStatus.Active)
        {
            return Result.Failure(AppError.Conflict(NotActiveMessage));
        }

        // Locked out by failed sign-ins (the Identity lockout): unlock first, as for a stored Locked status.
        if (await UserRules.IsLockedAsync(logins, account, cancellationToken))
        {
            return Result.Failure(AppError.Conflict(LockedMessage));
        }

        var role = await roles.FindAsync(account.RoleId, cancellationToken);
        if (role is not { IsActive: true })
        {
            return Result.Failure(AppError.Conflict(RoleInactiveMessage));
        }

        if (account.EmployeeId is { } employeeId
            && await employees.AnyAsync(employee => employee.Id == employeeId && employee.ExitDate != null, cancellationToken))
        {
            return Result.Failure(AppError.Conflict(LeaverMessage));
        }

        if (account.ExternalSubject is null || string.IsNullOrWhiteSpace(account.Email))
        {
            return Result.Failure(AppError.Conflict(NoLoginMessage));
        }

        try
        {
            await logins.SendPasswordResetAsync(account.ExternalSubject, cancellationToken);
        }
        catch (EmailNotConfiguredException)
        {
            return Result.Failure(AppError.Unavailable(NotConfiguredMessage));
        }
#pragma warning disable CA1031 // A mail server failure is reported as "unavailable"; the exception text could hold the address.
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Result.Failure(AppError.Unavailable(SendFailedMessage));
        }
#pragma warning restore CA1031

        // The note names the user by display name only: no address, never a token.
        activity.Record(ActivityCategories.System, $"Password reset email sent for user \"{mapper.Describe(account)}\"");
        return await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
