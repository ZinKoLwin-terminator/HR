using System.Linq.Expressions;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Organization;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Attendance;
using Apex.Hrms.Domain.Common;
using FluentValidation;

namespace Apex.Hrms.Application.Attendance.Holidays;

/// <summary>Mirrors the Holiday interface in attendance.models.ts. <c>Type</c> is "Public" or "Company".</summary>
public sealed record HolidayDto(int Id, DateOnly Date, string Name, string Type, byte[] RowVersion) : IIdentified;

internal interface IHolidayFields
{
    DateOnly Date { get; }

    string Name { get; }

    string Type { get; }
}

/// <summary>POST body. A holiday has no status.</summary>
public sealed record HolidayCreateRequest(DateOnly Date, string Name, string Type) : IHolidayFields, ICreateRequest<HolidayDetails>
{
    public HolidayDetails ToDetails() => new HolidayDetails(Date, Name, Type).Normalized();

    // Holidays have no status; the shared create flow asks for one and the factory ignores it.
    public RecordStatus ParseStatus() => RecordStatus.Active;
}

/// <summary>PUT body. Requires the row version.</summary>
public sealed record HolidayUpdateRequest(DateOnly Date, string Name, string Type, string? RowVersion)
    : IHolidayFields, IDetailsRequest<HolidayDetails>, IVersionedRequest
{
    public HolidayDetails ToDetails() => new HolidayDetails(Date, Name, Type).Normalized();
}

/// <summary>Filters of the list: an inclusive date range (the calendar asks for a month) and the type.</summary>
public sealed record HolidayFilter(DateOnly? DateFrom = null, DateOnly? DateTo = null, string? Type = null);

internal sealed class HolidayMapping :
    IEntityDescriptor<Holiday>,
    IDtoMapper<Holiday, HolidayDto>,
    IEntityFactory<Holiday, HolidayDetails>
{
    public string Label => "Holiday";

    public string Describe(Holiday entity) => $"{entity.Name} ({entity.Date:yyyy-MM-dd})";

    public HolidayDto ToDto(Holiday entity) => new(entity.Id, entity.Date, entity.Name, entity.Type, entity.RowVersion);

    public Result Validate(HolidayDetails details, Holiday? existing) => Holiday.Validate(details);

    public Result<Holiday> Create(HolidayDetails details, RecordStatus status) => Holiday.Create(details);

    public Result Update(Holiday entity, HolidayDetails details) => entity.Update(details);
}

/// <summary>Holidays are unique by date.</summary>
internal sealed class HolidaySaveRules(IRepository<Holiday> holidays) : ISaveRules<Holiday, HolidayDetails>
{
    public Task<Result> CheckCreateAsync(HolidayDetails details, CancellationToken cancellationToken) =>
        EnsureUniqueDateAsync(details, excludedId: 0, cancellationToken);

    public Task<Result> CheckUpdateAsync(Holiday existing, HolidayDetails details, CancellationToken cancellationToken) =>
        EnsureUniqueDateAsync(details, existing.Id, cancellationToken);

    private async Task<Result> EnsureUniqueDateAsync(HolidayDetails details, int excludedId, CancellationToken cancellationToken)
    {
        var date = details.Date;
        return await holidays.AnyAsync(holiday => holiday.Date == date && holiday.Id != excludedId, cancellationToken)
            ? Result.Failure(AppError.Validation(
                $"A holiday already exists on {date:yyyy-MM-dd}.",
                "date",
                "Date must be unique."))
            : Result.Success();
    }
}

internal sealed class HolidayQuerySpecification : IQuerySpecification<Holiday, HolidayFilter>
{
    public IReadOnlyDictionary<string, Expression<Func<Holiday, object>>> SortKeys { get; } =
        new Dictionary<string, Expression<Func<Holiday, object>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = h => h.Id,
            ["date"] = h => h.Date,
            ["name"] = h => h.Name,
            ["type"] = h => h.Type,
        };

    public Expression<Func<Holiday, bool>> Search(string term) => Predicates.ContainsAny<Holiday>(term, h => h.Name);

    public Expression<Func<Holiday, bool>> Filter(HolidayFilter filter) => Predicates.Always<Holiday>()
        .And(filter.DateFrom is { } from ? h => h.Date >= from : Predicates.Always<Holiday>())
        .And(filter.DateTo is { } to ? h => h.Date <= to : Predicates.Always<Holiday>())
        .And(TypeFilter(filter.Type));

    private static Expression<Func<Holiday, bool>> TypeFilter(string? type) =>
        FilterPredicates.IsSet(type)
            ? type is Holiday.PublicType or Holiday.CompanyType ? h => h.Type == type : Predicates.Never<Holiday>()
            : Predicates.Always<Holiday>();
}

internal abstract class HolidayFieldsValidator<TRequest> : RequestValidator<TRequest>
    where TRequest : IHolidayFields
{
    protected HolidayFieldsValidator() => RuleForText(x => x.Name, "Holiday name").RequiredText(100);
}

internal sealed class HolidayCreateRequestValidator : HolidayFieldsValidator<HolidayCreateRequest>;

internal sealed class HolidayUpdateRequestValidator : HolidayFieldsValidator<HolidayUpdateRequest>
{
    public HolidayUpdateRequestValidator() => RuleForText(x => x.RowVersion).RequiredRowVersion();
}
