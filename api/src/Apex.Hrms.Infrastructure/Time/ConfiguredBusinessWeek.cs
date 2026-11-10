using Apex.Hrms.Application.Abstractions;

namespace Apex.Hrms.Infrastructure.Time;

/// <summary>
/// The non-working days of the week from configuration: key <c>Business:WeekendDays</c>, a list of day names such as
/// <c>["Saturday", "Sunday"]</c>. A missing or empty list means Saturday and Sunday.
/// </summary>
internal sealed class ConfiguredBusinessWeek(IReadOnlySet<DayOfWeek> weekendDays) : IBusinessWeek
{
    public const string ConfigurationKey = "Business:WeekendDays";

    public static readonly IReadOnlySet<DayOfWeek> DefaultWeekend = new HashSet<DayOfWeek> { DayOfWeek.Saturday, DayOfWeek.Sunday };

    public IReadOnlySet<DayOfWeek> WeekendDays { get; } = weekendDays;

    /// <exception cref="InvalidOperationException">A configured name is not a day of the week (fail fast at startup).</exception>
    public static ConfiguredBusinessWeek From(string[]? configured)
    {
        if (configured is null or { Length: 0 })
        {
            return new ConfiguredBusinessWeek(DefaultWeekend);
        }

        var days = new HashSet<DayOfWeek>();
        foreach (var name in configured)
        {
            var match = Enum.GetNames<DayOfWeek>().FirstOrDefault(
                candidate => string.Equals(candidate, name?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (match is null)
            {
                throw new InvalidOperationException(
                    $"'{name}' in configuration key '{ConfigurationKey}' is not a day of the week (use names such as Saturday).");
            }

            days.Add(Enum.Parse<DayOfWeek>(match));
        }

        return new ConfiguredBusinessWeek(days);
    }
}
