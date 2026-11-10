using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Domain.Attendance;

/// <summary>
/// A public or company holiday (att.Holiday). Read-only here: the leave module only needs the dates to leave out of
/// working-day counts and to show on the leave calendar. The attendance module will own writing it.
/// </summary>
public sealed class Holiday : AuditedEntity
{
    private Holiday()
    {
    }

    public DateOnly Date { get; private set; }

    public string Name { get; private set; } = string.Empty;

    /// <summary>"Public" or "Company".</summary>
    public string Type { get; private set; } = string.Empty;
}
