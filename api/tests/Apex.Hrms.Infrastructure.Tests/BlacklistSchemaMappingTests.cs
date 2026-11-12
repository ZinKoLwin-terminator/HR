using Apex.Hrms.Domain.Employees;
using Apex.Hrms.Domain.Recruitment;
using Apex.Hrms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Apex.Hrms.Infrastructure.Tests;

/// <summary>Checks the do-not-rehire mapping against hr.EmployeeBlacklist and the new rec columns of the SSDT project. No connection is opened.</summary>
public sealed class BlacklistSchemaMappingTests : IDisposable
{
    private readonly HrmsDbContext context = new(
        new DbContextOptionsBuilder<HrmsDbContext>()
            .UseSqlServer("Server=.;Database=HR;Integrated Security=True;Encrypt=True;TrustServerCertificate=True")
            .Options);

    public void Dispose() => context.Dispose();

    private IEntityType Entity<T>() => context.Model.FindEntityType(typeof(T))!;

    [Fact]
    public void The_blacklist_entry_maps_to_hr_EmployeeBlacklist_with_audit_columns_and_a_rowversion()
    {
        var entry = Entity<EmployeeBlacklistEntry>();

        Assert.Equal("hr", entry.GetSchema());
        Assert.Equal("EmployeeBlacklist", entry.GetTableName());
        Assert.True(entry.FindProperty("RowVersion")!.IsConcurrencyToken);
        Assert.Equal(ValueGenerated.OnAddOrUpdate, entry.FindProperty("RowVersion")!.ValueGenerated);
        Assert.Equal(ValueGenerated.OnAdd, entry.FindProperty("Id")!.ValueGenerated);
        Assert.NotNull(entry.FindProperty("CreatedByUserId"));
        Assert.NotNull(entry.FindProperty("ModifiedAtUtc"));
    }

    [Fact]
    public void The_columns_use_the_ssdt_types_and_lengths()
    {
        var entry = Entity<EmployeeBlacklistEntry>();

        Assert.Equal(500, entry.FindProperty("Reason")!.GetMaxLength());
        Assert.Equal(500, entry.FindProperty("LiftReason")!.GetMaxLength());
        Assert.Equal(130, entry.FindProperty("BlacklistedByName")!.GetMaxLength());
        Assert.Equal(130, entry.FindProperty("LiftedByName")!.GetMaxLength());
        Assert.Equal("date", entry.FindProperty("BlacklistedOn")!.GetColumnType());
        Assert.Equal("date", entry.FindProperty("LiftedOn")!.GetColumnType());
        Assert.True(entry.FindProperty("LiftedOn")!.IsNullable);
        Assert.Equal(40, Entity<Candidate>().FindProperty("NationalId")!.GetMaxLength());
        Assert.True(Entity<Candidate>().FindProperty("NationalId")!.IsNullable);
        Assert.Equal(300, Entity<JobApplication>().FindProperty("BlacklistOverrideReason")!.GetMaxLength());
        Assert.True(Entity<JobApplication>().FindProperty("BlacklistOverrideByUserId")!.IsNullable);
    }

    [Fact]
    public void The_employee_key_does_not_cascade_so_history_blocks_deleting_the_employee()
    {
        var key = Assert.Single(Entity<EmployeeBlacklistEntry>().GetForeignKeys());

        Assert.Equal(DeleteBehavior.NoAction, key.DeleteBehavior);
        Assert.Equal("FK_EmployeeBlacklist_Employee", key.GetConstraintName());
    }

    [Fact]
    public void A_second_active_entry_is_translated_to_the_already_listed_conflict()
    {
        var error = SqlConflicts.Translate(
            2601,
            "Cannot insert duplicate key row in object 'hr.EmployeeBlacklist' with unique index 'UQ_EmployeeBlacklist_ActiveEmployee'. The duplicate key value is (7).")!;

        Assert.Equal(Apex.Hrms.Domain.Common.ErrorType.Conflict, error.Type);
        Assert.Equal("This employee is already on the do-not-rehire list.", error.Message);
    }
}
