using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Performance;
using Apex.Hrms.TestSupport;

namespace Apex.Hrms.Domain.Tests.Performance;

/// <summary>The evidence file rules: the content decides the type, the extension must agree, the size is 1 byte to 10 MB, the name is cleaned and the storage key is exactly 32 lower-case hex characters.</summary>
public sealed class EvidenceFilesTests
{
    private const string Allowed = "Allowed evidence files are PDF, PNG, JPEG, Word (.docx) and Excel (.xlsx), up to 10 MB.";

    public static TheoryData<string, EvidenceKind, string> Accepted => new()
    {
        { "report.pdf", EvidenceKind.Pdf, "application/pdf" },
        { "REPORT.PDF", EvidenceKind.Pdf, "application/pdf" },
        { "shot.png", EvidenceKind.Png, "image/png" },
        { "photo.jpg", EvidenceKind.Jpeg, "image/jpeg" },
        { "photo.JPEG", EvidenceKind.Jpeg, "image/jpeg" },
        { "notes.docx", EvidenceKind.Docx, EvidenceFiles.DocxContentType },
        { "sheet.xlsx", EvidenceKind.Xlsx, EvidenceFiles.XlsxContentType },
    };

    [Theory]
    [MemberData(nameof(Accepted))]
    public void Each_allowed_type_is_accepted_by_its_content_and_stored_with_the_detected_content_type(string name, EvidenceKind kind, string contentType)
    {
        var content = Sample(kind);

        var info = Succeeded(EvidenceFiles.Inspect(name, content));

        Assert.Equal((name, kind, contentType), (info.FileName, info.Kind, info.ContentType));
        Assert.Equal(kind, EvidenceFiles.Detect(content));
    }

    public static TheoryData<string, EvidenceKind> Mismatched => new()
    {
        { "a.png", EvidenceKind.Pdf },
        { "a.pdf", EvidenceKind.Png },
        { "a.pdf", EvidenceKind.Jpeg },
        { "a.jpg", EvidenceKind.Png },
        { "a.docx", EvidenceKind.Xlsx },
        { "a.xlsx", EvidenceKind.Docx },
        { "a.pdf", EvidenceKind.Docx },
        { "a.docx", EvidenceKind.Pdf },
        { "a.txt", EvidenceKind.Pdf },
        { "a.exe", EvidenceKind.Pdf },
        { "a", EvidenceKind.Pdf },
        { "a.pdf.exe", EvidenceKind.Pdf },
    };

    [Theory]
    [MemberData(nameof(Mismatched))]
    public void An_extension_that_does_not_agree_with_the_content_is_refused(string name, EvidenceKind content)
    {
        var result = EvidenceFiles.Inspect(name, Sample(content));

        AssertRefused(result, Allowed);
    }

    [Fact]
    public void A_renamed_executable_html_svg_or_text_is_refused_whatever_its_extension_and_claimed_type()
    {
        foreach (var name in new[] { "a.pdf", "a.png", "a.jpg", "a.docx", "a.xlsx", "a.exe", "a.html", "a.svg" })
        {
            AssertRefused(EvidenceFiles.Inspect(name, EvidenceSamples.Executable()), Allowed);
            AssertRefused(EvidenceFiles.Inspect(name, EvidenceSamples.Html()), Allowed);
            AssertRefused(EvidenceFiles.Inspect(name, EvidenceSamples.Svg()), Allowed);
        }
    }

    [Fact]
    public void A_zip_that_is_not_a_word_or_excel_package_is_refused_even_when_named_docx_xlsx_or_pdf()
    {
        var plainZip = EvidenceSamples.Zip("readme.txt", "payload.exe");
        var noContentTypes = EvidenceSamples.Zip("word/document.xml");

        foreach (var name in new[] { "a.docx", "a.xlsx", "a.pdf", "a.zip" })
        {
            AssertRefused(EvidenceFiles.Inspect(name, plainZip), Allowed);
            AssertRefused(EvidenceFiles.Inspect(name, noContentTypes), Allowed);
        }
    }

    [Fact]
    public void A_broken_zip_or_a_bare_zip_header_is_refused_without_throwing()
    {
        byte[] bareHeader = [0x50, 0x4B, 0x03, 0x04];
        var truncated = EvidenceSamples.Docx()[..30];

        AssertRefused(EvidenceFiles.Inspect("a.docx", bareHeader), Allowed);
        AssertRefused(EvidenceFiles.Inspect("a.docx", truncated), Allowed);
        Assert.Null(EvidenceFiles.Detect(bareHeader));
    }

    [Fact]
    public void A_pdf_signature_must_be_at_the_start_of_the_file()
    {
        var shifted = new byte[] { (byte)' ' }.Concat(EvidenceSamples.Pdf()).ToArray();

        AssertRefused(EvidenceFiles.Inspect("a.pdf", shifted), Allowed);
    }

    [Fact]
    public void An_empty_file_is_refused_and_one_byte_of_a_real_signature_is_not_enough()
    {
        AssertRefused(EvidenceFiles.Inspect("a.pdf", []), EvidenceFiles.EmptyMessage);
        AssertRefused(EvidenceFiles.Inspect("a.pdf", [(byte)'%']), Allowed);
        AssertRefused(EvidenceFiles.Inspect("a.png", [0x89, 0x50, 0x4E, 0x47]), Allowed);
    }

    [Fact]
    public void Exactly_ten_megabytes_is_accepted_and_one_byte_more_is_refused()
    {
        var exact = EvidenceSamples.Pdf((int)EvidenceFiles.MaxBytes);
        var over = EvidenceSamples.Pdf((int)EvidenceFiles.MaxBytes + 1);

        Assert.Equal(10_485_760, exact.Length);
        Assert.Equal("a.pdf", Succeeded(EvidenceFiles.Inspect("a.pdf", exact)).FileName);
        AssertRefused(EvidenceFiles.Inspect("a.pdf", over), EvidenceFiles.TooLargeMessage);
    }

    [Fact]
    public void A_file_with_no_name_gets_evidence_and_the_extension_of_what_it_is()
    {
        Assert.Equal("evidence.pdf", Succeeded(EvidenceFiles.Inspect(null, EvidenceSamples.Pdf())).FileName);
        Assert.Equal("evidence.pdf", Succeeded(EvidenceFiles.Inspect("  ", EvidenceSamples.Pdf())).FileName);
        Assert.Equal("evidence.png", Succeeded(EvidenceFiles.Inspect("...", EvidenceSamples.Png())).FileName);
        Assert.Equal("evidence.jpg", Succeeded(EvidenceFiles.Inspect("", EvidenceSamples.Jpeg())).FileName);
        Assert.Equal("evidence.docx", Succeeded(EvidenceFiles.Inspect("\u0001\u0002", EvidenceSamples.Docx())).FileName);
        Assert.Equal("evidence.xlsx", Succeeded(EvidenceFiles.Inspect(null, EvidenceSamples.Xlsx())).FileName);
    }

    [Fact]
    public void A_name_that_is_only_an_extension_keeps_the_extension_under_the_fallback_stem()
    {
        Assert.Equal("evidence.pdf", Succeeded(EvidenceFiles.Inspect(".pdf", EvidenceSamples.Pdf())).FileName);
    }

    [Fact]
    public void The_stored_name_is_the_cleaned_one()
    {
        Assert.Equal("x.pdf", Succeeded(EvidenceFiles.Inspect(@"..\..\x.pdf", EvidenceSamples.Pdf())).FileName);
    }

    // ---- File name cleaning ----

    [Theory]
    [InlineData(@"..\..\x.pdf", "x.pdf")]
    [InlineData("../../x.pdf", "x.pdf")]
    [InlineData("/etc/passwd", "passwd")]
    [InlineData(@"C:\Windows\System32\cmd.exe", "cmd.exe")]
    [InlineData("a/b\\c/d.pdf", "d.pdf")]
    [InlineData("  report.pdf  ", "report.pdf")]
    [InlineData("rep\u0000ort\u0007.pdf", "report.pdf")]
    [InlineData("line\r\nbreak.pdf", "linebreak.pdf")]
    [InlineData("tab\there.pdf", "tabhere.pdf")]
    [InlineData("q4: <final> \"v2\" | a?*.pdf", "q4 final v2  a.pdf")]
    [InlineData("invoice\u202Efdp.exe", "invoicefdp.exe")]
    [InlineData("a\u2028b\u2029c.pdf", "abc.pdf")]
    [InlineData("မြန်မာ.pdf", "မြန်မာ.pdf")]
    [InlineData("no extension", "no extension")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData("..", "")]
    [InlineData("...", "")]
    [InlineData("dir/", "")]
    [InlineData(@"..\", "")]
    public void A_client_file_name_is_cleaned_to_its_last_segment_without_control_characters_and_forbidden_characters(string raw, string expected)
    {
        Assert.Equal(expected, EvidenceFiles.SanitizeFileName(raw));
    }

    [Fact]
    public void A_null_name_cleans_to_nothing()
    {
        Assert.Equal(string.Empty, EvidenceFiles.SanitizeFileName(null));
    }

    [Fact]
    public void A_name_of_three_hundred_characters_is_cut_to_260_and_keeps_its_extension()
    {
        var cleaned = EvidenceFiles.SanitizeFileName(new string('a', 296) + ".pdf");

        Assert.Equal(260, cleaned.Length);
        Assert.EndsWith(".pdf", cleaned, StringComparison.Ordinal);
        Assert.Equal(new string('a', 256) + ".pdf", cleaned);
    }

    [Fact]
    public void A_long_name_without_an_extension_is_cut_to_260()
    {
        Assert.Equal(260, EvidenceFiles.SanitizeFileName(new string('b', 300)).Length);
    }

    [Fact]
    public void A_long_name_is_never_cut_in_the_middle_of_a_surrogate_pair()
    {
        var name = new string('a', 259) + "\U0001F600\U0001F600.pdf";

        var cleaned = EvidenceFiles.SanitizeFileName(name);

        Assert.True(cleaned.Length <= 260);
        Assert.EndsWith(".pdf", cleaned, StringComparison.Ordinal);
        Assert.DoesNotContain(cleaned, character => char.IsSurrogate(character) && !IsPaired(cleaned, character));
    }

    [Fact]
    public void A_long_name_through_inspect_fits_the_attachment_column_and_creates_a_valid_row()
    {
        var info = Succeeded(EvidenceFiles.Inspect(new string('n', 400) + ".docx", EvidenceSamples.Docx()));

        var attachment = KpiAttachment.Create(1, info.FileName, info.ContentType, 100, IdOf('a'), new DateOnly(2026, 11, 9));

        Assert.True(info.FileName.Length <= KpiAttachment.FileNameMaxLength);
        Assert.True(attachment.IsSuccess);
    }

    // ---- Storage keys ----

    [Theory]
    [InlineData("0123456789abcdef0123456789abcdef", true)]
    [InlineData("ffffffffffffffffffffffffffffffff", true)]
    [InlineData("00000000000000000000000000000000", true)]
    [InlineData("0123456789ABCDEF0123456789abcdef", false)]
    [InlineData("0123456789abcdef0123456789abcde", false)]
    [InlineData("0123456789abcdef0123456789abcdef0", false)]
    [InlineData("0123456789abcdef0123456789abcdeg", false)]
    [InlineData("..\\..\\0123456789abcdef0123456789", false)]
    [InlineData("../0123456789abcdef0123456789abcd", false)]
    [InlineData("0123456789abcdef-0123456789abcdef", false)]
    [InlineData("0123456789abcdef0123456789abcde\n", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void A_storage_key_is_valid_only_as_exactly_32_lower_case_hex_characters(string? key, bool valid)
    {
        Assert.Equal(valid, KpiAttachment.IsValidStorageKey(key));
    }

    [Fact]
    public void A_generated_key_is_always_valid_and_never_repeats()
    {
        var keys = Enumerable.Range(0, 200).Select(_ => Guid.NewGuid().ToString("N")).ToList();

        Assert.All(keys, key => Assert.True(KpiAttachment.IsValidStorageKey(key), key));
        Assert.Equal(keys.Count, keys.Distinct().Count());
    }

    [Fact]
    public void Only_the_stored_content_types_are_served_with_their_own_type()
    {
        foreach (var kind in Enum.GetValues<EvidenceKind>())
        {
            Assert.True(EvidenceFiles.IsStoredContentType(EvidenceFiles.ContentTypeOf(kind)));
        }

        Assert.False(EvidenceFiles.IsStoredContentType("text/html"));
        Assert.False(EvidenceFiles.IsStoredContentType("image/svg+xml"));
        Assert.False(EvidenceFiles.IsStoredContentType(null));
    }

    private static string IdOf(char digit) => new(digit, 32);

    private static bool IsPaired(string text, char surrogate)
    {
        var index = text.IndexOf(surrogate, StringComparison.Ordinal);
        return char.IsHighSurrogate(surrogate)
            ? index + 1 < text.Length && char.IsLowSurrogate(text[index + 1])
            : index > 0 && char.IsHighSurrogate(text[index - 1]);
    }

    private static byte[] Sample(EvidenceKind kind) => kind switch
    {
        EvidenceKind.Pdf => EvidenceSamples.Pdf(),
        EvidenceKind.Png => EvidenceSamples.Png(),
        EvidenceKind.Jpeg => EvidenceSamples.Jpeg(),
        EvidenceKind.Docx => EvidenceSamples.Docx(),
        _ => EvidenceSamples.Xlsx(),
    };

    private static EvidenceFileInfo Succeeded(Result<EvidenceFileInfo> result)
    {
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        return result.Value;
    }

    private static void AssertRefused(Result<EvidenceFileInfo> result, string message)
    {
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal(message, Assert.Single(result.Error.FieldErrors!["file"]));
    }
}
