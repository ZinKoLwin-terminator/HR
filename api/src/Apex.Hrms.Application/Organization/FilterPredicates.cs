using System.Linq.Expressions;
using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Application.Organization;

/// <summary>
/// Translates query-string filter values. As in the frontend, blank and "all" mean "no filter"; a value that
/// is not a valid status matches nothing.
/// </summary>
internal static class FilterPredicates
{
    public static bool IsSet(string? value) =>
        !string.IsNullOrWhiteSpace(value) && !string.Equals(value, "all", StringComparison.OrdinalIgnoreCase);

    public static Expression<Func<T, bool>> Status<T>(string? text)
        where T : CodedRecord
    {
        if (!IsSet(text))
        {
            return Predicates.Always<T>();
        }

        return RecordStatusText.TryParse(text, out var status)
            ? row => row.Status == status
            : Predicates.Never<T>();
    }
}
