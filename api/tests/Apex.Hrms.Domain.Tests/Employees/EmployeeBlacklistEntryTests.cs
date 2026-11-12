using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Employees;

namespace Apex.Hrms.Domain.Tests.Employees;

public sealed class EmployeeBlacklistEntryTests
{
    private static readonly DateOnly Today = new(2026, 11, 9);

    private static EmployeeBlacklistEntry NewEntry(string reason = "Left during an investigation.") =>
        EmployeeBlacklistEntry.Create(7, reason, Today, 3, "Hana Administrator").Value;

    [Fact]
    public void A_new_entry_is_active_and_keeps_the_trimmed_reason_and_who_made_it()
    {
        var entry = NewEntry("  Reason text.  ");

        Assert.True(entry.IsActive);
        Assert.Equal(7, entry.EmployeeId);
        Assert.Equal("Reason text.", entry.Reason);
        Assert.Equal(Today, entry.BlacklistedOn);
        Assert.Equal(3, entry.BlacklistedByUserId);
        Assert.Equal("Hana Administrator", entry.BlacklistedByName);
        Assert.Null(entry.LiftedOn);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_reason_is_refused_on_the_reason_field(string? reason)
    {
        var result = EmployeeBlacklistEntry.Create(7, reason, Today, 3, "Hana");

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.True(result.Error.FieldErrors!.ContainsKey("reason"));
    }

    [Fact]
    public void The_reason_may_have_five_hundred_characters_but_not_more()
    {
        Assert.True(EmployeeBlacklistEntry.Create(7, new string('x', 500), Today, 3, "Hana").IsSuccess);
        Assert.True(EmployeeBlacklistEntry.Create(7, new string('x', 501), Today, 3, "Hana").IsFailure);
    }

    [Fact]
    public void Lifting_sets_all_four_lift_fields_and_makes_the_entry_inactive()
    {
        var entry = NewEntry();

        var lifted = entry.Lift("  Reviewed.  ", Today.AddDays(2), 4, "Ira Reviewer");

        Assert.True(lifted.IsSuccess);
        Assert.False(entry.IsActive);
        Assert.Equal(Today.AddDays(2), entry.LiftedOn);
        Assert.Equal(4, entry.LiftedByUserId);
        Assert.Equal("Ira Reviewer", entry.LiftedByName);
        Assert.Equal("Reviewed.", entry.LiftReason);
    }

    [Fact]
    public void Lifting_needs_a_reason_and_a_lifted_entry_cannot_be_lifted_again()
    {
        var entry = NewEntry();

        Assert.Equal(ErrorType.Validation, entry.Lift(" ", Today, 4, "Ira").Error.Type);
        Assert.True(entry.IsActive);

        Assert.True(entry.Lift("Done.", Today, 4, "Ira").IsSuccess);
        Assert.Equal(ErrorType.Conflict, entry.Lift("Again.", Today, 4, "Ira").Error.Type);
    }

    [Fact]
    public void The_lift_date_is_never_before_the_listing_date()
    {
        var entry = NewEntry();

        entry.Lift("Clock moved back.", Today.AddDays(-1), 4, "Ira");

        Assert.Equal(Today, entry.LiftedOn);
    }

    [Theory]
    [InlineData("  Ada@Example.Test ", "ADA@EXAMPLE.TEST")]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Emails_and_national_ids_ignore_case_and_surrounding_spaces_and_blanks_never_match(string? value, string? expected)
    {
        Assert.Equal(expected, BlacklistMatching.NormalizeEmail(value));
        Assert.Equal(expected, BlacklistMatching.NormalizeNationalId(value));
    }

    [Theory]
    [InlineData("+95 (9) 555-0101", "959555 0101")]
    [InlineData("095 955 50101", "09595550101")]
    [InlineData("n/a", null)]
    [InlineData(" ", null)]
    [InlineData(null, null)]
    public void Phones_compare_their_digits_only(string? value, string? expectedWithSpaces)
    {
        Assert.Equal(expectedWithSpaces?.Replace(" ", string.Empty, StringComparison.Ordinal), BlacklistMatching.NormalizePhone(value));
    }
}
