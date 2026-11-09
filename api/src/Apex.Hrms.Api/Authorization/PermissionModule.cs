namespace Apex.Hrms.Api.Authorization;

/// <summary>What a caller does with a resource; each verb has its own named policy per module.</summary>
internal enum Permission
{
    View,
    Create,
    Edit,
    Delete,
}

/// <summary>
/// A group of endpoints that share one permission key, e.g. "organization". Its policies are named
/// <c>organization:View</c>, <c>organization:Create</c>, <c>organization:Edit</c> and <c>organization:Delete</c>.
/// Add every new module to <see cref="All"/> so its policies are registered.
/// </summary>
internal sealed record PermissionModule(string Key)
{
    public static readonly PermissionModule Organization = new("organization");

    public static IReadOnlyList<PermissionModule> All { get; } = [Organization];

    public string PolicyName(Permission permission) => $"{Key}:{permission}";
}
