using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Activity;
using Apex.Hrms.Application.Security;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Activity;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Security;
using Apex.Hrms.Domain.Settings;

namespace Apex.Hrms.Application.Settings;

/// <summary>
/// The role permission matrix. The modules and actions come from the database (the reference data of the post-deployment
/// script), so a grant can only name an action that exists for its module. Rules of the mock service: unknown modules and
/// actions are refused and a grant other than View needs View of the same module. On top: the HR Administrator always keeps
/// every grant.
/// </summary>
internal sealed class PermissionMatrixHandler(
    IPermissionStore store,
    EntityLoader<AppRole> loader,
    IRepository<AppRole> roles,
    IActivityRecorder activity,
    IRoleGrantInvalidator grantCache) : IPermissionMatrixHandler
{
    public const string ViewAction = "View";
    public const string AdministratorMessage = "HR Administrator always has full access and cannot be changed.";

    public async Task<Result<PermissionMatrixDto>> GetMatrixAsync(CancellationToken cancellationToken)
    {
        var modules = await store.ModulesAsync(cancellationToken);
        var allRoles = await roles.ListAsync(Predicates.Always<AppRole>(), cancellationToken);
        var grants = await store.GrantsAsync(null, cancellationToken);
        var byRole = grants.ToLookup(grant => grant.RoleId);
        return Result.Success(new PermissionMatrixDto(
            [.. modules.Select(module => new PermissionModuleDto(module.Key, module.Label, module.Actions))],
            [.. allRoles.Select(role => ToDto(role, byRole[role.Id], modules))]));
    }

    public async Task<Result<RolePermissionsDto>> GetForRoleAsync(int roleId, CancellationToken cancellationToken)
    {
        var loaded = await loader.LoadAsync(roleId, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result.Failure<RolePermissionsDto>(loaded.Error);
        }

        var modules = await store.ModulesAsync(cancellationToken);
        var grants = await store.GrantsAsync(roleId, cancellationToken);
        return Result.Success(ToDto(loaded.Value, grants, modules));
    }

    public async Task<Result<RolePermissionsDto>> SaveAsync(int roleId, RolePermissionsUpdateRequest request, CancellationToken cancellationToken)
    {
        if (request.Grants is null)
        {
            return Result.Failure<RolePermissionsDto>(AppError.Validation("Grants are required.", "grants", "Grants are required."));
        }

        var loaded = await loader.LoadForChangeAsync(roleId, request.RowVersion, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result.Failure<RolePermissionsDto>(loaded.Error);
        }

        var role = loaded.Value;
        var modules = await store.ModulesAsync(cancellationToken);
        var clean = Clean(request.Grants, modules);
        if (clean.IsFailure)
        {
            return Result.Failure<RolePermissionsDto>(clean.Error);
        }

        if (role.Name == SecurityPolicy.AdministratorRoleName && !HasEveryGrant(clean.Value, modules))
        {
            return Result.Failure<RolePermissionsDto>(AppError.Conflict(AdministratorMessage));
        }

        var flat = clean.Value.SelectMany(pair => pair.Value.Select(action => (pair.Key, action))).ToList();
        activity.Record(ActivityCategories.System, $"Permissions of role \"{role.Name}\" updated");
        var saved = await store.ReplaceGrantsAsync(role, flat, cancellationToken);
        if (saved.IsFailure)
        {
            return Result.Failure<RolePermissionsDto>(saved.Error);
        }

        // Committed: the permission check must read the new grants from now on, not the cached ones.
        grantCache.Invalidate(roleId);

        // The role was saved with the grants, so it carries the new row version.
        var grants = await store.GrantsAsync(roleId, cancellationToken);
        return Result.Success(ToDto(role, grants, modules));
    }

    private static Result<Dictionary<string, List<string>>> Clean(Dictionary<string, string[]?> grants, IReadOnlyList<PermissionModuleInfo> modules)
    {
        var clean = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var (key, requested) in grants)
        {
            var module = modules.FirstOrDefault(candidate => candidate.Key == key);
            if (module is null)
            {
                return Fail($"Unknown module \"{key}\".", "grants", $"Unknown module \"{key}\".");
            }

            var actions = (requested ?? []).Distinct(StringComparer.Ordinal).ToList();
            var invalid = actions.FirstOrDefault(action => !module.Actions.Contains(action, StringComparer.Ordinal));
            if (invalid is not null)
            {
                return Fail($"{module.Label} has no \"{invalid}\" permission.", key, $"{module.Label} has no \"{invalid}\" permission.");
            }

            if (actions.Count > 0 && !actions.Contains(ViewAction, StringComparer.Ordinal))
            {
                return Fail($"{module.Label}: View is required to grant other permissions.", key, "View is required.");
            }

            // Keep the order of the module definition, so the response and the writes are stable.
            clean[key] = [.. module.Actions.Where(actions.Contains)];
        }

        return Result.Success(clean);
    }

    private static bool HasEveryGrant(Dictionary<string, List<string>> grants, IReadOnlyList<PermissionModuleInfo> modules) =>
        modules.All(module => grants.TryGetValue(module.Key, out var actions) && module.Actions.All(actions.Contains));

    private static Result<Dictionary<string, List<string>>> Fail(string message, string field, string fieldMessage) =>
        Result.Failure<Dictionary<string, List<string>>>(AppError.Validation(message, field, fieldMessage));

    private static RolePermissionsDto ToDto(AppRole role, IEnumerable<RoleGrant> grants, IReadOnlyList<PermissionModuleInfo> modules)
    {
        var byModule = grants.GroupBy(grant => grant.ModuleKey).ToDictionary(group => group.Key, group => group.Select(grant => grant.Action).ToList());
        var ordered = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (var module in modules)
        {
            if (byModule.TryGetValue(module.Key, out var actions))
            {
                ordered[module.Key] = [.. module.Actions.Where(actions.Contains)];
            }
        }

        return new RolePermissionsDto(role.Id, role.Id, ordered, role.RowVersion);
    }
}

internal sealed class GeneralSettingsHandler(
    EntityLoader<GeneralSettings> loader,
    IActivityRecorder activity,
    IUnitOfWork unitOfWork) : IGeneralSettingsHandler
{
    public async Task<Result<GeneralSettingsDto>> GetAsync(CancellationToken cancellationToken)
    {
        var loaded = await loader.LoadAsync(GeneralSettings.SingletonId, cancellationToken);
        return loaded.IsFailure ? Result.Failure<GeneralSettingsDto>(loaded.Error) : Result.Success(ToDto(loaded.Value));
    }

    public async Task<Result<GeneralSettingsDto>> SaveAsync(GeneralSettingsUpdateRequest request, CancellationToken cancellationToken)
    {
        var details = request.ToDetails();
        var valid = GeneralSettings.Validate(details);
        var version = RowVersionToken.Decode(request.RowVersion);
        if (valid.IsFailure)
        {
            // The row version problem is reported with the field errors, so one round trip shows everything.
            return Result.Failure<GeneralSettingsDto>(
                version.IsFailure && valid.Error.FieldErrors is { } fields
                    ? AppError.Validation(valid.Error.Message, new Dictionary<string, string[]>(fields) { [RowVersionToken.Field] = version.Error.FieldErrors![RowVersionToken.Field] })
                    : valid.Error);
        }

        var loaded = await loader.LoadForChangeAsync(GeneralSettings.SingletonId, request.RowVersion, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result.Failure<GeneralSettingsDto>(loaded.Error);
        }

        var updated = loaded.Value.Update(details);
        if (updated.IsFailure)
        {
            return Result.Failure<GeneralSettingsDto>(updated.Error);
        }

        activity.Record(ActivityCategories.System, "General settings updated");
        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        return saved.IsFailure ? Result.Failure<GeneralSettingsDto>(saved.Error) : Result.Success(ToDto(loaded.Value));
    }

    private static GeneralSettingsDto ToDto(GeneralSettings settings) => new(
        settings.Id,
        settings.OrganizationName,
        settings.ContactEmail,
        settings.Timezone,
        settings.DateFormat,
        settings.CurrencyCode,
        settings.FiscalYearStartMonth,
        settings.EmailNotifications,
        settings.WeeklyDigest,
        settings.RowVersion);
}
