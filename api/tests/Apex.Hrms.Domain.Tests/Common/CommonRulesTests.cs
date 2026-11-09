using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Domain.Tests.Common;

public sealed class CommonRulesTests
{
    [Theory]
    [InlineData("HR", true)]
    [InlineData("HR-TA", true)]
    [InlineData("A1-B2", true)]
    [InlineData("9X", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("hr", false)]
    [InlineData("-HR", false)]
    [InlineData("HR TA", false)]
    [InlineData("HR_TA", false)]
    public void RecordCodeRules_accepts_only_upper_case_letters_digits_and_dashes(string? code, bool expected) =>
        Assert.Equal(expected, RecordCodeRules.IsValid(code));

    [Theory]
    [InlineData("Active", true)]
    [InlineData("Inactive", true)]
    [InlineData("active", false)]
    [InlineData("All", false)]
    [InlineData("1", false)]
    [InlineData(null, false)]
    public void RecordStatusText_parses_exact_names_only(string? text, bool expected) =>
        Assert.Equal(expected, RecordStatusText.TryParse(text, out _));

    [Fact]
    public void Violations_with_one_entry_use_its_summary_as_the_message()
    {
        var result = new Violations().Add("name", "Name is required.", "The name is missing.").ToResult();

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal("The name is missing.", result.Error.Message);
        Assert.Equal(["Name is required."], result.Error.FieldErrors!["name"]);
    }

    [Fact]
    public void Violations_with_several_entries_use_a_generic_message_and_group_by_field()
    {
        var result = new Violations().Add("a", "one").Add("a", "two").Add("b", "three").ToResult();

        Assert.Equal("One or more validation errors occurred.", result.Error.Message);
        Assert.Equal(["one", "two"], result.Error.FieldErrors!["a"]);
        Assert.Equal(["three"], result.Error.FieldErrors["b"]);
    }

    [Fact]
    public void Violations_without_entries_succeed() => Assert.True(new Violations().ToResult().IsSuccess);

    [Fact]
    public void Result_exposes_value_only_on_success_and_error_only_on_failure()
    {
        var success = Result.Success(5);
        var failure = Result.Failure<int>(AppError.NotFound("missing"));

        Assert.Equal(5, success.Value);
        Assert.Throws<InvalidOperationException>(() => success.Error);
        Assert.Throws<InvalidOperationException>(() => failure.Value);
        Assert.Equal(ErrorType.NotFound, failure.Error.Type);
    }
}
