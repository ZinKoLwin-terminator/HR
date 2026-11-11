using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Domain.Recruitment;

/// <summary>The life of a job opening. Filled is reached only automatically, when enough candidates are selected.</summary>
public enum JobOpeningStatus
{
    Draft,
    Open,
    OnHold,
    Closed,
    Filled,
}

/// <summary>Recruitment says "Internship" where employees say "Intern".</summary>
public enum RecruitmentEmploymentType
{
    FullTime,
    PartTime,
    Contract,
    Internship,
}

public enum CandidateSource
{
    CareerSite,
    Referral,
    JobBoard,
    LinkedIn,
    Agency,
}

public enum ApplicationStage
{
    Applied,
    Screening,
    Interview,
    Selected,
    Rejected,
}

public enum InterviewRound
{
    PhoneScreen,
    Technical,
    Hr,
    Final,
}

public enum InterviewMode
{
    Onsite,
    Video,
    Phone,
}

public enum InterviewStatus
{
    Scheduled,
    Completed,
    Cancelled,
}

public enum InterviewRecommendation
{
    StrongHire,
    Hire,
    NoHire,
}

public static class JobOpeningStatusText
{
    public static readonly TextMap<JobOpeningStatus> Map = new(new Dictionary<JobOpeningStatus, string>
    {
        [JobOpeningStatus.Draft] = "Draft",
        [JobOpeningStatus.Open] = "Open",
        [JobOpeningStatus.OnHold] = "On Hold",
        [JobOpeningStatus.Closed] = "Closed",
        [JobOpeningStatus.Filled] = "Filled",
    });

    public static string ToText(JobOpeningStatus value) => Map.ToText(value);

    public static bool TryParse(string? text, out JobOpeningStatus value) => Map.TryParse(text, out value);
}

public static class RecruitmentEmploymentTypeText
{
    public static readonly TextMap<RecruitmentEmploymentType> Map = new(new Dictionary<RecruitmentEmploymentType, string>
    {
        [RecruitmentEmploymentType.FullTime] = "Full-time",
        [RecruitmentEmploymentType.PartTime] = "Part-time",
        [RecruitmentEmploymentType.Contract] = "Contract",
        [RecruitmentEmploymentType.Internship] = "Internship",
    });

    public static string ToText(RecruitmentEmploymentType value) => Map.ToText(value);

    public static RecruitmentEmploymentType Parse(string? text) => Map.Parse(text);

    public static bool TryParse(string? text, out RecruitmentEmploymentType value) => Map.TryParse(text, out value);
}

public static class CandidateSourceText
{
    public static readonly TextMap<CandidateSource> Map = new(new Dictionary<CandidateSource, string>
    {
        [CandidateSource.CareerSite] = "Career site",
        [CandidateSource.Referral] = "Referral",
        [CandidateSource.JobBoard] = "Job board",
        [CandidateSource.LinkedIn] = "LinkedIn",
        [CandidateSource.Agency] = "Agency",
    });

    public static string ToText(CandidateSource value) => Map.ToText(value);

    public static CandidateSource Parse(string? text) => Map.Parse(text);

    public static bool TryParse(string? text, out CandidateSource value) => Map.TryParse(text, out value);
}

public static class ApplicationStageText
{
    public static readonly TextMap<ApplicationStage> Map = new(new Dictionary<ApplicationStage, string>
    {
        [ApplicationStage.Applied] = "Applied",
        [ApplicationStage.Screening] = "Screening",
        [ApplicationStage.Interview] = "Interview",
        [ApplicationStage.Selected] = "Selected",
        [ApplicationStage.Rejected] = "Rejected",
    });

    public static string ToText(ApplicationStage value) => Map.ToText(value);

    public static ApplicationStage Parse(string? text) => Map.Parse(text);

    public static bool TryParse(string? text, out ApplicationStage value) => Map.TryParse(text, out value);
}

public static class InterviewRoundText
{
    public static readonly TextMap<InterviewRound> Map = new(new Dictionary<InterviewRound, string>
    {
        [InterviewRound.PhoneScreen] = "Phone screen",
        [InterviewRound.Technical] = "Technical",
        [InterviewRound.Hr] = "HR",
        [InterviewRound.Final] = "Final",
    });

    public static string ToText(InterviewRound value) => Map.ToText(value);

    public static InterviewRound Parse(string? text) => Map.Parse(text);

    public static bool TryParse(string? text, out InterviewRound value) => Map.TryParse(text, out value);
}

public static class InterviewModeText
{
    public static readonly TextMap<InterviewMode> Map = new(new Dictionary<InterviewMode, string>
    {
        [InterviewMode.Onsite] = "Onsite",
        [InterviewMode.Video] = "Video",
        [InterviewMode.Phone] = "Phone",
    });

    public static string ToText(InterviewMode value) => Map.ToText(value);

    public static InterviewMode Parse(string? text) => Map.Parse(text);

    public static bool TryParse(string? text, out InterviewMode value) => Map.TryParse(text, out value);
}

public static class InterviewStatusText
{
    public static readonly TextMap<InterviewStatus> Map = new(new Dictionary<InterviewStatus, string>
    {
        [InterviewStatus.Scheduled] = "Scheduled",
        [InterviewStatus.Completed] = "Completed",
        [InterviewStatus.Cancelled] = "Cancelled",
    });

    public static string ToText(InterviewStatus value) => Map.ToText(value);

    public static InterviewStatus Parse(string? text) => Map.Parse(text);

    public static bool TryParse(string? text, out InterviewStatus value) => Map.TryParse(text, out value);
}

public static class InterviewRecommendationText
{
    public static readonly TextMap<InterviewRecommendation> Map = new(new Dictionary<InterviewRecommendation, string>
    {
        [InterviewRecommendation.StrongHire] = "Strong hire",
        [InterviewRecommendation.Hire] = "Hire",
        [InterviewRecommendation.NoHire] = "No hire",
    });

    public static string ToText(InterviewRecommendation value) => Map.ToText(value);

    public static InterviewRecommendation Parse(string? text) => Map.Parse(text);

    public static bool TryParse(string? text, out InterviewRecommendation value) => Map.TryParse(text, out value);

    /// <summary>Hire and Strong hire count towards selecting a candidate.</summary>
    public static bool IsPositive(InterviewRecommendation? value) =>
        value is InterviewRecommendation.Hire or InterviewRecommendation.StrongHire;
}
