using Apex.Hrms.Application.Employees;
using Apex.Hrms.Application.Leave.LeavePolicies;
using Apex.Hrms.Application.Leave.LeaveTypes;
using Apex.Hrms.Application.Organization.JobGrades;
using Apex.Hrms.Application.Tests.Fakes;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Application.Tests.Leave;

/// <summary>Leave types and leave policies: the validation rules of the mock services, uniqueness, activation and delete protection.</summary>
public sealed class LeaveTypeAndPolicyTests : IDisposable
{
    private readonly TestHost host = new();

    public void Dispose() => host.Dispose();

    // ---- Leave types ----

    [Fact]
    public async Task A_leave_type_is_created_with_its_fields_and_row_version()
    {
        var created = await host.CreateTypeAsync(LeaveFixtures.NewType("AL", "Annual Leave") with { Description = "  Paid vacation  ", EligibleGender = "Female" });

        Assert.Equal("AL", created.Code);
        Assert.Equal("Paid vacation", created.Description);
        Assert.Equal("Female", created.EligibleGender);
        Assert.Equal("#3f6fd8", created.Color);
        Assert.Equal("Active", created.Status);
        Assert.NotEmpty(created.RowVersion);
    }

    [Theory]
    [InlineData("al")]
    [InlineData("A L")]
    [InlineData("-AL")]
    [InlineData("")]
    public async Task The_code_must_be_upper_case_letters_digits_and_dashes(string code)
    {
        var result = await host.CreateType(LeaveFixtures.NewType(code));

        ResultAssert.FailedOnField(result, "code");
    }

    [Fact]
    public async Task The_code_must_be_unique()
    {
        await host.CreateTypeAsync(LeaveFixtures.NewType("AL"));

        var result = await host.CreateType(LeaveFixtures.NewType("AL", "Other"));

        ResultAssert.FailedOnField(result, "code", "Leave type code \"AL\" already exists.");
    }

    [Fact]
    public async Task At_least_one_consecutive_day_must_be_allowed()
    {
        var result = await host.CreateType(LeaveFixtures.NewType(maxConsecutive: 0));

        ResultAssert.FailedOnField(result, "maxConsecutiveDays", "Maximum consecutive days must be at least 1.");
    }

    [Fact]
    public async Task Carry_forward_cannot_exceed_the_yearly_entitlement()
    {
        var request = LeaveFixtures.NewType(days: 10m) with { CarryForward = true, MaxCarryForwardDays = 11m };

        var refused = await host.CreateType(request);
        var allowed = await host.CreateType(request with { MaxCarryForwardDays = 10m });
        var notCarried = await host.CreateType(request with { Code = "CL", Name = "Casual", CarryForward = false, MaxCarryForwardDays = 11m });

        ResultAssert.FailedOnField(refused, "maxCarryForwardDays", "Carry-forward days cannot exceed the yearly entitlement.");
        Assert.True(allowed.IsSuccess);
        Assert.True(notCarried.IsSuccess);
    }

    [Theory]
    [InlineData("3f6fd8")]
    [InlineData("#3f6fd")]
    [InlineData("#3f6fdg")]
    [InlineData("")]
    public async Task The_colour_must_be_a_hex_triplet(string color)
    {
        var result = await host.CreateType(LeaveFixtures.NewType() with { Color = color });

        ResultAssert.FailedOnField(result, "color");
    }

    [Fact]
    public async Task The_eligible_gender_must_be_a_known_gender_or_empty()
    {
        var unknown = await host.CreateType(LeaveFixtures.NewType(gender: "Robot"));
        var blank = ResultAssert.Succeeded(await host.CreateType(LeaveFixtures.NewType("CL", "Casual", gender: "  ")));

        ResultAssert.FailedOnField(unknown, "eligibleGender");
        Assert.Null(blank.EligibleGender);
    }

    [Fact]
    public async Task Days_have_at_most_one_decimal_place_and_are_not_negative()
    {
        ResultAssert.FailedOnField(await host.CreateType(LeaveFixtures.NewType(days: 1.25m)), "defaultDaysPerYear");
        ResultAssert.FailedOnField(await host.CreateType(LeaveFixtures.NewType(days: -1m)), "defaultDaysPerYear");
        Assert.True((await host.CreateType(LeaveFixtures.NewType(days: 7.5m))).IsSuccess);
    }

    [Fact]
    public async Task A_leave_type_can_be_updated_and_its_status_changes_only_by_command()
    {
        var created = await host.CreateTypeAsync();
        var update = host.Get<IUpdateHandler<LeaveTypeUpdateRequest, LeaveTypeDto>>();

        var changed = ResultAssert.Succeeded(await update.HandleAsync(created.Id, LeaveFixtures.TypeUpdateOf(created) with { Name = "Yearly Leave" }, default));
        var deactivated = ResultAssert.Succeeded(await host.Command<DeactivateCommand, LeaveTypeDto>(new DeactivateCommand(created.Id, Requests.Version(changed.RowVersion))));
        var activated = ResultAssert.Succeeded(await host.Command<ActivateCommand, LeaveTypeDto>(new ActivateCommand(created.Id, Requests.Version(deactivated.RowVersion))));

        Assert.Equal("Yearly Leave", changed.Name);
        Assert.Equal("Inactive", deactivated.Status);
        Assert.Equal("Active", activated.Status);
    }

    [Fact]
    public async Task A_stale_row_version_on_update_is_a_conflict()
    {
        var created = await host.CreateTypeAsync();
        var update = host.Get<IUpdateHandler<LeaveTypeUpdateRequest, LeaveTypeDto>>();
        ResultAssert.Succeeded(await update.HandleAsync(created.Id, LeaveFixtures.TypeUpdateOf(created) with { Name = "Yearly Leave" }, default));

        var result = await update.HandleAsync(created.Id, LeaveFixtures.TypeUpdateOf(created) with { Name = "Other" }, default);

        ResultAssert.Failed(result, ErrorType.Conflict);
    }

    [Fact]
    public async Task A_leave_type_with_applications_or_policies_cannot_be_deleted()
    {
        var org = await host.SeedOrgAsync();
        var employee = await host.CreateLeaveEmployeeAsync(org, "mia@example.test");
        var type = await host.CreateTypeAsync();
        var delete = host.Get<IDeleteHandler<LeaveTypeDto>>();
        await host.CreateApplicationAsync(LeaveFixtures.NewApplication(employee.Id, type.Id));
        await host.CreatePolicyAsync(LeaveFixtures.NewPolicy(type.Id));

        var result = await delete.HandleAsync(type.Id, Requests.Version(type.RowVersion), default);

        ResultAssert.Failed(
            result,
            ErrorType.Conflict,
            "\"Annual Leave\" cannot be deleted because it is used by 1 application(s), 1 policy(ies). Deactivate it instead.");
    }

    [Fact]
    public async Task An_unused_leave_type_can_be_deleted()
    {
        var type = await host.CreateTypeAsync();

        var result = await host.Get<IDeleteHandler<LeaveTypeDto>>().HandleAsync(type.Id, Requests.Version(type.RowVersion), default);

        Assert.True(result.IsSuccess);
        ResultAssert.Failed(await host.Get<IGetByIdHandler<LeaveTypeDto>>().HandleAsync(type.Id, default), ErrorType.NotFound);
    }

    // ---- Leave policies ----

    [Fact]
    public async Task A_policy_is_created_with_its_fields()
    {
        var type = await host.CreateTypeAsync();
        var grade = await host.CreateJobGradeAsync("G2");

        var policy = await host.CreatePolicyAsync(LeaveFixtures.NewPolicy(type.Id, jobGradeId: grade.Id, employmentType: "Contract", days: 18m, accrual: "Monthly", minServiceMonths: 3));

        Assert.Equal(type.Id, policy.LeaveTypeId);
        Assert.Equal(grade.Id, policy.JobGradeId);
        Assert.Equal("Contract", policy.EmploymentType);
        Assert.Equal(18m, policy.DaysPerYear);
        Assert.Equal("Monthly", policy.Accrual);
        Assert.Equal((short)3, policy.MinServiceMonths);
        Assert.Equal("Active", policy.Status);
    }

    [Fact]
    public async Task A_policy_needs_a_unique_name_an_existing_type_and_grade_and_valid_enums()
    {
        var type = await host.CreateTypeAsync();
        await host.CreatePolicyAsync(LeaveFixtures.NewPolicy(type.Id, "Standard"));

        ResultAssert.FailedOnField(
            await host.CreatePolicy(LeaveFixtures.NewPolicy(type.Id, "Standard", employmentType: "Intern")),
            "name",
            "Policy name \"Standard\" already exists.");
        ResultAssert.FailedOnField(await host.CreatePolicy(LeaveFixtures.NewPolicy(9999, "Other")), "leaveTypeId");
        ResultAssert.FailedOnField(await host.CreatePolicy(LeaveFixtures.NewPolicy(type.Id, "Other", jobGradeId: 9999)), "jobGradeId");
        ResultAssert.FailedOnField(await host.CreatePolicy(LeaveFixtures.NewPolicy(type.Id, "Other", accrual: "Daily")), "accrual");
        ResultAssert.FailedOnField(await host.CreatePolicy(LeaveFixtures.NewPolicy(type.Id, "Other", employmentType: "Casual")), "employmentType");
        ResultAssert.FailedOnField(await host.CreatePolicy(LeaveFixtures.NewPolicy(type.Id, "Other", days: -1m)), "daysPerYear");
        ResultAssert.FailedOnField(await host.CreatePolicy(LeaveFixtures.NewPolicy(type.Id, "Other", minServiceMonths: -1)), "minServiceMonths");
        ResultAssert.FailedOnField(await host.CreatePolicy(LeaveFixtures.NewPolicy(type.Id, " ")), "name");
    }

    [Fact]
    public async Task Only_one_active_policy_may_cover_the_same_type_grade_and_employment_type()
    {
        var type = await host.CreateTypeAsync();
        var otherType = await host.CreateTypeAsync(LeaveFixtures.NewType("CL", "Casual Leave"));
        var grade = await host.CreateJobGradeAsync("G2");
        await host.CreatePolicyAsync(LeaveFixtures.NewPolicy(type.Id, "All staff"));

        var clash = await host.CreatePolicy(LeaveFixtures.NewPolicy(type.Id, "All staff again"));
        var otherGroups = new[]
        {
            LeaveFixtures.NewPolicy(type.Id, "Grade", jobGradeId: grade.Id),
            LeaveFixtures.NewPolicy(type.Id, "Contract", employmentType: "Contract"),
            LeaveFixtures.NewPolicy(otherType.Id, "Casual all staff"),
        };

        var error = ResultAssert.FailedOnField(
            clash,
            "leaveTypeId",
            "An active policy already exists for this leave type, grade and employment type.");
        Assert.Equal("Another active policy covers the same group.", Assert.Single(error.FieldErrors!["leaveTypeId"]));
        foreach (var request in otherGroups)
        {
            Assert.True((await host.CreatePolicy(request)).IsSuccess, request.Name);
        }
    }

    [Fact]
    public async Task Inactive_policies_may_share_a_group_but_cannot_be_activated_into_a_clash()
    {
        var type = await host.CreateTypeAsync();
        var active = await host.CreatePolicyAsync(LeaveFixtures.NewPolicy(type.Id, "Active one"));
        var inactive = await host.CreatePolicyAsync(LeaveFixtures.NewPolicy(type.Id, "Inactive one", status: "Inactive"));
        var activate = host.Get<ICommandHandler<ActivateCommand, LeavePolicyDto>>();

        var refused = await activate.HandleAsync(new ActivateCommand(inactive.Id, Requests.Version(inactive.RowVersion)), default);
        ResultAssert.Succeeded(await host.Command<DeactivateCommand, LeavePolicyDto>(new DeactivateCommand(active.Id, Requests.Version(active.RowVersion))));
        var fresh = ResultAssert.Succeeded(await host.Get<IGetByIdHandler<LeavePolicyDto>>().HandleAsync(inactive.Id, default));
        var allowed = ResultAssert.Succeeded(await activate.HandleAsync(new ActivateCommand(inactive.Id, Requests.Version(fresh.RowVersion)), default));

        ResultAssert.FailedOnField(refused, "leaveTypeId", "An active policy already exists for this leave type, grade and employment type.");
        Assert.Equal("Active", allowed.Status);
    }

    [Fact]
    public async Task Activating_an_active_policy_changes_nothing()
    {
        var type = await host.CreateTypeAsync();
        var active = await host.CreatePolicyAsync(LeaveFixtures.NewPolicy(type.Id));

        var again = ResultAssert.Succeeded(await host.Command<ActivateCommand, LeavePolicyDto>(new ActivateCommand(active.Id, Requests.Version(active.RowVersion))));

        Assert.Equal("Active", again.Status);
    }

    [Fact]
    public async Task Updating_an_active_policy_into_the_group_of_another_is_refused()
    {
        var type = await host.CreateTypeAsync();
        var grade = await host.CreateJobGradeAsync("G2");
        await host.CreatePolicyAsync(LeaveFixtures.NewPolicy(type.Id, "All staff"));
        var gradePolicy = await host.CreatePolicyAsync(LeaveFixtures.NewPolicy(type.Id, "Grade", jobGradeId: grade.Id));
        var update = host.Get<IUpdateHandler<LeavePolicyUpdateRequest, LeavePolicyDto>>();

        var clash = await update.HandleAsync(gradePolicy.Id, LeaveFixtures.PolicyUpdateOf(gradePolicy) with { JobGradeId = null }, default);
        var fine = await update.HandleAsync(gradePolicy.Id, LeaveFixtures.PolicyUpdateOf(gradePolicy, days: 20m), default);

        ResultAssert.FailedOnField(clash, "leaveTypeId");
        Assert.Equal(20m, ResultAssert.Succeeded(fine).DaysPerYear);
    }

    [Fact]
    public async Task A_policy_can_be_deleted_and_blocks_deleting_its_job_grade()
    {
        var type = await host.CreateTypeAsync();
        var grade = await host.CreateJobGradeAsync("G2");
        var policy = await host.CreatePolicyAsync(LeaveFixtures.NewPolicy(type.Id, jobGradeId: grade.Id));

        var blocked = await host.Get<IDeleteHandler<JobGradeDto>>().HandleAsync(grade.Id, Requests.Version(grade.RowVersion), default);
        var deleted = await host.Get<IDeleteHandler<LeavePolicyDto>>().HandleAsync(policy.Id, Requests.Version(policy.RowVersion), default);
        var fresh = ResultAssert.Succeeded(await host.Get<IGetByIdHandler<JobGradeDto>>().HandleAsync(grade.Id, default));
        var allowed = await host.Get<IDeleteHandler<JobGradeDto>>().HandleAsync(grade.Id, Requests.Version(fresh.RowVersion), default);

        Assert.Contains("1 leave policy(ies)", ResultAssert.Failed(blocked, ErrorType.Conflict).Message, StringComparison.Ordinal);
        Assert.True(deleted.IsSuccess);
        Assert.True(allowed.IsSuccess);
    }

    [Fact]
    public async Task An_employee_with_leave_applications_cannot_be_deleted()
    {
        var org = await host.SeedOrgAsync();
        var employee = await host.CreateLeaveEmployeeAsync(org, "mia@example.test");
        var type = await host.CreateTypeAsync();
        await host.CreateApplicationAsync(LeaveFixtures.NewApplication(employee.Id, type.Id));

        var result = await host.Get<IDeleteHandler<EmployeeDto>>().HandleAsync(employee.Id, Requests.Version(employee.RowVersion), default);

        Assert.Contains("1 leave application(s)", ResultAssert.Failed(result, ErrorType.Conflict).Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Policies_and_types_can_be_queried_with_filters()
    {
        var type = await host.CreateTypeAsync();
        var other = await host.CreateTypeAsync(LeaveFixtures.NewType("CL", "Casual Leave", status: "Inactive"));
        await host.CreatePolicyAsync(LeaveFixtures.NewPolicy(type.Id, "Standard"));
        await host.CreatePolicyAsync(LeaveFixtures.NewPolicy(type.Id, "Interns", employmentType: "Intern", accrual: "Monthly"));
        await host.CreatePolicyAsync(LeaveFixtures.NewPolicy(other.Id, "Casual", status: "Inactive"));
        var policies = host.Get<IQueryHandler<LeavePolicyDto, LeavePolicyFilter>>();
        var types = host.Get<IQueryHandler<LeaveTypeDto, LeaveTypeFilter>>();

        var forType = ResultAssert.Succeeded(await policies.HandleAsync(new PageRequest<LeavePolicyFilter>(0, 10, null, null, new LeavePolicyFilter(LeaveTypeId: type.Id)), default));
        var interns = ResultAssert.Succeeded(await policies.HandleAsync(new PageRequest<LeavePolicyFilter>(0, 10, null, null, new LeavePolicyFilter(EmploymentType: "Intern", Accrual: "Monthly")), default));
        var inactive = ResultAssert.Succeeded(await policies.HandleAsync(new PageRequest<LeavePolicyFilter>(0, 10, null, null, new LeavePolicyFilter(Status: "Inactive")), default));
        var searched = ResultAssert.Succeeded(await types.HandleAsync(new PageRequest<LeaveTypeFilter>(0, 10, new SortState("name", "desc"), "Leave", new LeaveTypeFilter(Status: "all")), default));
        var activeTypes = ResultAssert.Succeeded(await types.HandleAsync(new PageRequest<LeaveTypeFilter>(0, 10, null, null, new LeaveTypeFilter(Status: "Active")), default));

        Assert.Equal(2, forType.Total);
        Assert.Equal("Interns", Assert.Single(interns.Items).Name);
        Assert.Equal("Casual", Assert.Single(inactive.Items).Name);
        Assert.Equal(["Casual Leave", "Annual Leave"], searched.Items.Select(item => item.Name));
        Assert.Equal("AL", Assert.Single(activeTypes.Items).Code);
    }
}
