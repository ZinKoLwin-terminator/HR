using Apex.Hrms.Domain.Attendance;
using Apex.Hrms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Apex.Hrms.Infrastructure.Tests;

/// <summary>Checks the attendance part of the EF model against the att tables of the SSDT project. No connection is opened.</summary>
public sealed class AttendanceSchemaMappingTests : IDisposable
{
    private readonly HrmsDbContext context = new(
        new DbContextOptionsBuilder<HrmsDbContext>()
            .UseSqlServer("Server=.;Database=HR;Integrated Security=True;Encrypt=True;TrustServerCertificate=True")
            .Options);

    public void Dispose() => context.Dispose();

    private IEntityType Entity<T>() => context.Model.FindEntityType(typeof(T))!;

    [Theory]
    [InlineData(typeof(Shift), "Shift")]
    [InlineData(typeof(ShiftWorkDay), "ShiftWorkDay")]
    [InlineData(typeof(AttendanceRecord), "AttendanceRecord")]
    [InlineData(typeof(AttendanceAdjustment), "AttendanceAdjustment")]
    [InlineData(typeof(OvertimeRecord), "OvertimeRecord")]
    [InlineData(typeof(Holiday), "Holiday")]
    public void Tables_map_to_the_att_schema(Type entity, string table)
    {
        var type = context.Model.FindEntityType(entity)!;

        Assert.Equal("att", type.GetSchema());
        Assert.Equal(table, type.GetTableName());
    }

    [Fact]
    public void Audited_attendance_tables_have_a_rowversion_token_and_identity_keys()
    {
        foreach (var type in new[] { Entity<Shift>(), Entity<AttendanceRecord>(), Entity<AttendanceAdjustment>(), Entity<OvertimeRecord>(), Entity<Holiday>() })
        {
            var rowVersion = type.FindProperty("RowVersion")!;
            Assert.True(rowVersion.IsConcurrencyToken, type.Name);
            Assert.Equal(ValueGenerated.OnAddOrUpdate, rowVersion.ValueGenerated);
            Assert.Equal(ValueGenerated.OnAdd, type.FindProperty("Id")!.ValueGenerated);
        }
    }

    [Fact]
    public void The_record_key_is_a_bigint_in_the_table_and_an_int_in_the_entity()
    {
        var id = Entity<AttendanceRecord>().FindProperty("Id")!;

        Assert.Equal("bigint", id.GetColumnType());
        Assert.Equal(typeof(int), id.ClrType);
        Assert.Equal(typeof(long), id.GetProviderClrType());
    }

    [Fact]
    public void Times_minutes_and_texts_use_the_ssdt_types()
    {
        var record = Entity<AttendanceRecord>();
        Assert.Equal("time(0)", record.FindProperty("CheckIn")!.GetColumnType());
        Assert.Equal("time(0)", record.FindProperty("CheckOut")!.GetColumnType());
        Assert.Equal("date", record.FindProperty("Date")!.GetColumnType());
        Assert.Equal("smallint", record.FindProperty("WorkedMinutes")!.GetColumnType());
        Assert.Equal("smallint", record.FindProperty("OvertimeMinutes")!.GetColumnType());
        Assert.Equal("varchar(30)", record.FindProperty("Status")!.GetColumnType());
        Assert.Equal("varchar(30)", record.FindProperty("Source")!.GetColumnType());
        Assert.Equal(250, record.FindProperty("Remarks")!.GetMaxLength());

        var shift = Entity<Shift>();
        Assert.Equal("time(0)", shift.FindProperty("StartTime")!.GetColumnType());
        Assert.Equal("smallint", shift.FindProperty("BreakMinutes")!.GetColumnType());
        Assert.Equal("varchar(7)", shift.FindProperty("Color")!.GetColumnType());
        Assert.Equal(20, shift.FindProperty("Code")!.GetMaxLength());
        Assert.Equal(60, shift.FindProperty("Name")!.GetMaxLength());
        Assert.Equal("tinyint", Entity<ShiftWorkDay>().FindProperty("DayOfWeek")!.GetColumnType());

        var adjustment = Entity<AttendanceAdjustment>();
        Assert.Equal("varchar(30)", adjustment.FindProperty("RequestedStatus")!.GetColumnType());
        Assert.Equal(250, adjustment.FindProperty("Reason")!.GetMaxLength());
        Assert.Equal(130, adjustment.FindProperty("DecidedByName")!.GetMaxLength());

        var overtime = Entity<OvertimeRecord>();
        Assert.Equal("smallint", overtime.FindProperty("Minutes")!.GetColumnType());
        Assert.Equal(120, overtime.FindProperty("Reason")!.GetMaxLength());
    }

    [Fact]
    public void The_unique_indexes_of_the_database_are_mapped()
    {
        Assert.Contains(Entity<Shift>().GetIndexes(), index => index.IsUnique && index.GetDatabaseName() == "UQ_Shift_Code");
        Assert.Contains(Entity<AttendanceRecord>().GetIndexes(), index => index.IsUnique && index.Properties.Select(p => p.Name).SequenceEqual(["EmployeeId", "Date"]));
        Assert.Contains(Entity<OvertimeRecord>().GetIndexes(), index => index.IsUnique && index.Properties.Select(p => p.Name).SequenceEqual(["EmployeeId", "Date"]));
        var pending = Assert.Single(Entity<AttendanceAdjustment>().GetIndexes(), index => index.IsUnique);
        Assert.Equal("[Status] = 'Pending'", pending.GetFilter());
    }
}
