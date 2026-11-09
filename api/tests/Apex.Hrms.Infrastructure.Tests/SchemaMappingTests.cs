using Apex.Hrms.Application.Employees;
using Apex.Hrms.Domain.Employees;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Organization;
using Apex.Hrms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Apex.Hrms.Infrastructure.Tests;

/// <summary>
/// Checks the EF model against the SSDT schema (the HR DBPJ SSDT project). The context is only used to build the model and
/// SQL text; no connection is ever opened.
/// </summary>
public sealed class SchemaMappingTests : IDisposable
{
    private readonly HrmsDbContext context = new(
        new DbContextOptionsBuilder<HrmsDbContext>()
            .UseSqlServer("Server=.;Database=HR;Integrated Security=True;Encrypt=True;TrustServerCertificate=True")
            .Options);

    public void Dispose() => context.Dispose();

    private IEntityType Entity<T>() => context.Model.FindEntityType(typeof(T))!;

    [Theory]
    [InlineData(typeof(Company), "org", "Company")]
    [InlineData(typeof(Branch), "org", "Branch")]
    [InlineData(typeof(Department), "org", "Department")]
    [InlineData(typeof(Section), "org", "Section")]
    [InlineData(typeof(JobGrade), "org", "JobGrade")]
    [InlineData(typeof(Position), "org", "Position")]
    [InlineData(typeof(Employee), "hr", "Employee")]
    [InlineData(typeof(EmployeeBankAccount), "hr", "EmployeeBankAccount")]
    [InlineData(typeof(EmployeeDocument), "hr", "EmployeeDocument")]
    [InlineData(typeof(EmploymentHistoryEntry), "hr", "EmploymentHistory")]
    public void Tables_map_to_the_ssdt_schema_and_name(Type entity, string schema, string table)
    {
        var type = context.Model.FindEntityType(entity)!;

        Assert.Equal(schema, type.GetSchema());
        Assert.Equal(table, type.GetTableName());
    }

    [Fact]
    public void Every_org_table_has_an_identity_key_and_a_rowversion_concurrency_token()
    {
        foreach (var type in new[] { Entity<Company>(), Entity<Branch>(), Entity<Department>(), Entity<Section>(), Entity<JobGrade>(), Entity<Position>() })
        {
            var rowVersion = type.FindProperty("RowVersion")!;
            Assert.True(rowVersion.IsConcurrencyToken, type.Name);
            Assert.Equal(ValueGenerated.OnAddOrUpdate, rowVersion.ValueGenerated);
            Assert.Equal(ValueGenerated.OnAdd, type.FindProperty("Id")!.ValueGenerated);
        }
    }

    [Fact]
    public void Status_and_type_are_non_unicode_varchar_30()
    {
        Assert.Equal("varchar(30)", Entity<Company>().FindProperty("Status")!.GetColumnType());
        Assert.Equal("varchar(30)", Entity<Branch>().FindProperty("Type")!.GetColumnType());
    }

    [Fact]
    public void Money_dates_and_small_integers_use_the_ssdt_column_types()
    {
        Assert.Equal("decimal(18,2)", Entity<JobGrade>().FindProperty("MinSalary")!.GetColumnType());
        Assert.Equal("smallint", Entity<JobGrade>().FindProperty("Level")!.GetColumnType());
        Assert.Equal("smallint", Entity<Position>().FindProperty("Headcount")!.GetColumnType());
        Assert.Equal("date", Entity<Company>().FindProperty("FoundedDate")!.GetColumnType());
        Assert.Equal("datetime2(3)", Entity<Company>().FindProperty("CreatedAtUtc")!.GetColumnType());
    }

    [Theory]
    [InlineData(typeof(Company), "Name", 100)]
    [InlineData(typeof(Company), "LegalName", 150)]
    [InlineData(typeof(Branch), "Email", 120)]
    [InlineData(typeof(Department), "CostCenter", 20)]
    [InlineData(typeof(JobGrade), "Name", 60)]
    [InlineData(typeof(Position), "Title", 100)]
    [InlineData(typeof(Section), "Description", 300)]
    public void Text_columns_have_the_ssdt_lengths(Type entity, string property, int length) =>
        Assert.Equal(length, context.Model.FindEntityType(entity)!.FindProperty(property)!.GetMaxLength());

    [Fact]
    public void Unique_constraints_match_ssdt()
    {
        AssertUnique<Company>("UQ_Company_Code", "Code");
        AssertUnique<Company>("UQ_Company_Name", "Name");
        AssertUnique<Branch>("UQ_Branch_Code", "Code");
        AssertUnique<Department>("UQ_Department_Code", "Code");
        AssertUnique<Section>("UQ_Section_Code", "Code");
        AssertUnique<JobGrade>("UQ_JobGrade_Code", "Code");
        AssertUnique<Position>("UQ_Position_Code", "Code");
    }

    [Fact]
    public void Foreign_keys_map_the_plain_parent_columns()
    {
        // The database foreign keys are composite (BranchId + CompanyId, SectionId + DepartmentId). EF maps only the
        // parent column, so the other column stays an ordinary, changeable property.
        AssertForeignKey<Department>("FK_Department_Branch", "BranchId");
        AssertForeignKey<Position>("FK_Position_Section", "SectionId");
        AssertForeignKey<Position>("FK_Position_Department", "DepartmentId");
        AssertForeignKey<Section>("FK_Section_Department", "DepartmentId");
        AssertForeignKey<Branch>("FK_Branch_Company", "CompanyId");
        AssertForeignKey<Position>("FK_Position_Position_ReportsToPositionId", "ReportsToPositionId");
    }

    [Fact]
    public void Parent_columns_are_not_part_of_any_key_so_a_child_can_change_parent()
    {
        foreach (var entity in new[] { Entity<Branch>(), Entity<Department>(), Entity<Section>(), Entity<Position>() })
        {
            Assert.Single(entity.GetKeys());
            Assert.Equal(["Id"], entity.FindPrimaryKey()!.Properties.Select(property => property.Name));
        }
    }

    [Fact]
    public void Changing_the_parent_of_a_tracked_branch_is_detected_without_error() =>
        AssertParentChangeIsDetected(
            Branch.Create(new BranchDetails(1, "YGN", "Yangon", BranchType.Branch, null, null, null, "Yangon", "Myanmar", null, new DateOnly(2020, 1, 1)), RecordStatus.Active, Today).Value,
            branch => branch.Update(new BranchDetails(2, "YGN", "Yangon", BranchType.Branch, null, null, null, "Yangon", "Myanmar", null, new DateOnly(2020, 1, 1)), Today),
            nameof(Branch.CompanyId));

    [Fact]
    public void Changing_the_parent_of_a_tracked_department_is_detected_without_error() =>
        AssertParentChangeIsDetected(
            Department.Create(new DepartmentDetails(1, 1, "ENG", "Engineering", null, null, null), RecordStatus.Active).Value,
            department => department.Update(new DepartmentDetails(1, 2, "ENG", "Engineering", null, null, null)),
            nameof(Department.BranchId));

    [Fact]
    public void Changing_the_company_of_a_tracked_department_is_detected_without_error() =>
        AssertParentChangeIsDetected(
            Department.Create(new DepartmentDetails(1, 1, "ENG", "Engineering", null, null, null), RecordStatus.Active).Value,
            department => department.Update(new DepartmentDetails(2, 1, "ENG", "Engineering", null, null, null)),
            nameof(Department.CompanyId));

    [Fact]
    public void Changing_the_parent_of_a_tracked_section_is_detected_without_error() =>
        AssertParentChangeIsDetected(
            Section.Create(new SectionDetails(1, "BE", "Backend", null, null), RecordStatus.Active).Value,
            section => section.Update(new SectionDetails(2, "BE", "Backend", null, null)),
            nameof(Section.DepartmentId));

    [Fact]
    public void Changing_the_department_of_a_tracked_position_is_detected_without_error() =>
        AssertParentChangeIsDetected(
            Position.Create(new PositionDetails(1, null, 1, "DEV", "Developer", null, 1, null), RecordStatus.Active).Value,
            position => position.Update(new PositionDetails(2, null, 1, "DEV", "Developer", null, 1, null)),
            nameof(Position.DepartmentId));

    [Fact]
    public void Changing_the_section_of_a_tracked_position_is_detected_without_error() =>
        AssertParentChangeIsDetected(
            Position.Create(new PositionDetails(1, 1, 1, "DEV", "Developer", null, 1, null), RecordStatus.Active).Value,
            position => position.Update(new PositionDetails(1, 2, 1, "DEV", "Developer", null, 1, null)),
            nameof(Position.SectionId));

    [Fact]
    public void Foreign_keys_never_cascade_deletes_except_the_employee_children_the_database_cascades()
    {
        // ON DELETE CASCADE exists in the database only for the rows an employee owns.
        var cascading = new[] { typeof(EmployeeBankAccount), typeof(EmployeeDocument), typeof(EmploymentHistoryEntry) };
        foreach (var type in context.Model.GetEntityTypes().Where(type => !type.IsOwned()))
        {
            var expected = cascading.Contains(type.ClrType) ? DeleteBehavior.Cascade : DeleteBehavior.NoAction;
            Assert.All(type.GetForeignKeys(), key => Assert.Equal(expected, key.DeleteBehavior));
        }
    }

    [Fact]
    public void Employee_maps_the_ssdt_columns_and_keeps_the_history_tables_out_of_the_model()
    {
        var employee = Entity<Employee>();

        Assert.Equal("EmploymentStatus", employee.FindProperty("Status")!.GetColumnName());
        Assert.Equal("varchar(30)", employee.FindProperty("Status")!.GetColumnType());
        Assert.Equal("varchar(30)", employee.FindProperty("Gender")!.GetColumnType());
        Assert.Equal("varchar(30)", employee.FindProperty("EmploymentType")!.GetColumnType());
        Assert.Equal("date", employee.FindProperty("DateOfBirth")!.GetColumnType());
        Assert.Equal(12, employee.FindProperty("EmployeeCode")!.GetMaxLength());
        Assert.Equal(40, employee.FindProperty("NationalId")!.GetMaxLength());
        Assert.True(employee.FindProperty("RowVersion")!.IsConcurrencyToken);
        Assert.Equal(ValueGenerated.OnAddOrUpdate, employee.FindProperty("RowVersion")!.ValueGenerated);

        // SQL Server maintains the temporal history tables; the model must not know them.
        Assert.DoesNotContain(
            context.Model.GetEntityTypes(),
            type => type.GetTableName() is { } name && name.EndsWith("History", StringComparison.Ordinal) && type.ClrType != typeof(EmploymentHistoryEntry));
        Assert.Null(employee.FindProperty("ValidFromUtc"));
        AssertUnique<Employee>("UQ_Employee_EmployeeCode", "EmployeeCode");
        AssertUnique<Employee>("UQ_Employee_WorkEmail", "WorkEmail");
        AssertUnique<EmployeeBankAccount>("UQ_EmployeeBankAccount_EmployeeId", "EmployeeId");
        Assert.Equal("varchar(20)", Entity<EmployeeBankAccount>().FindProperty("AccountNumber")!.GetColumnType());
    }

    [Fact]
    public void Address_and_emergency_contact_are_columns_of_the_employee_row()
    {
        var employee = Entity<Employee>();
        var address = employee.FindNavigation(nameof(Employee.Address))!.TargetEntityType;
        var contact = employee.FindNavigation(nameof(Employee.EmergencyContact))!.TargetEntityType;

        Assert.Equal(AddressColumns, AddressProperties.Select(name => address.FindProperty(name)!.GetColumnName()));
        Assert.Equal(ContactColumns, ContactProperties.Select(name => contact.FindProperty(name)!.GetColumnName()));
        Assert.Equal("hr", address.GetSchema());
        Assert.Equal("Employee", address.GetTableName());
    }

    [Fact]
    public void Employee_org_link_maps_the_view_and_the_code_sequence_is_declared()
    {
        var link = Entity<EmployeeOrgLink>();

        Assert.Equal("EmployeeOrgLink", link.GetViewName());
        Assert.Equal("hr", link.GetViewSchema());
        Assert.Null(link.GetTableName());
        var sequence = context.Model.FindSequence("EmployeeCodeSeq", "hr");
        Assert.NotNull(sequence);
        Assert.Equal(1, sequence.StartValue);
    }

    [Fact]
    public void Employee_foreign_keys_match_ssdt_names()
    {
        AssertForeignKey<Employee>("FK_Employee_Branch", "BranchId");
        AssertForeignKey<Employee>("FK_Employee_Department", "DepartmentId");
        AssertForeignKey<Employee>("FK_Employee_Section", "SectionId");
        AssertForeignKey<Employee>("FK_Employee_Position", "PositionId");
        AssertForeignKey<Employee>("FK_Employee_JobGrade", "JobGradeId");
        AssertForeignKey<Employee>("FK_Employee_Employee_ManagerEmployeeId", "ManagerEmployeeId");
        AssertForeignKey<EmployeeBankAccount>("FK_EmployeeBankAccount_Employee", "EmployeeId");
        AssertForeignKey<Department>("FK_Department_Employee_HeadEmployeeId", "HeadEmployeeId");
    }

    [Fact]
    public void Employee_list_query_with_search_filter_and_sort_translates_to_sql_without_personal_columns()
    {
        var status = EmploymentStatus.OnLeave;
        var predicate = Apex.Hrms.Application.Predicates.And(
            Apex.Hrms.Application.Predicates.ContainsAny<Employee>("mia", e => e.EmployeeCode, e => e.FirstName + " " + e.LastName, e => e.WorkEmail),
            e => e.Status == status);

        var sql = context.Set<Employee>().AsNoTracking().Where(predicate)
            .OrderBy(Key<Employee>(e => e.FirstName + " " + e.LastName)).ThenBy(e => e.Id)
            .ToQueryString();

        Assert.Contains("[hr].[Employee]", sql, StringComparison.Ordinal);
        Assert.Contains("LIKE", sql, StringComparison.Ordinal);
        Assert.Contains("On Leave", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Sorting_by_a_boxed_key_translates_to_sql_with_a_stable_id_tiebreak()
    {
        var sql = context.Set<JobGrade>().AsNoTracking()
            .OrderByDescending(Key<JobGrade>(g => g.Level)).ThenBy(g => g.Id)
            .Skip(10).Take(5)
            .ToQueryString();

        Assert.Contains("ORDER BY [j].[Level] DESC, [j].[Id]", sql, StringComparison.Ordinal);
        Assert.Contains("[org].[JobGrade]", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Search_predicate_from_the_application_layer_translates_to_like()
    {
        var predicate = Apex.Hrms.Application.Predicates.ContainsAny<Company>("alp", c => c.Code, c => c.Name);

        var sql = context.Set<Company>().AsNoTracking().Where(predicate).ToQueryString();

        Assert.Contains("LIKE", sql, StringComparison.Ordinal);
        Assert.Contains("[org].[Company]", sql, StringComparison.Ordinal);
    }

    private static readonly DateOnly Today = new(2026, 11, 9);

    private static readonly string[] AddressProperties = ["Line1", "Line2", "City", "State", "PostalCode", "Country"];
    private static readonly string[] AddressColumns = ["AddressLine1", "AddressLine2", "AddressCity", "AddressState", "AddressPostalCode", "AddressCountry"];
    private static readonly string[] ContactProperties = ["Name", "Relationship", "Phone"];
    private static readonly string[] ContactColumns = ["EmergencyContactName", "EmergencyContactRelationship", "EmergencyContactPhone"];

    /// <summary>
    /// Attaches an existing row (no database), changes its parent through the domain method and lets the change
    /// tracker detect it. With alternate keys over the parent column EF threw here ("part of a key and so cannot
    /// be modified"), which surfaced as a 500 when a record moved to another parent.
    /// </summary>
    private void AssertParentChangeIsDetected<T>(T entity, Func<T, Result> change, string parentProperty)
        where T : AuditedEntity
    {
        typeof(Apex.Hrms.Domain.Common.Entity).GetProperty(nameof(Apex.Hrms.Domain.Common.Entity.Id))!.SetValue(entity, 5);
        var entry = context.Attach(entity);
        Assert.Equal(EntityState.Unchanged, entry.State);

        Assert.True(change(entity).IsSuccess);
        var exception = Record.Exception(() => context.ChangeTracker.DetectChanges());

        Assert.Null(exception);
        Assert.Equal(EntityState.Modified, entry.State);
        Assert.True(entry.Property(parentProperty).IsModified);
    }

    private static System.Linq.Expressions.Expression<Func<T, object>> Key<T>(System.Linq.Expressions.Expression<Func<T, object>> key) => key;

    private void AssertUnique<T>(string name, params string[] columns)
    {
        var index = Entity<T>().GetIndexes().SingleOrDefault(candidate => candidate.GetDatabaseName() == name);
        Assert.NotNull(index);
        Assert.True(index.IsUnique);
        Assert.Equal(columns, index.Properties.Select(property => property.Name));
    }

    private void AssertForeignKey<T>(string name, params string[] columns)
    {
        var key = Entity<T>().GetForeignKeys().SingleOrDefault(candidate => candidate.GetConstraintName() == name);
        Assert.NotNull(key);
        Assert.Equal(columns, key.Properties.Select(property => property.Name));
    }
}
