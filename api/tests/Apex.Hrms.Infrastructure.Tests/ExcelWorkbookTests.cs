using Apex.Hrms.Application.Excel;
using Apex.Hrms.Domain.Excel;
using Apex.Hrms.Infrastructure.Excel;
using ClosedXML.Excel;

namespace Apex.Hrms.Infrastructure.Tests;

/// <summary>The ClosedXML reader and writer and the local upload folder, with real workbooks in memory.</summary>
public sealed class ExcelWorkbookTests : IDisposable
{
    private static readonly ExcelLimits Limits = new() { MaxRows = 3, MaxSheets = 3 };
    private readonly ClosedXmlWorkbookReader reader = new();
    private readonly ClosedXmlWorkbookWriter writer = new();
    private readonly string folder = Path.Combine(Path.GetTempPath(), "hrms-excel-test-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    private static ExcelModuleInfo Info() => new(
        "things",
        "Things",
        "Things",
        "Test things.",
        [
            new ExcelColumn { Key = "code", Header = "Thing Code", Required = true, Description = "Code.", Example = "T1" },
            new ExcelColumn { Key = "kind", Header = "Kind", Type = ExcelColumnType.Enum, Allowed = ["A", "B"], Description = "Kind.", Example = "A" },
            new ExcelColumn { Key = "since", Header = "Since", Type = ExcelColumnType.Date, Description = "Date.", Example = "2026-01-05" },
        ],
        null,
        ["code"],
        "Thing Code",
        true,
        true,
        null);

    private static byte[] Workbook(Action<IXLWorkbook> build)
    {
        using var workbook = new XLWorkbook();
        build(workbook);
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    [Fact]
    public void The_template_has_the_headers_required_markers_field_guide_and_hidden_lists_of_the_module()
    {
        var bytes = writer.BuildTemplate(Info(), [], new ExcelLimits());

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        Assert.Equal(["Instructions", "Things", "Field guide", "Lists"], workbook.Worksheets.Select(s => s.Name));
        var data = workbook.Worksheet("Things");
        Assert.Equal(["Thing Code", "Kind", "Since"], Enumerable.Range(1, 3).Select(c => data.Cell(1, c).GetString()));

        // Required headers are dark blue, optional ones grey (the marker of the mock).
        Assert.Equal("FF1E4DB7", data.Cell(1, 1).Style.Fill.BackgroundColor.Color.Name.ToUpperInvariant());
        Assert.Equal("FF5B6B8A", data.Cell(1, 2).Style.Fill.BackgroundColor.Color.Name.ToUpperInvariant());
        var guide = workbook.Worksheet("Field guide");
        Assert.Equal("Yes", guide.Cell(2, 2).GetString());
        Assert.Equal("No", guide.Cell(3, 2).GetString());
        Assert.Equal("T1", guide.Cell(2, 6).GetString());
        Assert.Equal(XLWorksheetVisibility.Hidden, workbook.Worksheet("Lists").Visibility);
        Assert.NotEmpty(data.DataValidations);
    }

    [Fact]
    public void A_template_can_be_read_back_as_an_upload_with_no_data_rows()
    {
        var read = reader.Read(writer.BuildTemplate(Info(), [], new ExcelLimits()), "Things", new ExcelLimits());

        Assert.True(read.IsSuccess);
        Assert.Equal(["Thing Code", "Kind", "Since"], read.Value.Headers);
        Assert.Empty(read.Value.Rows);
        Assert.Equal(1, read.Value.SheetCount);
    }

    [Fact]
    public void The_reader_returns_typed_cells_with_worksheet_row_numbers_and_skips_blank_rows()
    {
        var bytes = Workbook(workbook =>
        {
            var sheet = workbook.AddWorksheet("Things");
            sheet.Cell(1, 1).Value = "Thing Code";
            sheet.Cell(1, 2).Value = "Since";
            sheet.Cell(2, 1).Value = "T1";
            sheet.Cell(2, 2).Value = new DateTime(2026, 1, 5);
            sheet.Cell(4, 1).Value = "T2";
            sheet.Cell(4, 2).Value = 12.5;
        });

        var sheet = reader.Read(bytes, "Things", Limits).Value;

        Assert.Equal([2, 4], sheet.Rows.Select(r => r.RowNumber));
        Assert.Equal("T1", sheet.Rows[0].Cells[0].Value);
        Assert.Equal(new DateTime(2026, 1, 5), sheet.Rows[0].Cells[1].Value);
        Assert.Equal(12.5, sheet.Rows[1].Cells[1].Value);
    }

    [Fact]
    public void The_reader_flags_formulas_without_calculating_them()
    {
        var bytes = Workbook(workbook =>
        {
            var sheet = workbook.AddWorksheet("Things");
            sheet.Cell(1, 1).Value = "Thing Code";
            sheet.Cell(2, 1).FormulaA1 = "=1+1";
        });

        var cell = reader.Read(bytes, "Things", Limits).Value.Rows.Single().Cells.Single();

        Assert.True(cell.Formula);
        Assert.Null(cell.Value);
    }

    [Fact]
    public void The_reader_uses_the_named_sheet_or_the_first_visible_data_sheet_and_ignores_guide_sheets()
    {
        var bytes = Workbook(workbook =>
        {
            workbook.AddWorksheet("Instructions").Cell(1, 1).Value = "Guide";
            var other = workbook.AddWorksheet("Whatever");
            other.Cell(1, 1).Value = "Thing Code";
            other.Cell(2, 1).Value = "T1";
        });

        Assert.Equal("Whatever", reader.Read(bytes, "Things", Limits).Value.Name);
    }

    [Fact]
    public void The_reader_refuses_too_many_rows_too_many_sheets_duplicate_headers_and_unreadable_files()
    {
        var rows = Workbook(workbook =>
        {
            var sheet = workbook.AddWorksheet("Things");
            sheet.Cell(1, 1).Value = "Thing Code";
            for (var row = 2; row <= 5; row++)
            {
                sheet.Cell(row, 1).Value = "T" + row;
            }
        });
        var sheets = Workbook(workbook =>
        {
            for (var i = 0; i < 4; i++)
            {
                workbook.AddWorksheet("S" + i).Cell(1, 1).Value = "x";
            }
        });
        var duplicate = Workbook(workbook =>
        {
            var sheet = workbook.AddWorksheet("Things");
            sheet.Cell(1, 1).Value = "Thing Code";
            sheet.Cell(1, 2).Value = "thing code";
            sheet.Cell(2, 1).Value = "x";
        });

        Assert.Equal("The worksheet has more than 3 data rows. Split the file into smaller files.", reader.Read(rows, "Things", Limits).Error.Message);
        Assert.Equal("The workbook has 4 worksheets; at most 3 are allowed.", reader.Read(sheets, "Things", Limits).Error.Message);
        Assert.Equal("The column header \"thing code\" appears more than once.", reader.Read(duplicate, "Things", Limits).Error.Message);
        Assert.StartsWith("The workbook is corrupted", reader.Read([1, 2, 3], "Things", Limits).Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void An_export_keeps_text_as_text_writes_real_dates_and_neutralises_formulas()
    {
        var info = Info();
        var rows = new IReadOnlyDictionary<string, object?>[]
        {
            new Dictionary<string, object?> { ["code"] = "00123", ["kind"] = "=cmd()", ["since"] = new DateOnly(2026, 1, 5) },
            new Dictionary<string, object?> { ["code"] = "T2", ["kind"] = null, ["since"] = null },
        };

        var bytes = writer.BuildExport("Things export", new ExportSheet("Things", info.Columns, rows), []);

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var sheet = workbook.Worksheet("Things");
        Assert.Equal("00123", sheet.Cell(2, 1).GetString());
        Assert.Equal(XLDataType.Text, sheet.Cell(2, 1).DataType);
        Assert.Equal("=cmd()", sheet.Cell(2, 2).GetString());
        Assert.Equal(XLDataType.Text, sheet.Cell(2, 2).DataType);
        Assert.False(sheet.Cell(2, 2).HasFormula);
        Assert.Equal(new DateTime(2026, 1, 5), sheet.Cell(2, 3).GetDateTime());
        Assert.True(sheet.Cell(3, 2).IsEmpty());
    }

    [Fact]
    public void The_error_report_has_one_line_per_issue_with_the_row_field_original_value_and_message()
    {
        var bytes = writer.BuildErrorReport(
            "Things",
            [
                new ImportIssueRow(3, "Thing Code", "T1", "Thing Code is required.", "Fill it in.", ImportSeverity.Error),
                new ImportIssueRow(7, "Kind", "=BAD()", "Not allowed.", null, ImportSeverity.Warning),
            ]);

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var sheet = workbook.Worksheet("Validation report");
        Assert.Equal(["Row", "Field", "Original value", "Severity", "Error", "Suggested correction"], Enumerable.Range(1, 6).Select(c => sheet.Cell(1, c).GetString()));
        Assert.Equal(3, sheet.Cell(2, 1).GetValue<int>());
        Assert.Equal("Thing Code is required.", sheet.Cell(2, 5).GetString());
        Assert.Equal("Fill it in.", sheet.Cell(2, 6).GetString());
        Assert.Equal("Warning", sheet.Cell(3, 4).GetString());
        Assert.Equal("=BAD()", sheet.Cell(3, 3).GetString());
        Assert.False(sheet.Cell(3, 3).HasFormula);
        Assert.Equal(XLDataType.Text, sheet.Cell(3, 3).DataType);
    }

    [Fact]
    public async Task The_upload_folder_returns_a_file_until_it_expires_then_removes_it()
    {
        var clock = new MutableTime(new DateTimeOffset(2026, 11, 9, 8, 0, 0, TimeSpan.Zero));
        var storage = new LocalFileStorage(folder, clock);
        var key = Guid.NewGuid().ToString("N");

        await storage.SaveAsync(key, [1, 2, 3], new Dictionary<string, string> { ["module"] = "things" }, TimeSpan.FromMinutes(30), default);
        var fresh = await storage.ReadAsync(key, default);
        clock.Advance(TimeSpan.FromMinutes(31));
        var expired = await storage.ReadAsync(key, default);

        Assert.Equal([1, 2, 3], fresh!.Content);
        Assert.Equal("things", fresh.Metadata["module"]);
        Assert.Null(expired);
        Assert.Empty(Directory.GetFiles(folder));
    }

    [Fact]
    public async Task Purge_removes_expired_files_and_keys_cannot_leave_the_folder()
    {
        var clock = new MutableTime(new DateTimeOffset(2026, 11, 9, 8, 0, 0, TimeSpan.Zero));
        var storage = new LocalFileStorage(folder, clock);
        var old = Guid.NewGuid().ToString("N");
        var recent = Guid.NewGuid().ToString("N");
        await storage.SaveAsync(old, [1], new Dictionary<string, string>(), TimeSpan.FromMinutes(1), default);
        await storage.SaveAsync(recent, [2], new Dictionary<string, string>(), TimeSpan.FromHours(1), default);

        clock.Advance(TimeSpan.FromMinutes(5));
        await storage.PurgeExpiredAsync(default);

        Assert.Null(await storage.ReadAsync(old, default));
        Assert.NotNull(await storage.ReadAsync(recent, default));
        await Assert.ThrowsAsync<ArgumentException>(() => storage.ReadAsync("..\\..\\secret", default));
    }

    private sealed class MutableTime(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset now = start;

        public void Advance(TimeSpan by) => now += by;

        public override DateTimeOffset GetUtcNow() => now;
    }
}
