using System.Reflection;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Domain.Common;
using DomainEntity = Apex.Hrms.Domain.Common.Entity;
using Apex.Hrms.Domain.Payroll;
using Apex.Hrms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Apex.Hrms.Infrastructure.Tests;

/// <summary>
/// Checks the payroll part of the EF model against the pay tables of the SSDT project, and what EF would send for the moves the
/// triggers watch. No connection is opened: the change tracker is inspected where a save would be.
/// </summary>
public sealed class PayrollSchemaMappingTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 11, 9);

    private readonly HrmsDbContext context = new(
        new DbContextOptionsBuilder<HrmsDbContext>()
            .UseSqlServer("Server=.;Database=HR;Integrated Security=True;Encrypt=True;TrustServerCertificate=True")
            .Options);

    public void Dispose() => context.Dispose();

    private IEntityType Entity<T>() => context.Model.FindEntityType(typeof(T))!;

    [Theory]
    [InlineData(typeof(SalaryComponent), "SalaryComponent")]
    [InlineData(typeof(SalaryStructure), "SalaryStructure")]
    [InlineData(typeof(SalaryStructureComponent), "SalaryStructureComponent")]
    [InlineData(typeof(SalaryAssignment), "SalaryAssignment")]
    [InlineData(typeof(PayrollRun), "PayrollRun")]
    [InlineData(typeof(PayrollRunWarning), "PayrollRunWarning")]
    [InlineData(typeof(Payslip), "Payslip")]
    [InlineData(typeof(PayslipLine), "PayslipLine")]
    public void Tables_map_to_the_pay_schema(Type entity, string table)
    {
        var type = context.Model.FindEntityType(entity)!;

        Assert.Equal("pay", type.GetSchema());
        Assert.Equal(table, type.GetTableName());
    }

    [Fact]
    public void The_audited_pay_tables_have_a_rowversion_concurrency_token()
    {
        foreach (var type in new[] { Entity<SalaryComponent>(), Entity<SalaryStructure>(), Entity<SalaryAssignment>(), Entity<PayrollRun>() })
        {
            var rowVersion = type.FindProperty("RowVersion")!;
            Assert.True(rowVersion.IsConcurrencyToken, type.Name);
            Assert.Equal(ValueGenerated.OnAddOrUpdate, rowVersion.ValueGenerated);
            Assert.Equal("datetime2(3)", type.FindProperty("CreatedAtUtc")!.GetColumnType());
            Assert.Equal("datetime2(3)", type.FindProperty("ModifiedAtUtc")!.GetColumnType());
        }
    }

    [Fact]
    public void Payslips_and_their_lines_are_never_changed_so_they_have_no_rowversion()
    {
        Assert.Null(Entity<Payslip>().FindProperty("RowVersion"));
        Assert.Null(Entity<PayslipLine>().FindProperty("RowVersion"));
        Assert.Equal("datetime2(3)", Entity<Payslip>().FindProperty("CreatedAtUtc")!.GetColumnType());
    }

    [Fact]
    public void Money_columns_are_decimal_never_floating_point()
    {
        foreach (var (type, properties) in new (IEntityType, string[])[]
        {
            (Entity<SalaryComponent>(), ["Value"]),
            (Entity<SalaryAssignment>(), ["BasicSalary"]),
            (Entity<PayrollRun>(), ["TotalBasic", "TotalAllowances", "TotalGross", "TotalDeductions", "TotalNet"]),
            (Entity<Payslip>(), ["BasicSalary", "GrossSalary", "TotalDeductions", "NetSalary"]),
            (Entity<PayslipLine>(), ["Amount"]),
        })
        {
            foreach (var property in properties)
            {
                Assert.Equal("decimal(18,2)", type.FindProperty(property)!.GetColumnType());
            }
        }
    }

    [Fact]
    public void Columns_use_the_ssdt_types_and_lengths()
    {
        Assert.Equal("varchar(30)", Entity<SalaryComponent>().FindProperty("Type")!.GetColumnType());
        Assert.Equal("varchar(30)", Entity<SalaryComponent>().FindProperty("Calculation")!.GetColumnType());
        Assert.Equal(20, Entity<SalaryComponent>().FindProperty("Code")!.GetMaxLength());
        Assert.Equal(60, Entity<SalaryComponent>().FindProperty("Name")!.GetMaxLength());
        Assert.Equal(250, Entity<SalaryComponent>().FindProperty("Description")!.GetMaxLength());
        Assert.Equal(250, Entity<SalaryStructure>().FindProperty("Description")!.GetMaxLength());
        Assert.Equal("smallint", Entity<SalaryStructureComponent>().FindProperty("SortOrder")!.GetColumnType());
        Assert.Equal("date", Entity<SalaryAssignment>().FindProperty("EffectiveFrom")!.GetColumnType());
        Assert.Equal(120, Entity<SalaryAssignment>().FindProperty("Remarks")!.GetMaxLength());

        var run = Entity<PayrollRun>();
        Assert.Equal("smallint", run.FindProperty("Year")!.GetColumnType());
        Assert.Equal("tinyint", run.FindProperty("Month")!.GetColumnType());
        Assert.Equal("varchar(30)", run.FindProperty("Status")!.GetColumnType());
        Assert.Equal(30, run.FindProperty("PeriodName")!.GetMaxLength());
        Assert.Equal("date", run.FindProperty("StartDate")!.GetColumnType());
        Assert.Equal("date", run.FindProperty("ProcessedOn")!.GetColumnType());
        Assert.Equal(130, run.FindProperty("ProcessedByName")!.GetMaxLength());
        Assert.Equal(130, run.FindProperty("ApprovedByName")!.GetMaxLength());
        Assert.Equal(400, Entity<PayrollRunWarning>().FindProperty("Message")!.GetMaxLength());

        var payslip = Entity<Payslip>();
        Assert.Equal(9, payslip.FindProperty("AccountMasked")!.GetMaxLength());
        Assert.Equal("tinyint", payslip.FindProperty("PeriodDays")!.GetColumnType());
        Assert.Equal("tinyint", payslip.FindProperty("DaysEmployed")!.GetColumnType());
        Assert.Equal("tinyint", payslip.FindProperty("WorkingDays")!.GetColumnType());
        Assert.Equal("decimal(4,1)", payslip.FindProperty("UnpaidLeaveDays")!.GetColumnType());
        Assert.Equal(12, payslip.FindProperty("EmployeeCode")!.GetMaxLength());
        Assert.Equal(130, payslip.FindProperty("EmployeeName")!.GetMaxLength());
        Assert.Equal(60, payslip.FindProperty("BankName")!.GetMaxLength());
        Assert.Equal("varchar(30)", Entity<PayslipLine>().FindProperty("LineType")!.GetColumnType());
        Assert.Equal(100, Entity<PayslipLine>().FindProperty("Basis")!.GetMaxLength());
    }

    [Fact]
    public void Unique_keys_and_foreign_keys_carry_the_names_the_error_translation_reads()
    {
        Assert.Contains(Entity<PayrollRun>().GetIndexes(), index => index.GetDatabaseName() == "UQ_PayrollRun_Year_Month" && index.IsUnique);
        Assert.Contains(Entity<SalaryAssignment>().GetIndexes(), index => index.GetDatabaseName() == "UQ_SalaryAssignment_EmployeeId_EffectiveFrom" && index.IsUnique);
        Assert.Contains(Entity<Payslip>().GetIndexes(), index => index.GetDatabaseName() == "UQ_Payslip_PayrollRunId_EmployeeId" && index.IsUnique);
        Assert.Contains(Entity<SalaryComponent>().GetIndexes(), index => index.GetDatabaseName() == "UQ_SalaryComponent_Code" && index.IsUnique);
        Assert.Contains(Entity<SalaryStructure>().GetIndexes(), index => index.GetDatabaseName() == "UQ_SalaryStructure_Code" && index.IsUnique);
        Assert.Contains(Entity<SalaryStructureComponent>().GetIndexes(), index => index.GetDatabaseName() == "UQ_SalaryStructureComponent_SortOrder" && index.IsUnique);
        Assert.Equal(
            ["PayrollRunId", "EmployeeId"],
            Entity<Payslip>().GetForeignKeys().Select(key => key.Properties.Single().Name).Order().Reverse());
    }

    [Fact]
    public void The_tables_with_triggers_are_declared_so_that_ef_does_not_save_with_an_output_clause()
    {
        // SQL Server refuses an INSERT or UPDATE with an OUTPUT clause on a table that has triggers; EF uses one to read the identity and the rowversion unless it is told.
        Assert.Equal(["TR_PayrollRun_PaidIsFinal"], Entity<PayrollRun>().GetDeclaredTriggers().Select(trigger => trigger.ModelName));
        Assert.Equal(["TR_PayrollRunWarning_PaidRunIsFinal"], Entity<PayrollRunWarning>().GetDeclaredTriggers().Select(trigger => trigger.ModelName));
        Assert.Equal(["TR_Payslip_PaidRunIsFinal"], Entity<Payslip>().GetDeclaredTriggers().Select(trigger => trigger.ModelName));
        Assert.Equal(["TR_PayslipLine_PaidRunIsFinal"], Entity<PayslipLine>().GetDeclaredTriggers().Select(trigger => trigger.ModelName));
    }

    // ---- What EF sends for the moves the triggers watch ----

    private static void Set(object entity, string property, object value) =>
        typeof(DomainEntity).GetProperty(property, BindingFlags.Public | BindingFlags.Instance)!.SetValue(entity, value);

    private static PayrollRun ApprovedRun(params string[] warnings)
    {
        var processor = new PayrollActor(1, "Pat Processor");
        var run = PayrollRun.Create(new PayrollRunDetails(2026, 11, null), Today).Value;
        Assert.True(run.Process(processor, new PayrollTotals(1, 1000m, 1150m, 80.5m, 1069.5m), warnings, Today, DateTime.UtcNow).IsSuccess);
        Assert.True(run.Approve(new PayrollActor(2, "Alex Approver"), Today).IsSuccess);
        Set(run, nameof(DomainEntity.Id), 5);
        var id = 100;
        foreach (var warning in run.Warnings)
        {
            Set(warning, nameof(DomainEntity.Id), ++id);
        }

        return run;
    }

    private static List<string> ModifiedProperties(PayrollRun run, HrmsDbContext context) =>
        [.. context.Entry(run).Properties.Where(property => property.IsModified).Select(property => property.Metadata.Name).Order(StringComparer.Ordinal)];

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow => new(2026, 11, 10, 8, 0, 0, TimeSpan.Zero);

        public DateOnly Today => new(2026, 11, 10);

        public TimeOnly TimeNow => new(8, 0);
    }

    private sealed class SignedIn : ICurrentUser
    {
        public int? UserId => 2;

        public int? EmployeeId => null;

        public bool IsAuthenticated => true;

        public bool IsSystem => false;

        public string DisplayName => "Alex Approver";
    }

    [Fact]
    public void Marking_a_run_paid_changes_only_the_status_the_paid_date_and_the_modified_columns()
    {
        var run = ApprovedRun();
        context.Attach(run);

        Assert.True(run.MarkPaid(new DateOnly(2026, 11, 10)).IsSuccess);
        var interceptor = new AuditStampInterceptor(new FixedClock(), new SignedIn());
        interceptor.SavingChanges(new DbContextEventData(null!, (_, _) => string.Empty, context), default);

        // The trigger of pay.PayrollRun refuses a run that is being paid when any other column differs.
        Assert.Equal(["ModifiedAtUtc", "ModifiedByUserId", "PaidOn", "Status"], ModifiedProperties(run, context));
    }

    [Fact]
    public void Approving_a_run_changes_only_the_approval_columns_the_status_and_the_modified_columns()
    {
        var processor = new PayrollActor(1, "Pat Processor");
        var run = PayrollRun.Create(new PayrollRunDetails(2026, 11, null), Today).Value;
        run.Process(processor, new PayrollTotals(1, 1000m, 1150m, 80.5m, 1069.5m), [], Today, DateTime.UtcNow);
        Set(run, nameof(DomainEntity.Id), 5);
        context.Attach(run);

        Assert.True(run.Approve(new PayrollActor(2, "Alex Approver"), Today).IsSuccess);
        var interceptor = new AuditStampInterceptor(new FixedClock(), new SignedIn());
        interceptor.SavingChanges(new DbContextEventData(null!, (_, _) => string.Empty, context), default);

        Assert.Equal(
            ["ApprovedByName", "ApprovedByUserId", "ApprovedOn", "ModifiedAtUtc", "ModifiedByUserId", "Status"],
            ModifiedProperties(run, context));
    }

    [Fact]
    public void Returning_a_run_to_draft_leaves_its_warnings_for_an_explicit_delete_instead_of_a_tracked_one()
    {
        // The deletes must follow the update of the run (the triggers lock an approved run's warnings and payslips until it is
        // Draft), so nothing here may make the change tracker delete them in the same save.
        var run = ApprovedRun("EMP-0009 Test Person has no salary assignment and was skipped.");
        context.Attach(run);

        Assert.True(run.ReturnToDraft().IsSuccess);
        context.ChangeTracker.DetectChanges();

        Assert.All(context.ChangeTracker.Entries<PayrollRunWarning>(), entry => Assert.Equal(EntityState.Unchanged, entry.State));
        Assert.Equal(EntityState.Modified, context.Entry(run).State);
        Assert.Contains("Status", ModifiedProperties(run, context));
        Assert.Contains("TotalNet", ModifiedProperties(run, context));
    }

    [Fact]
    public void A_structure_that_changes_its_components_sends_inserts_updates_and_deletes_that_never_share_a_sort_order()
    {
        var structure = SalaryStructure.Create(new SalaryStructureDetails("STAFF", "Staff", null, [1, 2, 3]), RecordStatus.Active).Value;
        Set(structure, nameof(DomainEntity.Id), 7);
        // As loaded from the database: the links carry the key of their structure (a composite key with a missing part would be tracked as new).
        foreach (var link in structure.Components)
        {
            typeof(SalaryStructureComponent).GetProperty(nameof(SalaryStructureComponent.SalaryStructureId))!.SetValue(link, 7);
        }

        context.Attach(structure);
        var oldOrders = structure.Components.ToDictionary(link => link.SalaryComponentId, link => link.SortOrder);

        Assert.True(structure.Update(new SalaryStructureDetails("STAFF", "Staff", null, [3, 4, 1])).IsSuccess);
        context.ChangeTracker.DetectChanges();

        var states = context.ChangeTracker.Entries<SalaryStructureComponent>().ToDictionary(entry => entry.Entity.SalaryComponentId, entry => entry.State);
        Assert.Equal(EntityState.Deleted, states[2]);
        Assert.Equal(EntityState.Added, states[4]);
        Assert.Equal(EntityState.Modified, states[3]);
        Assert.Equal(EntityState.Modified, states[1]);
        var survivors = structure.Components.Select(link => link.SortOrder).ToList();
        Assert.DoesNotContain(survivors, order => oldOrders.ContainsValue(order));
    }
}
