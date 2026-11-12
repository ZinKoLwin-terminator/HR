using System.Linq.Expressions;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Organization;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Employees;
using FluentValidation;

namespace Apex.Hrms.Application.Employees.Documents;

/// <summary>
/// Mirrors EmployeeDocument in employee-extras.models.ts. Metadata only: no file contents are stored or accepted, and a
/// document is personal data (reads need employees:ViewSensitive, writes employees:EditSensitive). <c>type</c> is one of
/// National ID, Passport, Resume, Offer Letter, Employment Contract, Certificate, Work Permit, Medical Report, Other.
/// </summary>
public sealed record EmployeeDocumentDto(
    int Id,
    int EmployeeId,
    string Type,
    string Title,
    string FileName,
    DateOnly UploadedOn,
    DateOnly? ExpiryDate,
    byte[] RowVersion) : IIdentified;

internal interface IEmployeeDocumentFields
{
    int EmployeeId { get; }

    string Type { get; }

    string Title { get; }

    string FileName { get; }

    DateOnly? UploadedOn { get; }

    DateOnly? ExpiryDate { get; }
}

/// <summary>The details plus the employee: the employee is not an editable field of a document.</summary>
public sealed record EmployeeDocumentSaveDetails(int EmployeeId, DocumentDetails Document);

/// <summary>POST body. <c>uploadedOn</c> defaults to today when omitted and cannot be in the future.</summary>
public sealed record EmployeeDocumentCreateRequest(
    int EmployeeId,
    string Type,
    string Title,
    string FileName,
    DateOnly? UploadedOn,
    DateOnly? ExpiryDate) : IEmployeeDocumentFields, ICreateRequest<EmployeeDocumentSaveDetails>
{
    EmployeeDocumentSaveDetails IDetailsRequest<EmployeeDocumentSaveDetails>.ToDetails() => EmployeeDocumentDetailsFactory.From(this);

    // Documents have no status; the shared create flow asks for one and the factory ignores it.
    RecordStatus ICreateRequest<EmployeeDocumentSaveDetails>.ParseStatus() => RecordStatus.Active;
}

/// <summary>PUT body. The employee cannot change (send the same <c>employeeId</c>; another one is a Validation error). Requires the row version.</summary>
public sealed record EmployeeDocumentUpdateRequest(
    int EmployeeId,
    string Type,
    string Title,
    string FileName,
    DateOnly? UploadedOn,
    DateOnly? ExpiryDate,
    string? RowVersion) : IEmployeeDocumentFields, IDetailsRequest<EmployeeDocumentSaveDetails>, IVersionedRequest
{
    EmployeeDocumentSaveDetails IDetailsRequest<EmployeeDocumentSaveDetails>.ToDetails() => EmployeeDocumentDetailsFactory.From(this);
}

internal static class EmployeeDocumentDetailsFactory
{
    public static EmployeeDocumentSaveDetails From(IEmployeeDocumentFields fields) => new(
        fields.EmployeeId,
        new DocumentDetails(
            DocumentTypeText.Parse(fields.Type.Trim()),
            fields.Title,
            fields.FileName,
            fields.UploadedOn,
            fields.ExpiryDate).Normalized());
}

/// <summary>Filters of the list. A document list is always paged: there is no "get all".</summary>
public sealed record EmployeeDocumentFilter(int? EmployeeId = null, string? Type = null);

internal sealed class EmployeeDocumentMapping(IClock clock) :
    IEntityDescriptor<EmployeeDocument>,
    IDtoMapper<EmployeeDocument, EmployeeDocumentDto>,
    IEntityFactory<EmployeeDocument, EmployeeDocumentSaveDetails>
{
    public string Label => "Document";

    public string ActivityCategory => Domain.Activity.ActivityCategories.Employee;

    /// <summary>The title only, as in the mock: the file name of a personal document is not written to the activity feed.</summary>
    public string Describe(EmployeeDocument entity) => entity.Title;

    public EmployeeDocumentDto ToDto(EmployeeDocument entity) => new(
        entity.Id,
        entity.EmployeeId,
        DocumentTypeText.ToText(entity.Type),
        entity.Title,
        entity.FileName,
        entity.UploadedOn,
        entity.ExpiryDate,
        entity.RowVersion);

    public Result Validate(EmployeeDocumentSaveDetails details, EmployeeDocument? existing) =>
        EmployeeDocument.Validate(details.Document, clock.Today);

    public Result<EmployeeDocument> Create(EmployeeDocumentSaveDetails details, RecordStatus status) =>
        EmployeeDocument.Create(details.EmployeeId, details.Document, clock.Today);

    public Result Update(EmployeeDocument entity, EmployeeDocumentSaveDetails details)
    {
        var same = entity.EnsureSameEmployee(details.EmployeeId);
        return same.IsFailure ? same : entity.Update(details.Document, clock.Today);
    }
}

/// <summary>The employee must exist (as the mock checks) and, on update, cannot change.</summary>
internal sealed class EmployeeDocumentSaveRules(
    IRepository<Employee> employees,
    IAccessScope<Employee> scope,
    ICurrentUser user) : ISaveRules<EmployeeDocument, EmployeeDocumentSaveDetails>
{
    public Task<Result> CheckCreateAsync(EmployeeDocumentSaveDetails details, CancellationToken cancellationToken) =>
        EnsureEmployeeAsync(details.EmployeeId, cancellationToken);

    public Task<Result> CheckUpdateAsync(EmployeeDocument existing, EmployeeDocumentSaveDetails details, CancellationToken cancellationToken) =>
        existing.EnsureSameEmployee(details.EmployeeId) is { IsFailure: true } different
            ? Task.FromResult(different)
            : EnsureEmployeeAsync(existing.EmployeeId, cancellationToken);

    private async Task<Result> EnsureEmployeeAsync(int employeeId, CancellationToken cancellationToken) =>
        await employees.FindAsync(employeeId, scope.PredicateFor(user), cancellationToken) is null
            ? Result.Failure(AppError.Validation("The selected employee does not exist.", "employeeId", "Choose an employee."))
            : Result.Success();
}

internal sealed class EmployeeDocumentQuerySpecification : IQuerySpecification<EmployeeDocument, EmployeeDocumentFilter>
{
    public IReadOnlyDictionary<string, Expression<Func<EmployeeDocument, object>>> SortKeys { get; } =
        new Dictionary<string, Expression<Func<EmployeeDocument, object>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = d => d.Id,
            ["employeeId"] = d => d.EmployeeId,
            ["type"] = d => d.Type,
            ["title"] = d => d.Title,
            ["uploadedOn"] = d => d.UploadedOn,
            ["expiryDate"] = d => d.ExpiryDate!,
        };

    /// <summary>The mock searches title, file name and type.</summary>
    public Expression<Func<EmployeeDocument, bool>> Search(string term)
    {
        var byText = Predicates.ContainsAny<EmployeeDocument>(term, d => d.Title, d => d.FileName);
        var matchingTypes = Enum.GetValues<DocumentType>()
            .Where(type => DocumentTypeText.ToText(type).Contains(term, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        return matchingTypes.Length == 0 ? byText : byText.Or(d => matchingTypes.Contains(d.Type));
    }

    public Expression<Func<EmployeeDocument, bool>> Filter(EmployeeDocumentFilter filter) => Predicates.Always<EmployeeDocument>()
        .And(filter.EmployeeId is { } employeeId ? d => d.EmployeeId == employeeId : Predicates.Always<EmployeeDocument>())
        .And(TypeFilter(filter.Type));

    private static Expression<Func<EmployeeDocument, bool>> TypeFilter(string? type)
    {
        if (!FilterPredicates.IsSet(type))
        {
            return Predicates.Always<EmployeeDocument>();
        }

        return DocumentTypeText.TryParse(type!.Trim(), out var parsed)
            ? d => d.Type == parsed
            : Predicates.Never<EmployeeDocument>();
    }
}

internal abstract class EmployeeDocumentFieldsValidator<TRequest> : RequestValidator<TRequest>
    where TRequest : IEmployeeDocumentFields
{
    protected EmployeeDocumentFieldsValidator()
    {
        RuleFor(x => x.EmployeeId).GreaterThan(0).WithMessage("Choose an employee.");
        RuleForText(x => x.Type, "Document type")
            .NotEmpty().WithMessage("Document type is required.")
            .Must(text => DocumentTypeText.TryParse(text?.Trim(), out _))
            .WithMessage($"Document type must be {DocumentTypeText.Map.OneOf}.");
        RuleForText(x => x.Title, "Title").RequiredText(EmployeeDocument.TitleMaxLength);
        RuleForText(x => x.FileName, "File name").RequiredText(EmployeeDocument.FileNameMaxLength);
    }
}

internal sealed class EmployeeDocumentCreateRequestValidator : EmployeeDocumentFieldsValidator<EmployeeDocumentCreateRequest>;

internal sealed class EmployeeDocumentUpdateRequestValidator : EmployeeDocumentFieldsValidator<EmployeeDocumentUpdateRequest>
{
    public EmployeeDocumentUpdateRequestValidator() => RuleForText(x => x.RowVersion).RequiredRowVersion();
}

