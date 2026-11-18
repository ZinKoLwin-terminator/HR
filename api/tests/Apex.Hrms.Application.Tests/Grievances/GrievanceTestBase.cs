using System.Text.Json;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Employees;
using Apex.Hrms.Application.Grievances;
using Apex.Hrms.Application.Grievances.Categories;
using Apex.Hrms.Application.Notifications;
using Apex.Hrms.Application.Tests.Fakes;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Grievances;
using Microsoft.Extensions.DependencyInjection;

namespace Apex.Hrms.Application.Tests.Grievances;

/// <summary>
/// The people of the grievance tests (all fictional) and the steps they take through the real handlers. Sam submits, Hana is HR
/// management, Ivan and Olga can investigate, Rita reviews, Sid is the employee a case can be about. The clock says 2026-11-09.
/// </summary>
public abstract class GrievanceTestBase : IDisposable
{
    internal const int AdminUser = 1;
    internal const int SamUser = 10;
    internal const int HanaUser = 20;
    internal const int IvanUser = 30;
    internal const int RitaUser = 40;
    internal const int OlgaUser = 50;
    internal const int SidUser = 60;

    internal static readonly string[] SelfGrants = ["grievance:View", "grievance:Create"];
    internal static readonly string[] ManagementGrants = ["grievance-management:View", "grievance-management:Create", "grievance-management:Edit", "grievance-management:Delete"];
    internal static readonly string[] InvestigationGrants = ["grievance-investigation:View", "grievance-investigation:Edit"];
    internal static readonly string[] ReviewGrants = ["grievance-review:View", "grievance-review:Approve"];

    private protected GrievanceTestBase(int retentionYears = GrievanceSettings.DefaultRetentionYears, long submitterDailyUploadBytes = GrievanceSettings.DefaultSubmitterDailyUploadBytes)
    {
        Host = new TestHost(services =>
        {
            services.AddSingleton<IGrievancePseudonyms, FakeGrievancePseudonyms>();
            services.AddSingleton<FakeGrievanceSubmitterDirectory>();
            services.AddSingleton<IGrievanceSubmitterDirectory>(provider => provider.GetRequiredService<FakeGrievanceSubmitterDirectory>());
            services.AddSingleton<FakeGrievanceAuditLog>();
            services.AddSingleton<IGrievanceAuditLog>(provider => provider.GetRequiredService<FakeGrievanceAuditLog>());
            services.AddSingleton<IGrievanceCaseNumberSequence, FakeGrievanceCaseNumbers>();
            services.AddSingleton(Files);
            services.AddSingleton<IGrievanceFileStorage>(Files);
            services.AddSingleton(Holders);
            services.AddSingleton<IPermissionHolders>(Holders);
            services.AddSingleton(Recipients);
            services.AddSingleton<INotificationRecipients>(Recipients);
            services.AddSingleton<FakeNotificationWriter>();
            services.AddSingleton<INotificationWriter>(provider => provider.GetRequiredService<FakeNotificationWriter>());
            services.AddSingleton<IRecordCounter<GrievanceCase>>(provider => (InMemoryRepository<GrievanceCase>)provider.GetRequiredService<IRepository<GrievanceCase>>());
            services.AddSingleton(new GrievanceSettings(retentionYears, submitterDailyUploadBytes));
        });
    }

    private protected TestHost Host { get; }

    private protected FakeGrievanceFileStorage Files { get; } = new();

    private protected FakePermissionHolders Holders { get; } = new();

    private protected FakeNotificationRecipients Recipients { get; } = new();

    private protected FakeGrievanceAuditLog Audit => Host.Get<FakeGrievanceAuditLog>();

    private protected FakeNotificationWriter Notifications => Host.Get<FakeNotificationWriter>();

    private protected FakeGrievanceSubmitterDirectory Submitters => Host.Get<FakeGrievanceSubmitterDirectory>();

    private protected IMyGrievanceService My => Host.Get<IMyGrievanceService>();

    private protected IGrievanceCaseReader Cases => Host.Get<IGrievanceCaseReader>();

    private protected IGrievanceCaseWork Work => Host.Get<IGrievanceCaseWork>();

    private protected World W { get; private set; } = null!;

    public void Dispose()
    {
        Host.Dispose();
        GC.SuppressFinalize(this);
    }

    private protected sealed record World(
        OrgSeed Org,
        EmployeeDto Sam,
        EmployeeDto Hana,
        EmployeeDto Ivan,
        EmployeeDto Rita,
        EmployeeDto Olga,
        EmployeeDto Sid,
        GrievanceCategoryDto Category,
        GrievanceCategoryDto NamedOnly);

    private protected async Task SeedAsync()
    {
        AsAdmin();
        var org = await Host.SeedOrgAsync();
        var sam = await Host.CreateStaffAsync(org, "EMP-0001", first: "Sam", last: "Submitterson");
        var hana = await Host.CreateStaffAsync(org, "EMP-0002", first: "Hana", last: "Manager");
        var ivan = await Host.CreateStaffAsync(org, "EMP-0003", first: "Ivan", last: "Investigator");
        var rita = await Host.CreateStaffAsync(org, "EMP-0004", first: "Rita", last: "Reviewer");
        var olga = await Host.CreateStaffAsync(org, "EMP-0005", first: "Olga", last: "Other");
        var sid = await Host.CreateStaffAsync(org, "EMP-0006", first: "Sid", last: "Subject");
        var category = ResultAssert.Succeeded(await Host.Get<ICreateHandler<GrievanceCategoryCreateRequest, GrievanceCategoryDto>>()
            .HandleAsync(new GrievanceCategoryCreateRequest("TST-HAR", "TST Harassment", null, true, 14, 1, "Active"), default));
        var namedOnly = ResultAssert.Succeeded(await Host.Get<ICreateHandler<GrievanceCategoryCreateRequest, GrievanceCategoryDto>>()
            .HandleAsync(new GrievanceCategoryCreateRequest("TST-PAY", "TST Compensation", null, false, 21, 2, "Active"), default));
        W = new World(org, sam, hana, ivan, rita, olga, sid, category, namedOnly);

        Holders.Grant("grievance-investigation:Edit", ivan.Id, olga.Id, hana.Id, sam.Id, sid.Id);
        Recipients.Accounts.AddRange(
        [
            new FakeAccount(AdminUser, null, Set([.. SelfGrants, .. ManagementGrants, .. InvestigationGrants, .. ReviewGrants])),
            new FakeAccount(SamUser, sam.Id, Set([.. SelfGrants, .. InvestigationGrants])),
            new FakeAccount(HanaUser, hana.Id, Set([.. SelfGrants, .. ManagementGrants, .. InvestigationGrants])),
            new FakeAccount(IvanUser, ivan.Id, Set([.. SelfGrants, .. InvestigationGrants])),
            new FakeAccount(RitaUser, rita.Id, Set([.. SelfGrants, .. ReviewGrants])),
            new FakeAccount(OlgaUser, olga.Id, Set([.. SelfGrants, .. InvestigationGrants])),
            new FakeAccount(SidUser, sid.Id, Set([.. SelfGrants, .. ManagementGrants])),
        ]);
    }

    private static HashSet<string> Set(string[] grants) => [.. grants];

    // ---- Callers ----

    private protected void As(EmployeeDto? employee, int userId, string name, params string[] grants)
    {
        Host.User.EmployeeId = employee?.Id;
        Host.User.UserId = userId;
        Host.User.DisplayName = name;
        Host.Permissions.Only(grants);
    }

    private protected void AsAdmin() => As(null, AdminUser, "Built-in Admin", [.. SelfGrants, .. ManagementGrants, .. InvestigationGrants, .. ReviewGrants, "employees:Edit", "organization:Edit"]);

    private protected void AsSam() => As(W.Sam, SamUser, "Sam Submitterson", SelfGrants);

    private protected void AsHana() => As(W.Hana, HanaUser, "Hana Manager", [.. SelfGrants, .. ManagementGrants]);

    private protected void AsIvan() => As(W.Ivan, IvanUser, "Ivan Investigator", [.. SelfGrants, .. InvestigationGrants]);

    private protected void AsOlga() => As(W.Olga, OlgaUser, "Olga Other", [.. SelfGrants, .. InvestigationGrants]);

    private protected void AsRita() => As(W.Rita, RitaUser, "Rita Reviewer", [.. SelfGrants, .. ReviewGrants]);

    /// <summary>Sid with management grants: the employee a case can be about, who must never see it.</summary>
    private protected void AsSid() => As(W.Sid, SidUser, "Sid Subject", [.. SelfGrants, .. ManagementGrants]);

    /// <summary>Moves the clock on, so later steps have later times.</summary>
    private protected void Later(int minutes = 1) => Host.Clock.TimeNow = Host.Clock.TimeNow.AddMinutes(minutes);

    // ---- Steps (each as the right person, through the real handlers) ----

    internal static string V(byte[] rowVersion) => Convert.ToBase64String(rowVersion);

    private protected static GrievanceDraftCreateRequest DraftRequest(int categoryId, bool anonymous = false, string subject = "TST Repeated unfair shift changes") =>
        new(categoryId, subject, "TST My shifts are changed without notice every single week.", null, "TST Floor 2", null, null, anonymous);

    private protected async Task<MyGrievanceDetailDto> DraftAsync(bool anonymous = false)
    {
        AsSam();
        return ResultAssert.Succeeded(await My.CreateDraftAsync(DraftRequest(W.Category.Id, anonymous), default));
    }

    private protected async Task<MyGrievanceDetailDto> SubmittedAsync(bool anonymous = false)
    {
        var draft = await DraftAsync(anonymous);
        Later();
        return ResultAssert.Succeeded(await My.SubmitAsync(draft.Id, new SubmitGrievanceRequest(V(draft.RowVersion), true), default));
    }

    private protected async Task<GrievanceCaseDetailDto> TeamGetAsync(int id) => ResultAssert.Succeeded(await Cases.GetAsync(id, default));

    private protected Task<Result<GrievanceCaseDetailDto>> Run<TCommand>(TCommand command)
        where TCommand : IEntityCommand =>
        Host.Get<ICommandHandler<TCommand, GrievanceCaseDetailDto>>().HandleAsync(command, default);

    private protected async Task<GrievanceCaseDetailDto> Ok<TCommand>(TCommand command)
        where TCommand : IEntityCommand
    {
        Later();
        return ResultAssert.Succeeded(await Run(command));
    }

    private protected byte[] Version(int id) => Host.Repository<GrievanceCase>().Items.Single(grievance => grievance.Id == id).RowVersion;

    private protected GrievanceCase Stored(int id) => Host.Repository<GrievanceCase>().Items.Single(grievance => grievance.Id == id);

    private protected async Task<GrievanceCaseDetailDto> AcknowledgedAsync(bool anonymous = false, string priority = "Medium")
    {
        var submitted = await SubmittedAsync(anonymous);
        AsHana();
        return await Ok(new AcknowledgeGrievanceCommand(submitted.Id, V(Version(submitted.Id)), priority, null, null));
    }

    private protected async Task<GrievanceCaseDetailDto> AssignedAsync(bool anonymous = false, int? investigator = null)
    {
        var acknowledged = await AcknowledgedAsync(anonymous);
        AsHana();
        return await Ok(new AssignGrievanceInvestigatorCommand(acknowledged.Id, V(Version(acknowledged.Id)), investigator ?? W.Ivan.Id, "Lead", null));
    }

    private protected async Task<GrievanceCaseDetailDto> UnderInvestigationAsync(bool anonymous = false)
    {
        var assigned = await AssignedAsync(anonymous);
        AsIvan();
        return await Ok(new StartGrievanceInvestigationCommand(assigned.Id, V(Version(assigned.Id)), null));
    }

    private protected async Task<GrievanceCaseDetailDto> ProposedAsync(bool anonymous = false)
    {
        var investigating = await UnderInvestigationAsync(anonymous);
        AsIvan();
        return await Ok(new ProposeGrievanceResolutionCommand(investigating.Id, V(Version(investigating.Id)), "TST Mediation and a fixed rota."));
    }

    private protected async Task<GrievanceCaseDetailDto> ResolvedAsync(bool anonymous = false, bool visible = true)
    {
        var proposed = await ProposedAsync(anonymous);
        AsRita();
        return await Ok(new ApproveGrievanceResolutionCommand(proposed.Id, V(Version(proposed.Id)), visible, null));
    }

    private protected async Task<GrievanceCaseDetailDto> ClosedAsync(bool anonymous = false)
    {
        var resolved = await ResolvedAsync(anonymous);
        AsHana();
        return await Ok(new CloseGrievanceCommand(resolved.Id, V(Version(resolved.Id)), null));
    }

    private protected async Task<GrievanceCaseDetailDto> AwaitingAsync(bool anonymous = false)
    {
        var investigating = await UnderInvestigationAsync(anonymous);
        AsIvan();
        return await Ok(new RequestGrievanceInfoCommand(investigating.Id, V(Version(investigating.Id)), "TST Which dates were affected?"));
    }

    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    internal static string Json(object value) => JsonSerializer.Serialize(value, WebJson);
}
