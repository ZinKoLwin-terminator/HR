using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Organization;

namespace Apex.Hrms.Application.Organization.Companies;

internal sealed class CompanyMapping(IClock clock) :
    IEntityDescriptor<Company>,
    IDtoMapper<Company, CompanyDto>,
    IEntityFactory<Company, CompanyDetails>
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

    public Result Validate(CompanyDetails details, Company? existing) => Company.Validate(details, clock.Today);

    public Result<Company> Create(CompanyDetails details, RecordStatus status) =>
        Company.Create(details, status, clock.Today);

    public Result Update(Company entity, CompanyDetails details) => entity.Update(details, clock.Today);
}
