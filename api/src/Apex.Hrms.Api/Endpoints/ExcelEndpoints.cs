using System.Security.Claims;
using Apex.Hrms.Api.Authorization;
using Apex.Hrms.Api.Http;
using Apex.Hrms.Application;
using Apex.Hrms.Application.Excel;
using Apex.Hrms.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;

namespace Apex.Hrms.Api.Endpoints;

/// <summary>
/// Excel import and export. The route policies (<c>excel:View</c>, <c>excel:Import</c>, <c>excel:Export</c>) protect the
/// transfer itself; the permissions of the module whose rows are read or written (for employees: create, edit, edit
/// sensitive, edit bank for an import; view for an export, view sensitive and view bank for sensitive columns) are checked
/// on top, by name, so the module's own rules stay the single source of truth. Uploads are read into memory with a hard
/// size cap and kept only in temporary storage.
/// </summary>
internal static class ExcelEndpoints
{
    private static readonly PermissionModule Module = PermissionModule.Excel;

    public static IEndpointRouteBuilder MapExcelEndpoints(this IEndpointRouteBuilder app)
    {
        var view = Module.PolicyName(Permission.View);
        var import = Module.PolicyName(Permission.Import);
        var export = Module.PolicyName(Permission.Export);
        var group = app.MapGroup("/api/excel").WithTags("Excel");

        group.MapGet("/modules", (IExcelCatalogService catalog) => catalog.GetModules().ToOk())
            .RequireAuthorization(view);

        group.MapGet(
            "/templates/{module}",
            async (string module, IExcelCatalogService catalog, IExcelImportService imports, IAuthorizationService auth, ClaimsPrincipal user, CancellationToken cancellationToken) =>
                await Guard(auth, user, catalog.Find(module), m => m.ImportPolicies) is { } denied
                    ? denied
                    : (await imports.GetTemplateAsync(module, cancellationToken)).ToFile())
            .RequireAuthorization(import);

        group.MapPost(
            "/imports/{module}/inspect",
            async (string module, HttpRequest request, IExcelCatalogService catalog, IExcelImportService imports, IAuthorizationService auth, ClaimsPrincipal user, ExcelLimits limits, CancellationToken cancellationToken) =>
            {
                if (await Guard(auth, user, catalog.Find(module), m => m.ImportPolicies) is { } denied)
                {
                    return denied;
                }

                var upload = await ReadUploadAsync(request, limits, cancellationToken);
                return upload.IsFailure
                    ? Results.Problem(ApiProblems.From(upload.Error))
                    : (await imports.InspectAsync(module, upload.Value.FileName, upload.Value.Content, cancellationToken)).ToOk();
            })
            .RequireAuthorization(import);

        group.MapPost(
            "/imports/{module}/validate",
            async (string module, ImportValidateRequest body, IExcelCatalogService catalog, IExcelImportService imports, IAuthorizationService auth, ClaimsPrincipal user, CancellationToken cancellationToken) =>
                await Guard(auth, user, catalog.Find(module), m => m.ImportPolicies) is { } denied
                    ? denied
                    : (await imports.ValidateAsync(module, body, cancellationToken)).ToOk())
            .RequireAuthorization(import);

        group.MapPost(
            "/imports/{batchId}/execute",
            async (string batchId, ImportExecuteRequest body, IExcelCatalogService catalog, IExcelImportService imports, IAuthorizationService auth, ClaimsPrincipal user, CancellationToken cancellationToken) =>
            {
                var module = await imports.ModuleOfBatchAsync(batchId, cancellationToken);
                if (module.IsFailure)
                {
                    return Results.Problem(ApiProblems.From(module.Error));
                }

                return await Guard(auth, user, catalog.Find(module.Value), m => m.ImportPolicies) is { } denied
                    ? denied
                    : (await imports.ExecuteAsync(batchId, body.Mode, cancellationToken)).ToOk();
            })
            .RequireAuthorization(import);

        group.MapGet(
            "/imports/history",
            async (int? pageIndex, int? pageSize, IExcelImportService imports, CancellationToken cancellationToken) =>
                (await imports.GetHistoryAsync(pageIndex ?? 0, pageSize ?? 10, cancellationToken)).ToOk())
            .RequireAuthorization(view);

        group.MapGet(
            "/imports/{batchId}",
            async (string batchId, IExcelImportService imports, CancellationToken cancellationToken) =>
                (await imports.GetBatchAsync(batchId, cancellationToken)).ToOk())
            .RequireAuthorization(view);

        group.MapGet(
            "/imports/{batchId}/error-report",
            async (string batchId, IExcelImportService imports, CancellationToken cancellationToken) =>
                (await imports.GetErrorReportAsync(batchId, cancellationToken)).ToFile())
            .RequireAuthorization(view);

        group.MapPost(
            "/exports/{module}",
            async (string module, ExportRequest body, IExcelCatalogService catalog, IExcelExportService exports, IAuthorizationService auth, ClaimsPrincipal user, CancellationToken cancellationToken) =>
            {
                var found = catalog.Find(module);
                var denied = await Guard(auth, user, found, m => m.ExportPolicies)
                    ?? (body.IncludeSensitive ? await Guard(auth, user, found, m => m.SensitiveExportPolicies) : null);
                return denied ?? (await exports.ExportAsync(module, body, cancellationToken)).ToFile();
            })
            .RequireAuthorization(export);

        return app;
    }

    /// <summary>Null when every policy of the module is satisfied; otherwise the response to send (404 for an unknown module, 403 when a policy fails).</summary>
    private static async Task<IResult?> Guard(
        IAuthorizationService auth,
        ClaimsPrincipal user,
        IExcelModule? module,
        Func<IExcelModule, IReadOnlyList<string>> policies)
    {
        if (module is null)
        {
            return Results.Problem(ApiProblems.From(AppError.NotFound("This module is not available for Excel.")));
        }

        foreach (var policy in policies(module))
        {
            if (!(await auth.AuthorizeAsync(user, policy)).Succeeded)
            {
                return Results.Problem(ApiProblems.From(AppError.Forbidden("You do not have permission to use Excel with this module.")));
            }
        }

        return null;
    }

    private sealed record Upload(string FileName, byte[] Content);

    private static async Task<Result<Upload>> ReadUploadAsync(HttpRequest request, ExcelLimits limits, CancellationToken cancellationToken)
    {
        if (!request.HasFormContentType)
        {
            return Result.Failure<Upload>(AppError.Validation("Send the workbook as multipart/form-data.", "file", "Send the workbook as multipart/form-data."));
        }

        // Stop reading a request that is far bigger than any accepted workbook (the rest of the limit check is the service's).
        var feature = request.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (feature is { IsReadOnly: false })
        {
            feature.MaxRequestBodySize = limits.MaxFileBytes + (1024 * 1024);
        }

        var form = await request.ReadFormAsync(cancellationToken);
        if (form.Files.Count != 1)
        {
            return Result.Failure<Upload>(AppError.Validation("Upload exactly one file.", "file", "Upload exactly one file."));
        }

        var file = form.Files[0];
        await using var stream = file.OpenReadStream();
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length > limits.MaxFileBytes)
            {
                break;
            }
        }

        return Result.Success(new Upload(Path.GetFileName(file.FileName), buffer.ToArray()));
    }
}
