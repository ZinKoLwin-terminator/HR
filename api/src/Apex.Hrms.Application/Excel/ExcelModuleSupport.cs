using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Excel;

namespace Apex.Hrms.Application.Excel;

/// <summary>Row dictionaries of an export, keyed by column key.</summary>
internal static class ExcelRows
{
    public static IReadOnlyList<IReadOnlyDictionary<string, object?>> Of(IEnumerable<Dictionary<string, object?>> rows) =>
        [.. rows.Select(r => (IReadOnlyDictionary<string, object?>)r)];
}

/// <summary>Small helpers shared by the Excel modules: column builders, filters, paging through the list handlers, issue mapping.</summary>
internal static class ExcelModuleSupport
{
    public const string Code = "^[A-Z0-9][A-Z0-9-]*$";

    private static readonly JsonSerializerOptions FilterJson = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString,
    };

    public static ExcelColumn Col(string key, string header, string description, object? example, Func<ExcelColumn, ExcelColumn>? configure = null)
    {
        var column = new ExcelColumn { Key = key, Header = header, Description = description, Example = example };
        return configure is null ? column : configure(column);
    }

    public static ExcelColumn Out(string key, string header, ExcelColumnType type = ExcelColumnType.Text, bool sensitive = false, int? width = null) =>
        new() { Key = key, Header = header, Type = type, Sensitive = sensitive, Width = width };

    public static ExcelColumn Plain(string key, string header, int width) => new() { Key = key, Header = header, Width = width };

    public static string? Opt(string? text) => string.IsNullOrWhiteSpace(text) ? null : text;

    public static DateOnly Date(string text) => DateOnly.ParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static DateOnly? DateOrNull(string text) => text.Length == 0 ? null : Date(text);

    public static decimal Number(string text) => decimal.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);

    public static string Short(string value) => value.Length > 60 ? value[..60] + "…" : value;

    /// <summary>The filters of an export, in the shape of the module's list filter. Only a Filtered export applies them.</summary>
    public static Result<TFilter> ParseFilter<TFilter>(ExportRequest request)
    {
        var node = new JsonObject();
        if (request.Scope == ExportScope.Filtered && request.Filters is not null)
        {
            foreach (var (key, value) in request.Filters)
            {
                if (value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined
                    || (value.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(value.GetString())))
                {
                    continue;
                }

                node[key] = JsonNode.Parse(value.GetRawText());
            }
        }

        try
        {
            return Result.Success(node.Deserialize<TFilter>(FilterJson)!);
        }
        catch (Exception exception) when (exception is JsonException or FormatException or InvalidOperationException)
        {
            return Result.Failure<TFilter>(AppError.Validation("A filter value is not valid.", "filters", "A filter value is not valid."));
        }
    }

    /// <summary>
    /// Reads the records of an export through the module's own list (or get) handler, so the caller's scope, the filters and
    /// the search are exactly those of the list screen. At most <paramref name="limit"/> + 1 records come back.
    /// </summary>
    public static async Task<Result<List<TDto>>> CollectAsync<TDto, TFilter>(
        IQueryHandler<TDto, TFilter> query,
        IGetByIdHandler<TDto> get,
        ExportRequest request,
        TFilter filter,
        int limit,
        CancellationToken cancellationToken)
    {
        var items = new List<TDto>();
        if (request.Scope == ExportScope.Selected)
        {
            foreach (var id in (request.SelectedIds ?? []).Distinct().Take(limit + 1))
            {
                var found = await get.HandleAsync(id, cancellationToken);
                if (found.IsSuccess)
                {
                    items.Add(found.Value);
                }
                else if (found.Error.Type != ErrorType.NotFound)
                {
                    return Result.Failure<List<TDto>>(found.Error);
                }
            }

            return Result.Success(items);
        }

        var search = request.Scope == ExportScope.Filtered && !string.IsNullOrWhiteSpace(request.Search) ? request.Search.Trim() : null;
        for (var page = 0; items.Count <= limit; page++)
        {
            var result = await query.HandleAsync(new PageRequest<TFilter>(page, Paging.MaxPageSize, null, search, filter), cancellationToken);
            if (result.IsFailure)
            {
                return Result.Failure<List<TDto>>(result.Error);
            }

            items.AddRange(result.Value.Items);
            if (result.Value.Items.Count < Paging.MaxPageSize || (page + 1) * Paging.MaxPageSize >= result.Value.Total)
            {
                break;
            }
        }

        return Result.Success(items.Count > limit + 1 ? items.Take(limit + 1).ToList() : items);
    }

    /// <summary>The same employee read as the employee export does: scope, filters, search, id list; at most limit + 1 rows.</summary>
    public static async Task<Result<List<Domain.Employees.Employee>>> CollectEmployeesAsync(
        IRepository<Domain.Employees.Employee> employees,
        System.Linq.Expressions.Expression<Func<Domain.Employees.Employee, bool>> scope,
        IQuerySpecification<Domain.Employees.Employee, Employees.EmployeeFilter> specification,
        ExportRequest request,
        Employees.EmployeeFilter filter,
        int limit,
        CancellationToken cancellationToken)
    {
        var predicate = scope;
        if (request.Scope == ExportScope.Filtered)
        {
            predicate = predicate.And(specification.Filter(filter));
            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                if (request.Search.Length > Paging.MaxSearchLength)
                {
                    var message = $"Search must be at most {Paging.MaxSearchLength} characters.";
                    return Result.Failure<List<Domain.Employees.Employee>>(AppError.Validation(message, "search", message));
                }

                predicate = predicate.And(specification.Search(request.Search.Trim()));
            }
        }

        if (request.Scope == ExportScope.Selected)
        {
            var ids = request.SelectedIds ?? [];
            predicate = predicate.And(e => ids.Contains(e.Id));
        }

        var found = new List<Domain.Employees.Employee>();
        const int pageSize = 500;
        for (var page = 0; found.Count <= limit; page++)
        {
            var result = await employees.QueryAsync(
                new EntityQuery<Domain.Employees.Employee>(predicate, new SortCriterion<Domain.Employees.Employee>(e => e.EmployeeCode, false), page, pageSize),
                cancellationToken);
            found.AddRange(result.Items);
            if (result.Items.Count < pageSize || (page + 1) * pageSize >= result.Total)
            {
                break;
            }
        }

        return Result.Success(found.Count > limit + 1 ? found.Take(limit + 1).ToList() : found);
    }

    /// <summary>
    /// Turns a failure of a handler into the issues of a row, named after the columns to correct. <paramref name="fieldToColumn"/> maps
    /// the API field names (<c>companyId</c>) to column keys (<c>companyCode</c>); a name that is itself a column key maps to it.
    /// </summary>
    public static IEnumerable<ImportIssueDto> ToIssues(
        AppError error,
        ParsedRow row,
        IReadOnlyList<ExcelColumn> columns,
        IReadOnlyDictionary<string, string> fieldToColumn,
        string fallbackKey)
    {
        var fallback = columns.First(c => c.Key == fallbackKey);
        if (error.FieldErrors is { Count: > 0 })
        {
            foreach (var (field, messages) in error.FieldErrors)
            {
                var key = fieldToColumn.TryGetValue(field, out var mapped) ? mapped : field;
                var column = columns.FirstOrDefault(c => string.Equals(c.Key, key, StringComparison.OrdinalIgnoreCase)) ?? fallback;
                foreach (var message in messages)
                {
                    yield return Issue(column, row, message);
                }
            }

            yield break;
        }

        yield return Issue(fallback, row, error.Message);
    }

    public static ImportIssueDto Issue(ExcelColumn column, ParsedRow row, string message, ImportSeverity severity = ImportSeverity.Error) =>
        new(column.Header, Short(ExcelCoercion.Shown(column, row.Text(column.Key))), message, null, severity);
}

/// <summary>Collects the issues of one row while it is checked against the other records.</summary>
internal sealed class RowProblems(IReadOnlyList<ExcelColumn> columns, ParsedRow row)
{
    public List<ImportIssueDto> Issues { get; } = [];

    public bool Any => Issues.Count > 0;

    public void Add(string key, string message) =>
        Issues.Add(ExcelModuleSupport.Issue(columns.First(c => c.Key == key), row, message));
}

/// <summary>Shared definition of an export-only module (no import columns, no importer).</summary>
internal static class ExportOnly
{
    public static ExcelModuleInfo Info(string code, string label, string description, IReadOnlyList<ExcelColumn> columns, string? dateLabel) =>
        new(code, label, label, description, [], columns, [], string.Empty, CanImport: false, CanExport: true, dateLabel);

    public static Task<IReadOnlyList<ExportSheet>> NoReferences() => Task.FromResult<IReadOnlyList<ExportSheet>>([]);

    public static IExcelImportAdapter Refuse() =>
        throw new NotSupportedException("This module has no import.");
}
