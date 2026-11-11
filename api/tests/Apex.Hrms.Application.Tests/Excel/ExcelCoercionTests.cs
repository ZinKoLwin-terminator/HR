using Apex.Hrms.Application.Excel;

namespace Apex.Hrms.Application.Tests.Excel;

public sealed class ExcelCoercionTests
{
    private static ExcelColumn Column(ExcelColumnType type = ExcelColumnType.Text, bool required = false) =>
        new() { Key = "k", Header = "Thing", Type = type, Required = required };

    private static RawCell Cell(object? value, bool formula = false) => new(value, formula);

    [Fact]
    public void Headers_ignore_case_spaces_punctuation_and_required_asterisks()
    {
        Assert.Equal("employeecode", ExcelCoercion.NormalizeHeader("Employee Code *"));
        Assert.Equal("stateregion", ExcelCoercion.NormalizeHeader("State / Region"));
    }

    [Fact]
    public void Header_match_maps_aliases_and_reports_unknown_missing_and_ambiguous_headers()
    {
        var columns = new[]
        {
            new ExcelColumn { Key = "code", Header = "Employee Code", Required = true, Aliases = ["Emp Code"] },
            new ExcelColumn { Key = "last", Header = "Last Name", Required = true },
        };

        var match = ExcelCoercion.MatchHeaders(["Emp Code", "Nickname", "employee code"], columns);

        Assert.Equal("code", match.Mapping["Emp Code"]);
        Assert.Null(match.Mapping["Nickname"]);
        Assert.Equal(["Nickname"], match.UnknownHeaders);
        Assert.Equal(["Emp Code", "employee code"], match.AmbiguousHeaders);
        Assert.Equal(["last"], match.MissingRequired);
    }

    [Fact]
    public void Mapping_problems_name_unknown_fields_double_mappings_and_unmapped_required_columns()
    {
        var columns = new[]
        {
            new ExcelColumn { Key = "a", Header = "A", Required = true },
            new ExcelColumn { Key = "b", Header = "B", Required = true },
        };

        var problems = ExcelCoercion.MappingProblems(
            new Dictionary<string, string?> { ["x"] = "a", ["y"] = "a", ["z"] = "nope", ["ignored"] = null },
            columns);

        Assert.Contains("\"x\" and \"y\" are both mapped to A.", problems);
        Assert.Contains("\"z\" is mapped to an unknown field.", problems);
        Assert.Contains("The required column \"B\" is not mapped.", problems);
    }

    [Fact]
    public void A_formula_is_refused_and_never_evaluated()
    {
        var result = ExcelCoercion.Coerce(Column(), Cell(null, formula: true));

        Assert.Equal("Formulas are not allowed. Replace the formula with its value.", result.Error);
    }

    [Fact]
    public void A_blank_required_cell_is_an_error_and_a_blank_optional_cell_is_not()
    {
        Assert.Equal("Thing is required.", ExcelCoercion.Coerce(Column(required: true), Cell("  ")).Error);
        Assert.Null(ExcelCoercion.Coerce(Column(), Cell(null)).Error);
    }

    [Fact]
    public void Text_rules_apply_length_pattern_and_upper_casing()
    {
        var column = Column() with { MaxLength = 5, MinLength = 2, Uppercase = true, Pattern = "^[A-Z0-9]+$", PatternMessage = "Thing is not a code." };

        Assert.Equal("AB1", ExcelCoercion.Coerce(column, Cell("ab1")).Value);
        Assert.Equal("Thing must be at most 5 characters (found 6).", ExcelCoercion.Coerce(column, Cell("abcdef")).Error);
        Assert.Equal("Thing must be at least 2 characters.", ExcelCoercion.Coerce(column, Cell("a")).Error);
        Assert.Equal("Thing is not a code.", ExcelCoercion.Coerce(column, Cell("a-b")).Error);
    }

    [Fact]
    public void Email_and_phone_are_checked()
    {
        Assert.Equal("Thing is not a valid email address.", ExcelCoercion.Coerce(Column(ExcelColumnType.Email), Cell("nobody")).Error);
        Assert.Null(ExcelCoercion.Coerce(Column(ExcelColumnType.Email), Cell("a@b.example")).Error);
        Assert.Equal("Thing is not a valid phone number.", ExcelCoercion.Coerce(Column(ExcelColumnType.Phone), Cell("abc")).Error);
        Assert.Null(ExcelCoercion.Coerce(Column(ExcelColumnType.Phone), Cell("+95 9 123 456 789")).Error);
    }

    [Fact]
    public void Dates_accept_excel_dates_iso_text_and_day_month_name_text_only()
    {
        var column = Column(ExcelColumnType.Date);

        Assert.Equal("2026-11-04", ExcelCoercion.Coerce(column, Cell(new DateTime(2026, 11, 4))).Value);
        Assert.Equal("1990-11-04", ExcelCoercion.Coerce(column, Cell("1990-11-04")).Value);
        Assert.Equal("1990-11-04", ExcelCoercion.Coerce(column, Cell("04 Nov 1990")).Value);
        Assert.Equal("2026-11-04", ExcelCoercion.Coerce(column, Cell(46330d)).Value);
        Assert.Equal("Thing is not a valid date.", ExcelCoercion.Coerce(column, Cell("04/11/2026")).Error);
        Assert.Equal("Thing is not a valid date.", ExcelCoercion.Coerce(column, Cell("2026-02-30")).Error);
    }

    [Fact]
    public void Times_numbers_and_booleans_are_coerced()
    {
        Assert.Equal("08:30", ExcelCoercion.Coerce(Column(ExcelColumnType.Time), Cell(TimeSpan.FromMinutes(510))).Value);
        Assert.Equal("Thing is not a valid time.", ExcelCoercion.Coerce(Column(ExcelColumnType.Time), Cell("25:00")).Error);
        Assert.Equal(12m, ExcelCoercion.Coerce(Column(ExcelColumnType.Integer), Cell(12d)).Value);
        Assert.Equal("Thing must be a whole number.", ExcelCoercion.Coerce(Column(ExcelColumnType.Integer), Cell(1.5d)).Error);
        Assert.Equal("Thing must be a number.", ExcelCoercion.Coerce(Column(ExcelColumnType.Decimal), Cell("abc")).Error);
        Assert.Equal("Thing allows at most 2 decimal places.", ExcelCoercion.Coerce(Column(ExcelColumnType.Decimal) with { Decimals = 2 }, Cell(1.234d)).Error);
        Assert.Equal("Thing must be at least 5.", ExcelCoercion.Coerce(Column(ExcelColumnType.Integer) with { Min = 5 }, Cell(1d)).Error);
        Assert.Equal(true, ExcelCoercion.Coerce(Column(ExcelColumnType.Boolean), Cell("Yes")).Value);
        Assert.Equal("Thing must be Yes or No.", ExcelCoercion.Coerce(Column(ExcelColumnType.Boolean), Cell("maybe")).Error);
    }

    [Fact]
    public void An_enum_value_is_matched_ignoring_case_and_a_near_miss_gets_a_suggestion()
    {
        var column = Column(ExcelColumnType.Enum) with { Allowed = ["Male", "Female", "Other"] };

        Assert.Equal("Female", ExcelCoercion.Coerce(column, Cell("female")).Value);
        var typo = ExcelCoercion.Coerce(column, Cell("Femal"));
        Assert.Equal("Thing \"Femal\" is not an allowed value.", typo.Error);
        Assert.Contains("Did you mean \"Female\"?", typo.Suggestion);
        Assert.Equal("Allowed values: Male, Female, Other.", ExcelCoercion.Coerce(column, Cell("Unknown")).Suggestion);
    }

    [Fact]
    public void Match_keys_are_case_insensitive_and_blank_keys_never_match()
    {
        var a = new ParsedRow { ["code"] = "EMP1" };
        var b = new ParsedRow { ["code"] = "emp1" };

        Assert.Equal(ExcelCoercion.MatchKeyOf(a, ["code"]), ExcelCoercion.MatchKeyOf(b, ["code"]));
        Assert.Null(ExcelCoercion.MatchKeyOf(new ParsedRow { ["code"] = " " }, ["code"]));
    }

    [Theory]
    [InlineData("=SUM(A1)", "'=SUM(A1)")]
    [InlineData("@cmd", "'@cmd")]
    [InlineData("+95 9 123 456", "+95 9 123 456")]
    [InlineData("-5", "-5")]
    [InlineData("plain", "plain")]
    public void Text_that_could_run_as_a_formula_is_neutralised_but_numbers_and_phones_are_not(string input, string expected) =>
        Assert.Equal(expected, ExcelCoercion.NeutralizeFormula(input));

    [Fact]
    public void Sensitive_values_are_masked_in_previews()
    {
        Assert.Equal("••••", ExcelCoercion.Shown(Column() with { Sensitive = true }, "secret"));
        Assert.Equal(string.Empty, ExcelCoercion.Shown(Column() with { Sensitive = true }, string.Empty));
        Assert.Equal("shown", ExcelCoercion.Shown(Column(), "shown"));
    }

    [Fact]
    public void File_checks_refuse_other_formats_empty_oversized_renamed_macro_and_linked_files()
    {
        var limits = new ExcelLimits { MaxFileBytes = 2000 };
        var good = ExcelTestFiles.Package();

        Assert.True(ExcelFileChecks.Check("a.xlsx", good, limits).IsSuccess);
        Assert.Contains(".xlsm", ExcelFileChecks.Check("a.xlsm", good, limits).Error.Message);
        Assert.Contains(".csv", ExcelFileChecks.Check("a.csv", good, limits).Error.Message);
        Assert.Equal("The file is empty.", ExcelFileChecks.Check("a.xlsx", [], limits).Error.Message);
        Assert.Equal("The file is too large. The maximum size is 0 MB.", ExcelFileChecks.Check("a.xlsx", new byte[2001], limits).Error.Message);
        Assert.Contains("not a valid Excel workbook", ExcelFileChecks.Check("a.xlsx", [.. "just text, not a zip"u8], limits).Error.Message);
        Assert.Contains("macros", ExcelFileChecks.Check("a.xlsx", ExcelTestFiles.Package("xl/workbook.xml", "xl/vbaProject.bin"), limits).Error.Message);
        Assert.Contains("links to other workbooks", ExcelFileChecks.Check("a.xlsx", ExcelTestFiles.Package("xl/workbook.xml", "xl/externalLinks/externalLink1.xml"), limits).Error.Message);
        Assert.Contains("not a valid Excel workbook", ExcelFileChecks.Check("a.xlsx", ExcelTestFiles.Package("other.xml"), limits).Error.Message);
    }
}
