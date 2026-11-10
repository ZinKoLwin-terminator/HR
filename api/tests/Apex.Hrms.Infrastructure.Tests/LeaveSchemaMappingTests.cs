using Apex.Hrms.Domain.Attendance;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Leave;
using Apex.Hrms.Infrastructure.Persistence;
using Apex.Hrms.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Apex.Hrms.Infrastructure.Tests;

/// <summary>Checks the leave part of the EF model against the lve and att tables of the SSDT project. No connection is opened.</summary>
public sealed class LeaveSchemaMappingTests : IDisposable
{
    private readonly HrmsDbContext context = new(
        new DbContextOptionsBuilder<HrmsDbContext>()
            .UseSqlServer("Server=.;Database=HR;Integrated Security=True;Encrypt=True;TrustServerCertificate=True")
            .Options);

    public void Dispose() => context.Dispose();

    private IEntityType Entity<T>() => context.Model.FindEntityType(typeof(T))!;

    [Theory]
    [InlineData(typeof(LeaveType), "lve", "LeaveType")]
    [InlineData(typeof(LeavePolicy), "lve", "LeavePolicy")]
    [InlineData(typeof(LeaveApplication), "lve", "LeaveApplication")]
    [InlineData(typeof(Holiday), "att", "Holiday")]
    public void Tables_map_to_the_ssdt_schema_and_name(Type entity, string schema, string table)
    {
        var type = context.Model.FindEntityType(entity)!;

        Assert.Equal(schema, type.GetSchema());
        Assert.Equal(table, type.GetTableName());
    }

    [Fact]
    public void Every_leave_table_has_an_identity_key_and_a_rowversion_concurrency_token()
    {
        foreach (var type in new[] { Entity<LeaveType>(), Entity<LeavePolicy>(), Entity<LeaveApplication>(), Entity<Holiday>() })
        {
            var rowVersion = type.FindProperty("RowVersion")!;
            Assert.True(rowVersion.IsConcurrencyToken, type.Name);
            Assert.Equal(ValueGenerated.OnAddOrUpdate, rowVersion.ValueGenerated);
            Assert.Equal(ValueGenerated.OnAdd, type.FindProperty("Id")!.ValueGenerated);
            Assert.Equal("datetime2(3)", type.FindProperty("CreatedAtUtc")!.GetColumnType());
        }
    }

    [Fact]
    public void Columns_use_the_ssdt_types_and_lengths()
    {
        Assert.Equal("decimal(5,1)", Entity<LeaveType>().FindProperty("DefaultDaysPerYear")!.GetColumnType());
        Assert.Equal("decimal(5,1)", Entity<LeaveType>().FindProperty("MaxCarryForwardDays")!.GetColumnType());
        Assert.Equal("smallint", Entity<LeaveType>().FindProperty("MaxConsecutiveDays")!.GetColumnType());
        Assert.Equal("varchar(7)", Entity<LeaveType>().FindProperty("Color")!.GetColumnType());
        Assert.Equal("varchar(30)", Entity<LeaveType>().FindProperty("EligibleGender")!.GetColumnType());
        Assert.Equal(20, Entity<LeaveType>().FindProperty("Code")!.GetMaxLength());
        Assert.Equal(60, Entity<LeaveType>().FindProperty("Name")!.GetMaxLength());
        Assert.Equal(300, Entity<LeaveType>().FindProperty("Description")!.GetMaxLength());

        Assert.Equal(100, Entity<LeavePolicy>().FindProperty("Name")!.GetMaxLength());
        Assert.Equal("decimal(5,1)", Entity<LeavePolicy>().FindProperty("DaysPerYear")!.GetColumnType());
        Assert.Equal("smallint", Entity<LeavePolicy>().FindProperty("MinServiceMonths")!.GetColumnType());
        Assert.Equal("varchar(30)", Entity<LeavePolicy>().FindProperty("EmploymentType")!.GetColumnType());
        Assert.Equal("varchar(30)", Entity<LeavePolicy>().FindProperty("Accrual")!.GetColumnType());
        Assert.Equal("varchar(30)", Entity<LeavePolicy>().FindProperty("Status")!.GetColumnType());

        var application = Entity<LeaveApplication>();
        Assert.Equal("decimal(4,1)", application.FindProperty("Days")!.GetColumnType());
        Assert.Equal("date", application.FindProperty("StartDate")!.GetColumnType());
        Assert.Equal("date", application.FindProperty("EndDate")!.GetColumnType());
        Assert.Equal("date", application.FindProperty("AppliedOn")!.GetColumnType());
        Assert.Equal("date", application.FindProperty("ActionedOn")!.GetColumnType());
        Assert.Equal("varchar(30)", application.FindProperty("Status")!.GetColumnType());
        Assert.Equal(20, application.FindProperty("ApplicationNo")!.GetMaxLength());
        Assert.Equal(500, application.FindProperty("Reason")!.GetMaxLength());
        Assert.Equal(260, application.FindProperty("DocumentName")!.GetMaxLength());
        Assert.Equal(130, application.FindProperty("ActionedByName")!.GetMaxLength());
        Assert.Equal(250, application.FindProperty("ApproverRemarks")!.GetMaxLength());

        Assert.Equal("date", Entity<Holiday>().FindProperty("Date")!.GetColumnType());
    }

    [Fact]
    public void Unique_indexes_and_foreign_keys_match_ssdt_names()
    {
        Assert.Contains(Entity<LeaveType>().GetIndexes(), index => index.IsUnique && index.GetDatabaseName() == "UQ_LeaveType_Code");
        Assert.Contains(Entity<LeavePolicy>().GetIndexes(), index => index.IsUnique && index.GetDatabaseName() == "UQ_LeavePolicy_Name");
        Assert.Contains(Entity<LeaveApplication>().GetIndexes(), index => index.IsUnique && index.GetDatabaseName() == "UQ_LeaveApplication_ApplicationNo");

        var policyKeys = Entity<LeavePolicy>().GetForeignKeys().Select(key => key.GetConstraintName()).Order(StringComparer.Ordinal).ToList();
        Assert.Equal(["FK_LeavePolicy_JobGrade", "FK_LeavePolicy_LeaveType"], policyKeys);

        var applicationKeys = Entity<LeaveApplication>().GetForeignKeys().Select(key => key.GetConstraintName()).Order(StringComparer.Ordinal).ToList();
        Assert.Equal(
            ["FK_LeaveApplication_Employee", "FK_LeaveApplication_Employee_ApproverEmployeeId", "FK_LeaveApplication_LeaveType"],
            applicationKeys);
        Assert.All(
            Entity<LeaveApplication>().GetForeignKeys().Concat(Entity<LeavePolicy>().GetForeignKeys()),
            key => Assert.Equal(DeleteBehavior.NoAction, key.DeleteBehavior));
    }

    [Fact]
    public void The_history_table_and_period_columns_of_the_temporal_application_table_are_not_mapped()
    {
        var names = Entity<LeaveApplication>().GetProperties().Select(property => property.Name).ToList();

        Assert.DoesNotContain("ValidFromUtc", names);
        Assert.DoesNotContain("ValidToUtc", names);
        Assert.Null(context.Model.GetEntityTypes().FirstOrDefault(type => type.GetTableName() == "LeaveApplicationHistory"));
    }

    [Fact]
    public void Texts_are_stored_as_the_exact_values_the_database_checks()
    {
        var converter = Entity<LeaveApplication>().FindProperty("Status")!.GetValueConverter()!;
        var accrual = Entity<LeavePolicy>().FindProperty("Accrual")!.GetValueConverter()!;
        var employment = Entity<LeavePolicy>().FindProperty("EmploymentType")!.GetValueConverter()!;
        var gender = Entity<LeaveType>().FindProperty("EligibleGender")!.GetValueConverter()!;

        Assert.Equal("Cancelled", converter.ConvertToProvider(LeaveStatus.Cancelled));
        Assert.Equal("Monthly", accrual.ConvertToProvider(LeaveAccrual.Monthly));
        Assert.Equal("Full-time", employment.ConvertToProvider(Domain.Employees.EmploymentType.FullTime));
        Assert.Equal("Female", gender.ConvertToProvider(Domain.Employees.Gender.Female));
        Assert.Equal(LeaveStatus.Approved, converter.ConvertFromProvider("Approved"));
    }

    [Fact]
    public void An_application_list_query_with_filters_translates_to_sql_without_a_connection()
    {
        var from = new DateOnly(2026, 11, 1);
        var to = new DateOnly(2026, 11, 30);
        var sql = context.Set<LeaveApplication>().AsNoTracking()
            .Where(application => application.EmployeeId == 5
                && (application.Status == LeaveStatus.Submitted || application.Status == LeaveStatus.Approved)
                && application.StartDate <= to
                && application.EndDate >= from)
            .OrderBy(application => application.ActionedOn ?? DateOnly.MinValue)
            .ThenBy(application => application.Id)
            .ToQueryString();

        Assert.Contains("[lve].[LeaveApplication]", sql, StringComparison.Ordinal);
        Assert.Contains("[Status]", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void A_policy_group_comparison_with_nulls_translates_to_sql_that_treats_null_as_equal()
    {
        int? grade = null;
        Domain.Employees.EmploymentType? type = null;
        var sql = context.Set<LeavePolicy>().AsNoTracking()
            .Where(policy => policy.LeaveTypeId == 1 && policy.JobGradeId == grade && policy.EmploymentType == type)
            .ToQueryString();

        Assert.Contains("IS NULL", sql, StringComparison.Ordinal);
    }
}

public sealed class LeaveInfrastructureRuleTests
{
    [Theory]
    [InlineData("UQ_LeavePolicy_ActiveGroup", 2601, ErrorType.Validation, "leaveTypeId")]
    [InlineData("UQ_LeavePolicy_Name", 2627, ErrorType.Validation, "name")]
    [InlineData("UQ_LeaveType_Code", 2627, ErrorType.Validation, "code")]
    [InlineData("UQ_LeaveApplication_ApplicationNo", 2627, ErrorType.Conflict, null)]
    [InlineData("CK_LeaveApplication_NotOwnApprover", 547, ErrorType.Conflict, null)]
    [InlineData("CK_LeaveApplication_DateOrder", 547, ErrorType.Validation, "endDate")]
    [InlineData("CK_LeaveApplication_HalfDay", 547, ErrorType.Validation, "endDate")]
    [InlineData("CK_LeaveApplication_RejectionReason", 547, ErrorType.Validation, "remarks")]
    [InlineData("CK_LeaveType_Color", 547, ErrorType.Validation, "color")]
    [InlineData("CK_LeaveType_MaxCarryForwardDays", 547, ErrorType.Validation, "maxCarryForwardDays")]
    [InlineData("FK_LeaveApplication_Employee", 547, ErrorType.Conflict, null)]
    public void Leave_constraints_are_translated(string constraint, int number, ErrorType type, string? field)
    {
        var message = $"The statement conflicted with constraint '{constraint}'. The duplicate key value is (SECRET-VALUE).";

        var error = SqlConflicts.Translate(number, message)!;

        Assert.Equal(type, error.Type);
        Assert.DoesNotContain("SECRET-VALUE", error.Message, StringComparison.Ordinal);
        if (field is not null)
        {
            Assert.Contains(field, error.FieldErrors!.Keys);
        }
    }

    [Fact]
    public void The_weekend_defaults_to_saturday_and_sunday_and_reads_day_names()
    {
        Assert.Equal([DayOfWeek.Sunday, DayOfWeek.Saturday], ConfiguredBusinessWeek.From(null).WeekendDays.Order());
        Assert.Equal([DayOfWeek.Sunday, DayOfWeek.Saturday], ConfiguredBusinessWeek.From([]).WeekendDays.Order());
        Assert.Equal([DayOfWeek.Friday, DayOfWeek.Saturday], ConfiguredBusinessWeek.From(["friday", " Saturday "]).WeekendDays.Order());
    }

    [Theory]
    [InlineData("Funday")]
    [InlineData("1")]
    [InlineData("")]
    public void An_unknown_weekend_day_stops_the_start(string name) =>
        Assert.Throws<InvalidOperationException>(() => ConfiguredBusinessWeek.From([name]));
}
