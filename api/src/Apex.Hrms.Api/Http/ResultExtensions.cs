using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Api.Http;

/// <summary>Turns use-case results into HTTP responses; this is the only place that maps outcomes to status codes.</summary>
internal static class ResultExtensions
{
    public static IResult ToOk<T>(this Result<T> result) =>
        result.IsSuccess ? Results.Ok(result.Value) : result.ToProblem();

    public static IResult ToCreated<T>(this Result<T> result, Func<T, string> location) =>
        result.IsSuccess ? Results.Created(location(result.Value), result.Value) : result.ToProblem();

    public static IResult ToFile(this Result<Apex.Hrms.Application.Excel.ExcelFileResult> result) =>
        result.IsSuccess ? Results.File(result.Value.Content, result.Value.ContentType, result.Value.FileName) : result.ToProblem();

    public static IResult ToNoContent(this Result result) =>
        result.IsSuccess ? Results.NoContent() : result.ToProblem();

    private static IResult ToProblem(this Result result) => Results.Problem(ApiProblems.From(result.Error));
}
