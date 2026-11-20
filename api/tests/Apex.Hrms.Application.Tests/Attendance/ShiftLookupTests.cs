using Apex.Hrms.Application.Attendance.Shifts;
using Apex.Hrms.Application.Tests.Fakes;
using static Apex.Hrms.Application.Tests.Fakes.AttendanceFixtures;

namespace Apex.Hrms.Application.Tests.Attendance;

/// <summary>
/// The shift lookup: every shift, active and inactive, by name then id, with the times and work days of the shift DTO and
/// nothing else (no break or grace minutes, colour or row version).
/// </summary>
public sealed class ShiftLookupTests : IDisposable
{
    private readonly TestHost host = new();

    public void Dispose() => host.Dispose();

    private IShiftLookupHandler Lookup => host.Get<IShiftLookupHandler>();

    [Fact]
    public async Task No_shifts_give_an_empty_list()
    {
        var shifts = ResultAssert.Succeeded(await Lookup.HandleAsync(default));

        Assert.Empty(shifts);
    }

    [Fact]
    public async Task Every_shift_is_listed_with_inactive_ones_by_name_then_id_in_the_shift_dto_formats()
    {
        var general = await host.CreateShiftAsync();
        var night = await host.CreateShiftAsync(NewShift("NGT", "Night Shift", At(22, 0), At(6, 0), workDays: [6, 0, 3], night: true, status: "Inactive"));
        var generalTwo = await host.CreateShiftAsync(NewShift("GN2", "General Shift", At(8, 30), At(17, 30), workDays: [1, 2, 3, 4, 5, 6]));
        var early = await host.CreateShiftAsync(NewShift("EAR", "Early Shift", At(6, 0), At(14, 0)));

        var shifts = ResultAssert.Succeeded(await Lookup.HandleAsync(default));

        Assert.Equal(
            [early.Id, general.Id, generalTwo.Id, night.Id],
            shifts.Select(shift => shift.Id));
        Assert.True(general.Id < generalTwo.Id);

        var nightRow = shifts.Single(shift => shift.Id == night.Id);
        Assert.Equal("NGT", nightRow.Code);
        Assert.Equal("Night Shift", nightRow.Name);
        Assert.Equal(At(22, 0), nightRow.StartTime);
        Assert.Equal(At(6, 0), nightRow.EndTime);
        Assert.Equal([0, 3, 6], nightRow.WorkDays);
        Assert.True(nightRow.IsNightShift);
        Assert.Equal("Inactive", nightRow.Status);

        // The same values as the full shift DTO, field for field.
        foreach (var full in new[] { general, night, generalTwo, early })
        {
            var row = shifts.Single(shift => shift.Id == full.Id);
            Assert.Equal(
                (full.Code, full.Name, full.StartTime, full.EndTime, full.IsNightShift, full.Status),
                (row.Code, row.Name, row.StartTime, row.EndTime, row.IsNightShift, row.Status));
            Assert.Equal(full.WorkDays, row.WorkDays);
        }
    }

    [Fact]
    public void The_lookup_row_carries_only_the_contract_fields()
    {
        Assert.Equal(
            ["Id", "Code", "Name", "StartTime", "EndTime", "WorkDays", "IsNightShift", "Status"],
            typeof(ShiftLookupDto).GetProperties().Select(property => property.Name));
    }
}
