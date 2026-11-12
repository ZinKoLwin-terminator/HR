using System.Globalization;
using System.Linq.Expressions;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Activity;
using Apex.Hrms.Application.Organization;
using Apex.Hrms.Application.References;
using Apex.Hrms.Application.Security;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Security;
using FluentValidation;

namespace Apex.Hrms.Application.Settings;

internal sealed class RoleMapping :
    IEntityDescriptor<AppRole>,
    IDtoMapper<AppRole, RoleDto>,
    IEntityFactory<AppRole, RoleDetails>
{
    public string Label => "Role";

    public string Describe(AppRole entity) => entity.Name;

    public RoleDto ToDto(AppRole entity) => new(
        entity.Id,
        entity.Name,
        entity.Description ?? string.Empty,
        entity.IsSystem,
        entity.Status.ToString(),
        entity.RowVersion);

    public Result Validate(RoleDetails details, AppRole? existing) => AppRole.Validate(details, existing);

    public Result<AppRole> Create(RoleDetails details, RecordStatus status) => AppRole.Create(details, status);

    public Result Update(AppRole entity, RoleDetails details) => entity.Update(details);
}

internal sealed class RoleQuerySpecification : IQuerySpecification<AppRole, RoleFilter>
{
    public IReadOnlyDictionary<string, Expression<Func<AppRole, object>>> SortKeys { get; } =
        new Dictionary<string, Expression<Func<AppRole, object>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = r => r.Id,
            ["name"] = r => r.Name,
            ["description"] = r => r.Description!,
            ["isSystem"] = r => r.IsSystem,
            ["status"] = r => r.Status,
        };

    public Expression<Func<AppRole, bool>> Search(string term) =>
        Predicates.ContainsAny<AppRole>(term, r => r.Name, r => r.Description);

    public Expression<Func<AppRole, bool>> Filter(RoleFilter filter)
    {
        if (!FilterPredicates.IsSet(filter.Status))
        {
            return Predicates.Always<AppRole>();
        }

        return RecordStatusText.TryParse(filter.Status, out var status)
            ? role => role.Status == status
            : Predicates.Never<AppRole>();
    }
}

internal abstract class RoleFieldsValidator<TRequest> : RequestValidator<TRequest>
    where TRequest : IRoleFields
{
    protected RoleFieldsValidator()
    {
        RuleForText(x => x.Name, "Role name").RequiredText(AppRole.NameMaxLength);
        RuleForText(x => x.Description, "Description").OptionalText(AppRole.DescriptionMaxLength);
    }
}

internal sealed class RoleCreateRequestValidator : RoleFieldsValidator<RoleCreateRequest>
{
    public RoleCreateRequestValidator() =>
        RuleFor(x => x.Status)
            .Must(status => status is null || RecordStatusText.TryParse(status, out _))
            .WithMessage("Status must be Active or Inactive.");
}

internal sealed class RoleUpdateRequestValidator : RoleFieldsValidator<RoleUpdateRequest>
{
    public RoleUpdateRequestValidator() => RuleForText(x => x.RowVersion).RequiredRowVersion();
}

/// <summary>A role name is unique (the database compares case-insensitively).</summary>
internal sealed class RoleRules(IRepository<AppRole> roles) : ISaveRules<AppRole, RoleDetails>
{
    public Task<Result> CheckCreateAsync(RoleDetails details, CancellationToken cancellationToken) =>
        CheckAsync(details, 0, cancellationToken);

    public Task<Result> CheckUpdateAsync(AppRole existing, RoleDetails details, CancellationToken cancellationToken) =>
        CheckAsync(details, existing.Id, cancellationToken);

    private Task<Result> CheckAsync(RoleDetails details, int excludedId, CancellationToken cancellationToken)
    {
        var name = details.Name;
        return HierarchyChecks.EnsureUniqueAsync(roles, role => role.Name == name && role.Id != excludedId, "Role name", "name", name, cancellationToken);
    }
}

internal sealed class ActivateRoleAction : ICommandAction<AppRole, ActivateCommand>
{
    public Result Execute(AppRole entity, ActivateCommand command)
    {
        entity.Activate();
        return Result.Success();
    }
}

/// <summary>A built-in role stays active, and a role that active users hold cannot be deactivated.</summary>
internal sealed class DeactivateRoleHandler(
    EntityLoader<AppRole> loader,
    IRecordCounter<AppAccount> accounts,
    IActivityRecorder activity,
    IUnitOfWork unitOfWork,
    IRoleGrantInvalidator grantCache,
    RoleMapping mapper) : ICommandHandler<DeactivateCommand, RoleDto>
{
    public async Task<Result<RoleDto>> HandleAsync(DeactivateCommand command, CancellationToken cancellationToken)
    {
        var loaded = await loader.LoadForChangeAsync(command.Id, command.RowVersion, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result.Failure<RoleDto>(loaded.Error);
        }

        var role = loaded.Value;
        if (role.IsActive)
        {
            var deactivated = role.Deactivate();
            if (deactivated.IsFailure)
            {
                return Result.Failure<RoleDto>(deactivated.Error);
            }

            var roleId = role.Id;
            var active = await accounts.CountAsync(account => account.RoleId == roleId && account.Status == AccountStatus.Active, cancellationToken);
            if (active > 0)
            {
                return Result.Failure<RoleDto>(AppError.Conflict(
                    string.Create(CultureInfo.InvariantCulture, $"{active} active user(s) still have this role. Move them to another role first.")));
            }

            activity.Record(Domain.Activity.ActivityCategories.System, ActivityMessages.Updated(mapper.Label, mapper.Describe(role)));
        }

        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        if (saved.IsSuccess)
        {
            grantCache.Invalidate(role.Id);
        }

        return saved.IsFailure ? Result.Failure<RoleDto>(saved.Error) : Result.Success(mapper.ToDto(role));
    }
}

/// <summary>Deletes a custom role that no user holds. A built-in role is never deleted.</summary>
internal sealed class DeleteRoleHandler(
    EntityLoader<AppRole> loader,
    IEnumerable<IReferenceCheck<AppRole>> references,
    IRepository<AppRole> roles,
    IActivityRecorder activity,
    IUnitOfWork unitOfWork,
    IRoleGrantInvalidator grantCache,
    RoleMapping mapper) : IDeleteHandler<RoleDto>
{
    public async Task<Result> HandleAsync(int id, string? rowVersion, CancellationToken cancellationToken)
    {
        var loaded = await loader.LoadForChangeAsync(id, rowVersion, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result.Failure(loaded.Error);
        }

        var role = loaded.Value;
        if (role.IsSystem)
        {
            return Result.Failure(AppError.Conflict(AppRole.BuiltInCannotBeDeleted));
        }

        var usage = await ReferenceUsage.SummarizeAsync(references, ReferenceUse.Delete, id, cancellationToken);
        if (usage is not null)
        {
            return Result.Failure(AppError.Conflict(
                $"\"{role.Name}\" cannot be deleted because it is used by {usage}. Deactivate it instead."));
        }

        activity.Record(Domain.Activity.ActivityCategories.System, ActivityMessages.Deleted(mapper.Label, mapper.Describe(role)));
        roles.Remove(role);
        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        if (saved.IsSuccess)
        {
            grantCache.Invalidate(id);
        }

        return saved;
    }
}
