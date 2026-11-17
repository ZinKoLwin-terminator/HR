using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Api.Http;

/// <summary>Turns use-case results into HTTP responses; this is the only place that maps outcomes to status codes.</summary>
internal static class ResultExtensions
{
    public static IResult ToOk<T>(this Result<T> result) =>
        result.IsSuccess ? Results.Ok(result.Value) : result.ToProblem();

    public static IResult ToCreated<T>(this Result<T> result, Func<T, string> location) =>
        result.IsSuccess ? Results.Created(location(result.Value), result.Value) : result.ToProblem();

    /// <summary>201 with a Location header when the call created the record, 200 when it only changed an existing one.</summary>
    public static IResult ToCreatedOrOk<T, TBody>(this Result<T> result, Func<T, bool> created, Func<T, TBody> body, Func<TBody, string> location)
    {
        if (result.IsFailure)
        {
            return result.ToProblem();
        }

        var content = body(result.Value);
        return created(result.Value) ? Results.Created(location(content), content) : Results.Ok(content);
    }

    public static IResult ToFile(this Result<Apex.Hrms.Application.Excel.ExcelFileResult> result) =>
        result.IsSuccess ? Results.File(result.Value.Content, result.Value.ContentType, result.Value.FileName) : result.ToProblem();

    /// <summary>
    /// An evidence file: always a download (<c>Content-Disposition: attachment</c> with an RFC 6266 / 5987 encoded name, never inline),
    /// served with the content type detected when it was uploaded. The stream is disposed by the framework.
    /// </summary>
    public static IResult ToAttachmentFile(this Result<Apex.Hrms.Application.Performance.Scorecards.EvidenceDownload> result) =>
        result.IsSuccess
            ? Results.File(result.Value.Content, result.Value.ContentType, fileDownloadName: result.Value.FileName)
            : result.ToProblem();

    public static IResult ToNoContent(this Result result) =>
        result.IsSuccess ? Results.NoContent() : result.ToProblem();

    public static IResult ToAccepted(this Result result) =>
        result.IsSuccess ? Results.Accepted() : result.ToProblem();

    private static IResult ToProblem(this Result result) => Results.Problem(ApiProblems.From(result.Error));
}
