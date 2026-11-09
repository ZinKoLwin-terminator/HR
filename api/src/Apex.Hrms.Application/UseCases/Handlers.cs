using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Application.UseCases;

// One interface per CRUD contract method of the frontend (CrudDataService). They are keyed by the resource's
// DTO type, so the API layer asks for, e.g., IGetByIdHandler<CompanyDto> and never sees domain entities.

public interface IGetAllHandler<TDto>
{
    Task<Result<IReadOnlyList<TDto>>> HandleAsync(CancellationToken cancellationToken);
}

public interface IGetByIdHandler<TDto>
{
    Task<Result<TDto>> HandleAsync(int id, CancellationToken cancellationToken);
}

public interface IQueryHandler<TDto, TFilter>
{
    Task<Result<PagedResult<TDto>>> HandleAsync(PageRequest<TFilter> request, CancellationToken cancellationToken);
}

public interface ICreateHandler<in TRequest, TDto>
{
    Task<Result<TDto>> HandleAsync(TRequest request, CancellationToken cancellationToken);
}

public interface IUpdateHandler<in TRequest, TDto>
{
    Task<Result<TDto>> HandleAsync(int id, TRequest request, CancellationToken cancellationToken);
}

public interface IDeleteHandler<TDto>
{
    Task<Result> HandleAsync(int id, CancellationToken cancellationToken);
}
