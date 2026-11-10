namespace Apex.Hrms.Application.Abstractions;

/// <summary>The days of the week that are not working days (configuration key <c>Business:WeekendDays</c>, default Saturday and Sunday).</summary>
public interface IBusinessWeek
{
    IReadOnlySet<DayOfWeek> WeekendDays { get; }
}
