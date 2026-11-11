using System.Linq.Expressions;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Organization;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Recruitment;
using FluentValidation;

namespace Apex.Hrms.Application.Recruitment.Candidates;

/// <summary>
/// Mirrors the Candidate interface in recruitment.models.ts. The email is lower-case. <c>Source</c> is "Career site",
/// "Referral", "Job board", "LinkedIn" or "Agency". Personal data of a non-employee: handle with care.
/// </summary>
public sealed record CandidateDto(
    int Id,
    string FirstName,
    string LastName,
    string Email,
    string Phone,
    string Source,
    string? CurrentEmployer,
    decimal ExperienceYears,
    string? ResumeName,
    string? Notes,
    byte[] RowVersion) : IIdentified;

internal interface ICandidateFields
{
    string FirstName { get; }

    string LastName { get; }

    string Email { get; }

    string Phone { get; }

    string Source { get; }

    string? CurrentEmployer { get; }

    decimal ExperienceYears { get; }

    string? ResumeName { get; }

    string? Notes { get; }
}

internal static class CandidateDetailsFactory
{
    /// <summary>Call only after the request validator passed (the source text is known to be valid).</summary>
    public static CandidateDetails From(ICandidateFields fields) => new CandidateDetails(
        fields.FirstName,
        fields.LastName,
        fields.Email,
        fields.Phone,
        CandidateSourceText.Parse(fields.Source.Trim()),
        fields.CurrentEmployer,
        fields.ExperienceYears,
        fields.ResumeName,
        fields.Notes).Normalized();
}

/// <summary>POST body. A candidate has no status.</summary>
public sealed record CandidateCreateRequest(
    string FirstName,
    string LastName,
    string Email,
    string Phone,
    string Source,
    string? CurrentEmployer,
    decimal ExperienceYears,
    string? ResumeName,
    string? Notes) : ICandidateFields, ICreateRequest<CandidateDetails>
{
    public CandidateDetails ToDetails() => CandidateDetailsFactory.From(this);

    // Candidates have no status; the shared create flow asks for one and the factory ignores it.
    public RecordStatus ParseStatus() => RecordStatus.Active;
}

/// <summary>PUT body. Requires the row version.</summary>
public sealed record CandidateUpdateRequest(
    string FirstName,
    string LastName,
    string Email,
    string Phone,
    string Source,
    string? CurrentEmployer,
    decimal ExperienceYears,
    string? ResumeName,
    string? Notes,
    string? RowVersion) : ICandidateFields, IDetailsRequest<CandidateDetails>, IVersionedRequest
{
    public CandidateDetails ToDetails() => CandidateDetailsFactory.From(this);
}

/// <summary>Filters of the list; <c>source</c> is the text of a candidate source, "all" or blank for no filter.</summary>
public sealed record CandidateFilter(string? Source = null);

internal sealed class CandidateMapping :
    IEntityDescriptor<Candidate>,
    IDtoMapper<Candidate, CandidateDto>,
    IEntityFactory<Candidate, CandidateDetails>
{
    public string Label => "Candidate";

    public string Describe(Candidate entity) => entity.FullName;

    public CandidateDto ToDto(Candidate entity) => new(
        entity.Id,
        entity.FirstName,
        entity.LastName,
        entity.Email,
        entity.Phone,
        CandidateSourceText.ToText(entity.Source),
        entity.CurrentEmployer,
        entity.ExperienceYears,
        entity.ResumeName,
        entity.Notes,
        entity.RowVersion);

    public Result Validate(CandidateDetails details, Candidate? existing) => Candidate.Validate(details);

    public Result<Candidate> Create(CandidateDetails details, RecordStatus status) => Candidate.Create(details);

    public Result Update(Candidate entity, CandidateDetails details) => entity.Update(details);
}

/// <summary>The email is unique, compared without regard to case (it is stored lower-case).</summary>
internal sealed class CandidateSaveRules(IRepository<Candidate> candidates) : ISaveRules<Candidate, CandidateDetails>
{
    public Task<Result> CheckCreateAsync(CandidateDetails details, CancellationToken cancellationToken) =>
        EnsureUniqueEmailAsync(details, excludedId: 0, cancellationToken);

    public Task<Result> CheckUpdateAsync(Candidate existing, CandidateDetails details, CancellationToken cancellationToken) =>
        EnsureUniqueEmailAsync(details, existing.Id, cancellationToken);

    private Task<Result> EnsureUniqueEmailAsync(CandidateDetails details, int excludedId, CancellationToken cancellationToken)
    {
        var email = details.Email.ToLowerInvariant();
        return HierarchyChecks.EnsureUniqueAsync(
            candidates, c => c.Email == email && c.Id != excludedId, "Email", "email", email, cancellationToken);
    }
}

internal sealed class CandidateQuerySpecification : IQuerySpecification<Candidate, CandidateFilter>
{
    public IReadOnlyDictionary<string, Expression<Func<Candidate, object>>> SortKeys { get; } =
        new Dictionary<string, Expression<Func<Candidate, object>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = c => c.Id,
            ["firstName"] = c => c.FirstName,
            ["lastName"] = c => c.LastName,
            ["email"] = c => c.Email,
            ["source"] = c => c.Source,
            ["currentEmployer"] = c => c.CurrentEmployer ?? string.Empty,
            ["experienceYears"] = c => c.ExperienceYears,
        };

    public Expression<Func<Candidate, bool>> Search(string term) =>
        Predicates.ContainsAny<Candidate>(term, c => c.FirstName, c => c.LastName, c => c.Email, c => c.CurrentEmployer);

    public Expression<Func<Candidate, bool>> Filter(CandidateFilter filter)
    {
        if (!FilterPredicates.IsSet(filter.Source))
        {
            return Predicates.Always<Candidate>();
        }

        return CandidateSourceText.TryParse(filter.Source, out var source)
            ? c => c.Source == source
            : Predicates.Never<Candidate>();
    }
}

/// <summary>Limits of a candidate that follow from the database columns.</summary>
public static class CandidateLimits
{
    public const int NameMaxLength = 40;
    public const int EmailMaxLength = 100;
    public const int PhoneMaxLength = 25;
    public const int EmployerMaxLength = 80;
    public const int ResumeNameMaxLength = 260;
    public const int NotesMaxLength = 300;
}

/// <summary>Shape rules shared by the create and update requests. The domain owns the experience range.</summary>
internal abstract class CandidateFieldsValidator<TRequest> : RequestValidator<TRequest>
    where TRequest : ICandidateFields
{
    protected CandidateFieldsValidator()
    {
        RuleForText(x => x.FirstName, "First name").RequiredText(CandidateLimits.NameMaxLength);
        RuleForText(x => x.LastName, "Last name").RequiredText(CandidateLimits.NameMaxLength);
        RuleForText(x => x.Email, "Email").RequiredText(CandidateLimits.EmailMaxLength).ValidEmail();
        RuleForText(x => x.Phone, "Phone").RequiredText(CandidateLimits.PhoneMaxLength);
        RuleForText(x => x.CurrentEmployer, "Current employer").OptionalText(CandidateLimits.EmployerMaxLength);
        RuleForText(x => x.ResumeName, "Resume name").OptionalText(CandidateLimits.ResumeNameMaxLength);
        RuleForText(x => x.Notes, "Notes").OptionalText(CandidateLimits.NotesMaxLength);
        RuleFor(x => x.ExperienceYears)
            .PrecisionScale(4, 1, ignoreTrailingZeros: true).WithMessage("Use at most 1 decimal place.");
        RuleForText(x => x.Source, "Source")
            .NotEmpty().WithMessage("Source is required.")
            .Must(source => CandidateSourceText.TryParse(source?.Trim(), out _))
            .WithMessage($"Source must be {CandidateSourceText.Map.OneOf}.");
    }
}

internal sealed class CandidateCreateRequestValidator : CandidateFieldsValidator<CandidateCreateRequest>;

internal sealed class CandidateUpdateRequestValidator : CandidateFieldsValidator<CandidateUpdateRequest>
{
    public CandidateUpdateRequestValidator() => RuleForText(x => x.RowVersion).RequiredRowVersion();
}
