using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Application.References;

/// <summary>Runs the registered reference checks of one owner and phrases the outcome.</summary>
internal static class ReferenceUsage
{
    /// <summary>Counts every check that applies to <paramref name="use"/>; returns the non-zero ones as "3 department(s), 12 employee(s)", or null.</summary>
    public static async Task<string?> SummarizeAsync<TOwner>(
        IEnumerable<IReferenceCheck<TOwner>> checks,
        ReferenceUse use,
        int ownerId,
        CancellationToken cancellationToken)
    {
        var parts = new List<string>();
        foreach (var check in checks.Where(check => (check.Use & use) != 0))
        {
            var count = await check.CountAsync(ownerId, cancellationToken);
            if (count > 0)
            {
                parts.Add($"{count} {check.Label}");
            }
        }

        return parts.Count == 0 ? null : string.Join(", ", parts);
    }

    /// <summary>Fails with a Validation error on <paramref name="field"/> when records still point at the record being moved.</summary>
    public static async Task<Result> EnsureCanMoveAsync<TOwner>(
        IEnumerable<IReferenceCheck<TOwner>> checks,
        int ownerId,
        string label,
        string name,
        string newParent,
        string field,
        CancellationToken cancellationToken)
    {
        var usage = await SummarizeAsync(checks, ReferenceUse.Reparent, ownerId, cancellationToken);
        return usage is null
            ? Result.Success()
            : Result.Failure(AppError.Validation(
                $"{label} \"{name}\" cannot move to another {newParent} while it has {usage}.",
                field,
                $"Remove or move its {usage} first, or keep the current {newParent}."));
    }
}
