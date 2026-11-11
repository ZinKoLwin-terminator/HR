using System.Globalization;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Excel;
using Apex.Hrms.Domain.Common;
using ClosedXML.Excel;

namespace Apex.Hrms.Infrastructure.Excel;

/// <summary>
/// Reads uploaded workbooks defensively (formulas are flagged, never calculated; limits on sheets and rows) with ClosedXML
/// (MIT licence). Ported from <c>ExcelFileService.read</c> of the frontend mock.
/// </summary>
internal sealed class ClosedXmlWorkbookReader : IWorkbookReader
{
    /// <summary>Worksheets of our own templates that are never the data sheet.</summary>
    private static readonly string[] GuideSheets = ["instructions", "field guide", "lists", "organization codes", "grade and shift codes"];

    public Result<ParsedSheet> Read(byte[] content, string preferredSheet, ExcelLimits limits)
    {
        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(new MemoryStream(content, writable: false));
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return Fail("The workbook is corrupted or cannot be read. Open it in Excel, save it again as .xlsx and retry.");
        }

        using (workbook)
        {
            if (workbook.Worksheets.Count == 0)
            {
                return Fail("The workbook is empty: it contains no worksheets.");
            }

            // The sheets of our own template (instructions, field guide, code lists) do not count: the mock counted them too, which
            // made its own six-sheet employee template exceed its limit of five.
            var counted = workbook.Worksheets.Count(s => !GuideSheets.Contains(s.Name.Trim().ToLowerInvariant()));
            if (counted > limits.MaxSheets)
            {
                return Fail($"The workbook has {counted} worksheets; at most {limits.MaxSheets} are allowed.");
            }

            var wanted = preferredSheet.Trim().ToLowerInvariant();
            var sheet = workbook.Worksheets.FirstOrDefault(s => string.Equals(s.Name.Trim(), wanted, StringComparison.OrdinalIgnoreCase))
                ?? workbook.Worksheets.FirstOrDefault(s => s.Visibility == XLWorksheetVisibility.Visible
                    && !GuideSheets.Contains(s.Name.Trim().ToLowerInvariant()));
            return sheet is null
                ? Fail($"The worksheet \"{preferredSheet}\" was not found. Use the downloaded template.")
                : Parse(sheet, counted, limits);
        }
    }

    private static Result<ParsedSheet> Parse(IXLWorksheet sheet, int sheetCount, ExcelLimits limits)
    {
        var lastRow = sheet.LastRowUsed(XLCellsUsedOptions.Contents)?.RowNumber() ?? 0;
        if (lastRow == 0)
        {
            return Fail($"The worksheet \"{sheet.Name}\" is empty.");
        }

        var lastColumn = sheet.LastColumnUsed(XLCellsUsedOptions.Contents)?.ColumnNumber() ?? 0;
        var headers = new List<string>();
        for (var column = 1; column <= lastColumn; column++)
        {
            var raw = ToRaw(sheet.Cell(1, column));
            headers.Add(raw.Value is null ? string.Empty : ExcelCoercion.CellText(raw));
        }

        while (headers.Count > 0 && headers[^1].Length == 0)
        {
            headers.RemoveAt(headers.Count - 1);
        }

        if (headers.Count == 0)
        {
            return Fail("The first row must contain the column headers.");
        }

        var seen = new HashSet<string>();
        foreach (var header in headers.Where(h => h.Length > 0))
        {
            if (!seen.Add(ExcelCoercion.NormalizeHeader(header)))
            {
                return Fail($"The column header \"{header}\" appears more than once.");
            }
        }

        var rows = new List<ParsedSheetRow>();
        for (var rowNumber = 2; rowNumber <= lastRow; rowNumber++)
        {
            var cells = new List<RawCell>(headers.Count);
            for (var column = 1; column <= headers.Count; column++)
            {
                cells.Add(ToRaw(sheet.Cell(rowNumber, column)));
            }

            if (cells.All(c => !c.Formula && (c.Value is null || (c.Value is string text && text.Trim().Length == 0))))
            {
                continue;
            }

            if (rows.Count >= limits.MaxRows)
            {
                return Fail($"The worksheet has more than {limits.MaxRows} data rows. Split the file into smaller files.");
            }

            rows.Add(new ParsedSheetRow(rowNumber, cells));
        }

        return Result.Success(new ParsedSheet(sheet.Name, sheetCount, headers, rows));
    }

    /// <summary>Reads a cell as plain data. Formulas are flagged, never calculated.</summary>
    private static RawCell ToRaw(IXLCell cell)
    {
        if (cell.HasFormula)
        {
            return new RawCell(null, true);
        }

        var value = cell.Value;
        if (value.IsBlank)
        {
            return new RawCell(null, false);
        }

        object? plain = value.Type switch
        {
            XLDataType.Text => value.GetText(),
            XLDataType.Number => value.GetNumber(),
            XLDataType.Boolean => value.GetBoolean(),
            XLDataType.DateTime => value.GetDateTime(),
            XLDataType.TimeSpan => value.GetTimeSpan(),
            _ => value.ToString(CultureInfo.InvariantCulture),
        };
        return new RawCell(plain, false);
    }

    private static Result<ParsedSheet> Fail(string message) =>
        Result.Failure<ParsedSheet>(AppError.Validation(message, "file", message));
}

/// <summary>Generates templates, exports and error reports. Ported from the writing half of <c>ExcelFileService</c>.</summary>
internal sealed class ClosedXmlWorkbookWriter : IWorkbookWriter
{
    private const string HeaderDark = "#1F3A68";
    private const string HeaderRequired = "#1E4DB7";
    private const string HeaderOptional = "#5B6B8A";
    private const int ValidationRows = 1000;

    public byte[] BuildTemplate(ExcelModuleInfo info, IReadOnlyList<ExportSheet> referenceSheets, ExcelLimits limits)
    {
        using var workbook = NewWorkbook($"{info.Label} import template");
        var columns = info.Columns;

        var guide = workbook.AddWorksheet("Instructions");
        guide.Column(1).Width = 4;
        guide.Column(2).Width = 100;
        guide.Cell("B1").Value = $"{info.Label} — import template";
        guide.Cell("B1").Style.Font.Bold = true;
        guide.Cell("B1").Style.Font.FontSize = 16;
        string[] lines =
        [
            info.Description,
            string.Empty,
            "How to use this template",
            $"1. Fill in the \"{SafeName(info.SheetName)}\" worksheet, one record per row, starting directly below the header row.",
            "2. Do not rename, reorder or delete the column headers. Dark blue headers are required; grey headers are optional.",
            "3. See the \"Field guide\" worksheet for what each column means, its allowed values and an example.",
            "4. Use the drop-down lists where offered. Dates use the format yyyy-MM-dd (for example 2026-03-31).",
            "5. Save as .xlsx and upload the file on the Import Data screen. You will see a preview before anything is imported.",
            string.Empty,
            "Rules",
            $"• Existing records are matched by: {info.MatchKeyLabel}.",
            $"• Limits: up to {limits.MaxRows.ToString("N0", CultureInfo.InvariantCulture)} data rows and {(limits.MaxFileBytes / (1024.0 * 1024.0)).ToString("0", CultureInfo.InvariantCulture)} MB per file.",
            "• Formulas, macros and links to other workbooks are not accepted. Enter plain values.",
            "• All data in this demo is sample data.",
        ];
        for (var i = 0; i < lines.Length; i++)
        {
            var cell = guide.Cell(i + 3, 2);
            cell.Value = lines[i];
            cell.Style.Alignment.WrapText = true;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
            if (lines[i] is "How to use this template" or "Rules")
            {
                cell.Style.Font.Bold = true;
                cell.Style.Font.FontSize = 12;
            }
        }

        var data = workbook.AddWorksheet(SafeName(info.SheetName));
        var enumColumns = columns.Where(c => c.Type == ExcelColumnType.Enum && c.Allowed.Count > 0).ToList();
        for (var i = 0; i < columns.Count; i++)
        {
            var column = columns[i];
            var col = i + 1;
            data.Cell(1, col).Value = column.Header;
            data.Column(col).Width = column.Width ?? Math.Min(40, Math.Max(14, column.Header.Length + 4));
            if (column.Type == ExcelColumnType.Date)
            {
                data.Column(col).Style.DateFormat.Format = "yyyy-mm-dd";
            }
            else if (column.Type is ExcelColumnType.Text or ExcelColumnType.Phone or ExcelColumnType.Email)
            {
                data.Column(col).Style.NumberFormat.Format = "@";
            }
        }

        var listSheet = workbook.AddWorksheet("Lists");
        for (var i = 0; i < enumColumns.Count; i++)
        {
            listSheet.Cell(1, i + 1).Value = enumColumns[i].Header;
            for (var row = 0; row < enumColumns[i].Allowed.Count; row++)
            {
                listSheet.Cell(row + 2, i + 1).Value = enumColumns[i].Allowed[row];
            }
        }

        for (var i = 0; i < columns.Count; i++)
        {
            var listIndex = enumColumns.FindIndex(c => c.Key == columns[i].Key);
            AddValidation(data, listSheet, listIndex < 0 ? null : listIndex + 1, columns[i], i + 1);
        }

        StyleHeader(data, columns.Count, [.. columns.Select(c => c.Required ? HeaderRequired : HeaderOptional)]);

        var fieldGuide = workbook.AddWorksheet("Field guide");
        string[] titles = ["Column", "Required", "Type", "Allowed values / format", "Description", "Example"];
        for (var i = 0; i < titles.Length; i++)
        {
            fieldGuide.Cell(1, i + 1).Value = titles[i];
        }

        int[] widths = [26, 11, 20, 46, 60, 26];
        for (var i = 0; i < widths.Length; i++)
        {
            fieldGuide.Column(i + 1).Width = widths[i];
        }

        for (var i = 0; i < columns.Count; i++)
        {
            var column = columns[i];
            var row = i + 2;
            fieldGuide.Cell(row, 1).Value = column.Header;
            fieldGuide.Cell(row, 2).Value = column.Required ? "Yes" : "No";
            fieldGuide.Cell(row, 3).Value = TypeLabel(column.Type);
            fieldGuide.Cell(row, 4).Value = FormatRule(column);
            fieldGuide.Cell(row, 5).Value = column.Description;
            fieldGuide.Cell(row, 6).Value = column.Example is null ? string.Empty : Convert.ToString(column.Example, CultureInfo.InvariantCulture);
            for (var c = 1; c <= 6; c++)
            {
                fieldGuide.Cell(row, c).Style.Alignment.WrapText = true;
                fieldGuide.Cell(row, c).Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
            }
        }

        StyleHeader(fieldGuide, 6, []);
        foreach (var sheet in referenceSheets)
        {
            WriteSheet(workbook, sheet);
        }

        if (enumColumns.Count == 0)
        {
            listSheet.Delete();
        }
        else
        {
            listSheet.Visibility = XLWorksheetVisibility.Hidden;
            listSheet.Position = workbook.Worksheets.Count;
        }

        return Save(workbook);
    }

    public byte[] BuildExport(string title, ExportSheet main, IReadOnlyList<ExportSheet> extraSheets)
    {
        using var workbook = NewWorkbook(title);
        foreach (var sheet in new[] { main }.Concat(extraSheets))
        {
            WriteSheet(workbook, sheet);
        }

        return Save(workbook);
    }

    public byte[] BuildErrorReport(string moduleLabel, IReadOnlyList<ImportIssueRow> issues)
    {
        using var workbook = NewWorkbook($"{moduleLabel} import report");
        var sheet = workbook.AddWorksheet("Validation report");
        string[] titles = ["Row", "Field", "Original value", "Severity", "Error", "Suggested correction"];
        for (var i = 0; i < titles.Length; i++)
        {
            sheet.Cell(1, i + 1).Value = titles[i];
        }

        int[] widths = [8, 24, 30, 11, 60, 50];
        for (var i = 0; i < widths.Length; i++)
        {
            sheet.Column(i + 1).Width = widths[i];
        }

        StyleHeader(sheet, 6, []);
        for (var i = 0; i < issues.Count; i++)
        {
            var issue = issues[i];
            var row = i + 2;
            sheet.Cell(row, 1).Value = issue.RowNumber;
            sheet.Cell(row, 2).Value = ExcelCoercion.NeutralizeFormula(issue.Field);
            sheet.Cell(row, 3).Style.NumberFormat.Format = "@";
            sheet.Cell(row, 3).Value = ExcelCoercion.NeutralizeFormula(issue.Value);
            sheet.Cell(row, 4).Value = issue.Severity.ToString();
            sheet.Cell(row, 5).Value = ExcelCoercion.NeutralizeFormula(issue.Message);
            sheet.Cell(row, 6).Value = ExcelCoercion.NeutralizeFormula(issue.Suggestion ?? string.Empty);
        }

        return Save(workbook);
    }

    private static XLWorkbook NewWorkbook(string title)
    {
        var workbook = new XLWorkbook();
        workbook.Properties.Author = "APEX HRMS (sample data)";
        workbook.Properties.Title = title;
        return workbook;
    }

    private static byte[] Save(XLWorkbook workbook)
    {
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static string SafeName(string name)
    {
        var cleaned = new string([.. name.Select(ch => "\\/*?:[]".Contains(ch, StringComparison.Ordinal) ? ' ' : ch)]).Trim();
        return cleaned.Length == 0 ? "Sheet" : cleaned[..Math.Min(31, cleaned.Length)];
    }

    private static void StyleHeader(IXLWorksheet sheet, int count, IReadOnlyList<string> fills)
    {
        sheet.Row(1).Height = 24;
        for (var col = 1; col <= count; col++)
        {
            var cell = sheet.Cell(1, col);
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml(col - 1 < fills.Count ? fills[col - 1] : HeaderDark);
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            cell.Style.Alignment.WrapText = true;
        }

        sheet.SheetView.FreezeRows(1);
        sheet.Range(1, 1, 1, count).SetAutoFilter();
    }

    private static string TypeLabel(ExcelColumnType type) => type switch
    {
        ExcelColumnType.Integer => "Whole number",
        ExcelColumnType.Decimal => "Number",
        ExcelColumnType.Date => "Date (yyyy-MM-dd)",
        ExcelColumnType.Time => "Time (HH:mm)",
        ExcelColumnType.Email => "Email address",
        ExcelColumnType.Phone => "Phone number",
        ExcelColumnType.Enum => "Choose from list",
        ExcelColumnType.Boolean => "Yes / No",
        _ => "Text",
    };

    private static string FormatRule(ExcelColumn column)
    {
        var parts = new List<string>();
        if (column.Type == ExcelColumnType.Enum)
        {
            parts.Add(string.Join(", ", column.Allowed));
        }

        if (column.MaxLength is { } max)
        {
            parts.Add($"Up to {max} characters");
        }

        if (column.Type == ExcelColumnType.Decimal && column.Decimals is { } decimals)
        {
            parts.Add($"Up to {decimals} decimal places");
        }

        if (column.Min is not null || column.Max is not null)
        {
            parts.Add($"{column.Min?.ToString(CultureInfo.InvariantCulture) ?? "…"} to {column.Max?.ToString(CultureInfo.InvariantCulture) ?? "…"}");
        }

        if (column.Type == ExcelColumnType.Date)
        {
            parts.Add("yyyy-MM-dd");
        }

        if (column.Type == ExcelColumnType.Time)
        {
            parts.Add("HH:mm (24-hour)");
        }

        if (column.Type == ExcelColumnType.Boolean)
        {
            parts.Add("Yes or No");
        }

        return string.Join("; ", parts);
    }

    /// <summary>Adds the drop-down or data check for a column to the first rows of the data sheet.</summary>
    private static void AddValidation(IXLWorksheet data, IXLWorksheet lists, int? listColumn, ExcelColumn column, int col)
    {
        var range = data.Range(2, col, ValidationRows + 1, col);
        var validation = range.CreateDataValidation();
        validation.IgnoreBlanks = !column.Required;
        validation.ShowErrorMessage = true;
        validation.ErrorStyle = XLErrorStyle.Stop;
        validation.ErrorTitle = column.Header.Length > 32 ? column.Header[..32] : column.Header;
        if (column.Type == ExcelColumnType.Enum && listColumn is { } listCol && column.Allowed.Count > 0)
        {
            validation.List(lists.Range(2, listCol, column.Allowed.Count + 1, listCol));
            validation.ErrorMessage = $"Choose one of: {string.Join(", ", column.Allowed)}.";
        }
        else if (column.Type == ExcelColumnType.Boolean)
        {
            validation.List("Yes,No");
            validation.ErrorMessage = "Choose Yes or No.";
        }
        else if (column.Type is ExcelColumnType.Integer or ExcelColumnType.Decimal)
        {
            var low = (double)(column.Min ?? -1_000_000_000_000m);
            var high = (double)(column.Max ?? 1_000_000_000_000m);
            if (column.Type == ExcelColumnType.Integer)
            {
                validation.WholeNumber.Between((int)Math.Clamp(low, int.MinValue, int.MaxValue), (int)Math.Clamp(high, int.MinValue, int.MaxValue));
            }
            else
            {
                validation.Decimal.Between(low, high);
            }

            validation.ErrorMessage = "Enter a number.";
        }
        else if (column.Type == ExcelColumnType.Date)
        {
            validation.Date.GreaterThan(new DateTime(1900, 1, 1));
            validation.ErrorMessage = "Enter a valid date (yyyy-MM-dd).";
        }
        else if (column.MaxLength is { } length && column.Type is ExcelColumnType.Text or ExcelColumnType.Email or ExcelColumnType.Phone)
        {
            validation.TextLength.EqualOrLessThan(length);
            validation.ErrorMessage = $"At most {length} characters.";
        }
        else
        {
            validation.Clear();
        }
    }

    private static void WriteSheet(XLWorkbook workbook, ExportSheet sheet)
    {
        var ws = workbook.AddWorksheet(SafeName(sheet.Name));
        for (var i = 0; i < sheet.Columns.Count; i++)
        {
            var column = sheet.Columns[i];
            ws.Cell(1, i + 1).Value = column.Header;
            var widest = sheet.Rows.Take(500).Aggregate(
                column.Header.Length,
                (max, row) => Math.Max(max, Convert.ToString(row.GetValueOrDefault(column.Key), CultureInfo.InvariantCulture)?.Length ?? 0));
            ws.Column(i + 1).Width = column.Width ?? Math.Min(50, Math.Max(11, widest + 2));
        }

        StyleHeader(ws, sheet.Columns.Count, []);
        for (var r = 0; r < sheet.Rows.Count; r++)
        {
            for (var c = 0; c < sheet.Columns.Count; c++)
            {
                var column = sheet.Columns[c];
                var value = sheet.Rows[r].GetValueOrDefault(column.Key);
                if (value is null or "")
                {
                    continue;
                }

                WriteCell(ws.Cell(r + 2, c + 1), column, value);
            }
        }
    }

    private static void WriteCell(IXLCell cell, ExcelColumn column, object value)
    {
        switch (column.Type)
        {
            case ExcelColumnType.Date when ToDate(value) is { } date:
                cell.Value = date;
                cell.Style.DateFormat.Format = "yyyy-mm-dd";
                break;
            case ExcelColumnType.Integer or ExcelColumnType.Decimal when ToNumber(value) is { } number:
                cell.Value = number;
                cell.Style.NumberFormat.Format = column.Type == ExcelColumnType.Decimal ? "#,##0.00" : "0";
                break;
            case ExcelColumnType.Boolean:
                cell.Value = value is true || value is "true" ? "Yes" : "No";
                break;
            default:
                cell.Style.NumberFormat.Format = "@";
                cell.Value = ExcelCoercion.NeutralizeFormula(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty);
                break;
        }
    }

    private static DateTime? ToDate(object value) => value switch
    {
        DateOnly date => date.ToDateTime(TimeOnly.MinValue),
        DateTime date => date.Date,
        string text when DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) => parsed.ToDateTime(TimeOnly.MinValue),
        _ => null,
    };

    private static double? ToNumber(object value) => value switch
    {
        decimal d => (double)d,
        double d => d,
        int i => i,
        long l => l,
        string s when double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) => parsed,
        _ => null,
    };
}
