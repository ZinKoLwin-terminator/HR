using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Organization;

namespace Apex.Hrms.Application.Organization.Companies;

/// <summary>Mirrors the Company interface in organization.models.ts. RowVersion is base64 in JSON.</summary>
public sealed record CompanyDto(
    int Id,
    string Code,
    string Name,
    string LegalName,
    string RegistrationNumber,
    string TaxId,
    string Industry,
    string Email,
    string Phone,
    string Website,
    string Address,
    string City,
    string Country,
    DateOnly FoundedDate,
    string Status,
    byte[] RowVersion) : IIdentified;

/// <summary>The fields a company has on create and update.</summary>
internal interface ICompanyFields
{
    string Code { get; }

    string Name { get; }

    string LegalName { get; }

    string? RegistrationNumber { get; }

    string? TaxId { get; }

    string Industry { get; }

    string Email { get; }

    string Phone { get; }

    string? Website { get; }

    string? Address { get; }

    string City { get; }

    string Country { get; }

    DateOnly FoundedDate { get; }
}

/// <summary>POST body. Status is the status the new company starts with.</summary>
public sealed record CompanyCreateRequest(
    string Code,
    string Name,
    string LegalName,
    string? RegistrationNumber,
    string? TaxId,
    string Industry,
    string Email,
    string Phone,
    string? Website,
    string? Address,
    string City,
    string Country,
    DateOnly FoundedDate,
    string Status) : ICompanyFields, ICreateRequest<CompanyDetails>
{
    public CompanyDetails ToDetails() => CompanyDetailsFactory.From(this);

    public RecordStatus ParseStatus() => Enum.Parse<RecordStatus>(Status);
}

/// <summary>PUT body. It has no status (use activate and deactivate) and requires the row version.</summary>
public sealed record CompanyUpdateRequest(
    string Code,
    string Name,
    string LegalName,
    string? RegistrationNumber,
    string? TaxId,
    string Industry,
    string Email,
    string Phone,
    string? Website,
    string? Address,
    string City,
    string Country,
    DateOnly FoundedDate,
    string? RowVersion) : ICompanyFields, IDetailsRequest<CompanyDetails>, IVersionedRequest
{
    public CompanyDetails ToDetails() => CompanyDetailsFactory.From(this);
}

internal static class CompanyDetailsFactory
{
    public static CompanyDetails From(ICompanyFields fields) => new CompanyDetails(
        fields.Code,
        fields.Name,
        fields.LegalName,
        fields.RegistrationNumber,
        fields.TaxId,
        fields.Industry,
        fields.Email,
        fields.Phone,
        fields.Website,
        fields.Address,
        fields.City,
        fields.Country,
        fields.FoundedDate).Normalized();
}

public sealed record CompanyFilter(string? Status = null);
