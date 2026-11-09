namespace Apex.Hrms.Api.Http;

/// <summary>
/// The JSON body of DELETE and of every command: <c>{ "rowVersion": "..." }</c>. The row version is required; a body
/// without it is rejected with a Validation error on <c>rowVersion</c>.
/// </summary>
internal sealed record VersionedBody(string? RowVersion);
