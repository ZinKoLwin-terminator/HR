using Apex.Hrms.Application.UseCases;

namespace Apex.Hrms.Application.Organization.Companies;

/// <summary>Mirrors the Company interface in organization.models.ts.</summary>
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

public sealed record CompanyRequest(
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
    string Status,
    byte[]? RowVersion = null) : IVersionedRequest;

public sealed record CompanyFilter(string? Status = null);
