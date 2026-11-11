using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Domain.Recruitment;

/// <summary>The fields of a candidate. The email is stored in lower case.</summary>
public sealed record CandidateDetails(
    string FirstName,
    string LastName,
    string Email,
    string Phone,
    CandidateSource Source,
    string? CurrentEmployer,
    decimal ExperienceYears,
    string? ResumeName,
    string? Notes)
{
    /// <summary>Trims text, lower-cases the email and turns blank optional text into null. Idempotent.</summary>
    public CandidateDetails Normalized() => this with
    {
        FirstName = TextNormalization.Clean(FirstName),
        LastName = TextNormalization.Clean(LastName),
        Email = TextNormalization.Clean(Email).ToLowerInvariant(),
        Phone = TextNormalization.Clean(Phone),
        CurrentEmployer = TextNormalization.CleanOptional(CurrentEmployer),
        ResumeName = TextNormalization.CleanOptional(ResumeName),
        Notes = TextNormalization.CleanOptional(Notes),
    };
}

/// <summary>
/// A job applicant (rec.Candidate): personal data of a person who is not an employee. The email is unique (checked by
/// the Application layer) and kept lower-case; experience is 0 to 50 years. Only the file name of the resume is kept.
/// </summary>
public sealed class Candidate : AuditedEntity
{
    public const decimal MinExperienceYears = 0m;
    public const decimal MaxExperienceYears = 50m;

    private Candidate()
    {
    }

    public string FirstName { get; private set; } = string.Empty;

    public string LastName { get; private set; } = string.Empty;

    public string Email { get; private set; } = string.Empty;

    public string Phone { get; private set; } = string.Empty;

    public CandidateSource Source { get; private set; }

    public string? CurrentEmployer { get; private set; }

    public decimal ExperienceYears { get; private set; }

    public string? ResumeName { get; private set; }

    public string? Notes { get; private set; }

    public string FullName => $"{FirstName} {LastName}";

    public static Result Validate(CandidateDetails details)
    {
        var normalized = details.Normalized();
        return new Violations()
            .Require("firstName", "First name", normalized.FirstName)
            .Require("lastName", "Last name", normalized.LastName)
            .Require("email", "Email", normalized.Email)
            .Require("phone", "Phone", normalized.Phone)
            .AddIf(
                normalized.ExperienceYears is < MinExperienceYears or > MaxExperienceYears,
                "experienceYears",
                "Enter 0 to 50.",
                "Experience must be between 0 and 50 years.")
            .ToResult();
    }

    public static Result<Candidate> Create(CandidateDetails details)
    {
        var candidate = new Candidate();
        var result = candidate.Update(details);
        return result.IsFailure ? Result.Failure<Candidate>(result.Error) : Result.Success(candidate);
    }

    /// <summary>Applies the details, or changes nothing when any rule is violated.</summary>
    public Result Update(CandidateDetails details)
    {
        var valid = Validate(details);
        if (valid.IsFailure)
        {
            return valid;
        }

        var normalized = details.Normalized();
        FirstName = normalized.FirstName;
        LastName = normalized.LastName;
        Email = normalized.Email;
        Phone = normalized.Phone;
        Source = normalized.Source;
        CurrentEmployer = normalized.CurrentEmployer;
        ExperienceYears = normalized.ExperienceYears;
        ResumeName = normalized.ResumeName;
        Notes = normalized.Notes;
        return Result.Success();
    }
}
