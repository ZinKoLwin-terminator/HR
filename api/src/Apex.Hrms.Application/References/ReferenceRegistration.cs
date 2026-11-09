using System.Linq.Expressions;
using Apex.Hrms.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Apex.Hrms.Application.References;

internal static class ReferenceRegistration
{
    /// <summary>
    /// Declares that <typeparamref name="TChild"/> rows point at <typeparamref name="TOwner"/>, so they block the
    /// operations named by <paramref name="use"/>.
    /// </summary>
    /// <param name="pointsAt">Builds the predicate "this child points at that owner id".</param>
    public static IServiceCollection AddReference<TOwner, TChild>(
        this IServiceCollection services,
        string label,
        ReferenceUse use,
        Func<int, Expression<Func<TChild, bool>>> pointsAt)
        where TChild : class =>
        services.AddScoped<IReferenceCheck<TOwner>>(provider => new ChildReferenceCheck<TOwner, TChild>(
            label,
            use,
            pointsAt,
            provider.GetRequiredService<IRecordCounter<TChild>>()));
}
