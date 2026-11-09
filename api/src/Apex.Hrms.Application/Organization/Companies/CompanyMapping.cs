using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Organization;

namespace Apex.Hrms.Application.Organization.Companies;

internal sealed class CompanyMapping(IClock clock) :
    IEntityDescriptor<Company>,
    IDtoMapper<Company, CompanyDto>,
    IEntityFactory<Company, CompanyRequest>
{
    public string Label => "Company";

    public string Describe(Company entity) => entity.Name;

    public CompanyDto ToDto(Company entity) => new(
        entity.Id,
        entity.Code,
        entity.Name,
        entity.LegalName,
        entity.RegistrationNumber ?? string.Empty,
        entity.TaxId ?? string.Empty,
        entity.Industry,
        entity.Email,
        entity.Phone,
        entity.Website ?? string.Empty,
        entity.Address ?? string.Empty,
        entity.City,
        entity.Country,
        entity.FoundedDate,
        entity.Status.ToString(),
        entity.RowVersion);

    public Result<Company> Create(CompanyRequest request) => Company.Create(ToDetails(request), clock.Today);

    public Result Update(Company entity, CompanyRequest request) => entity.Update(ToDetails(request), clock.Today);

    private static CompanyDetails ToDetails(CompanyRequest request) => new(
        request.Code,
        request.Name,
        request.LegalName,
        request.RegistrationNumber,
        request.TaxId,
        request.Industry,
        request.Email,
        request.Phone,
        request.Website,
        request.Address,
        request.City,
        request.Country,
        request.FoundedDate,
        Enum.Parse<RecordStatus>(request.Status));
}
