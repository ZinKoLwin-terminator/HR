using Apex.Hrms.Application.Activity;
using Apex.Hrms.Domain.Employees;

namespace Apex.Hrms.Application.Dashboard;

// Mirror dashboard.models.ts field for field. Dates are yyyy-MM-dd, money is decimal.

public sealed record HeadcountSummaryDto(int Total, int Active, int Probation, int NewThisMonth, int OnLeaveToday, int ExitsThisMonth);

/// <param name="Date">The day the figures are for: today, or the latest working day when today is a day off.</param>
/// <param name="AttendanceRate">Percent of the scheduled employees who checked in (present + late + half day), one decimal.</param>
public sealed record TodayAttendanceSummaryDto(
    DateOnly Date,
    int Scheduled,
    int Present,
    int Late,
    int Absent,
    int OnLeave,
    int HalfDay,
    decimal AttendanceRate);

/// <param name="NetChangePercent">Percent change of the net payroll against the previous processed period, one decimal.</param>
public sealed record PayrollSummaryDto(
    string PeriodName,
    string Status,
    DateOnly PayDate,
    int EmployeeCount,
    decimal TotalGross,
    decimal TotalDeductions,
    decimal TotalNet,
    decimal NetChangePercent);

public sealed record CountByLabelDto(string Label, int Count);

public sealed record AttendanceTrendPointDto(DateOnly Date, int Present, int Late, int Absent, int OnLeave);

/// <param name="Month">yyyy-MM.</param>
public sealed record HeadcountGrowthPointDto(string Month, int Headcount, int Joiners, int Exits);

/// <param name="Type">Holiday, Birthday (only for callers with employees:ViewSensitive), Work Anniversary, Leave or Probation.</param>
public sealed record UpcomingEventDto(DateOnly Date, string Title, string Subtitle, string Type);

public sealed record PendingLeaveItemDto(
    int Id,
    string EmployeeName,
    string LeaveTypeName,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal Days,
    DateOnly AppliedOn);

/// <param name="Funnel">Applications per pipeline stage: Applied, Screening, Interview, Selected.</param>
public sealed record RecruitmentSummaryDto(
    int OpenJobs,
    int OpenPositions,
    int ActiveCandidates,
    int UpcomingInterviews,
    IReadOnlyList<CountByLabelDto> Funnel);

public sealed record DashboardSummaryDto(
    DateTimeOffset GeneratedAt,
    HeadcountSummaryDto Headcount,
    TodayAttendanceSummaryDto AttendanceToday,
    int PendingLeaveApprovals,
    IReadOnlyList<PendingLeaveItemDto> PendingLeaves,
    PayrollSummaryDto? Payroll,
    RecruitmentSummaryDto? Recruitment,
    IReadOnlyList<CountByLabelDto> DepartmentDistribution,
    IReadOnlyList<AttendanceTrendPointDto> AttendanceTrend,
    IReadOnlyList<HeadcountGrowthPointDto> HeadcountGrowth,
    IReadOnlyList<ActivityEntryDto> RecentActivities,
    IReadOnlyList<UpcomingEventDto> UpcomingEvents);

// ---- What the source hands to the calculations: slim rows, never whole entities. ----

/// <summary>The few employee columns the dashboard needs. The birth date only decides the date of a birthday event.</summary>
public sealed record EmployeeFigureRow(
    int Id,
    int DepartmentId,
    EmploymentStatus Status,
    DateOnly JoiningDate,
    DateOnly? ExitDate,
    DateOnly DateOfBirth,
    DateOnly? ProbationEndDate,
    string FirstName,
    string LastName)
{
    /// <summary>Resigned and Terminated employees have left; everyone else is current.</summary>
    public bool IsCurrent => !Status.HasLeft();

    public string FullName => $"{FirstName} {LastName}".Trim();
}

/// <summary>Counts read from the recruitment tables (rec.JobOpening, rec.Application, rec.Interview).</summary>
/// <param name="StageCounts">Applications by stage name (Applied, Screening, Interview, Selected, Rejected).</param>
public sealed record RecruitmentRows(
    int OpenJobs,
    int OpenPositions,
    IReadOnlyDictionary<string, int> StageCounts,
    int UpcomingInterviews);

/// <summary>A processed payroll run (pay.PayrollRun) as the dashboard reads it. The totals are demonstration figures.</summary>
public sealed record PayrollRunRow(
    string PeriodName,
    string Status,
    DateOnly PayDate,
    int EmployeeCount,
    decimal TotalGross,
    decimal TotalDeductions,
    decimal TotalNet);
