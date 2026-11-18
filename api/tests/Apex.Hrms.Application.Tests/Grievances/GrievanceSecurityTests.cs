using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Apex.Hrms.Application.Excel;
using Apex.Hrms.Application.Grievances;
using Apex.Hrms.Application.Tests.Fakes;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Grievances;
using Apex.Hrms.TestSupport;

namespace Apex.Hrms.Application.Tests.Grievances;

/// <summary>
/// The fixes of the security review: metadata removed from an anonymous submitter's files, the file and draft quotas, what a
/// reviewer-only caller reads, and the module-level access log. Every file here is a tiny fictional one built by the test.
/// </summary>
public sealed class GrievanceSecurityTests : GrievanceTestBase
{
    private const string Marker = "TST-IDENTIFYING-MARKER";

    // ---- Metadata of an anonymous submitter's files (M3) ----

    private async Task<(int Id, string Version)> AnonymousCaseAsync(bool anonymous = true)
    {
        var submitted = await SubmittedAsync(anonymous);
        AsSam();
        return (submitted.Id, V(Version(submitted.Id)));
    }

    private (byte[] Stored, GrievanceAttachment Row) OnlyStoredFile()
    {
        var row = Host.Repository<GrievanceAttachment>().Items.Single();
        return (Files.Files[row.StorageKey], row);
    }

    private static byte[] Docx()
    {
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            void Add(string name, string text)
            {
                using var stream = zip.CreateEntry(name).Open();
                stream.Write(Encoding.UTF8.GetBytes(text));
            }

            Add("[Content_Types].xml", """<?xml version="1.0"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="xml" ContentType="application/xml"/><Override PartName="/docProps/custom.xml" ContentType="application/vnd.openxmlformats-officedocument.custom-properties+xml"/></Types>""");
            Add("_rels/.rels", """<?xml version="1.0"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/><Relationship Id="rId9" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/custom-properties" Target="docProps/custom.xml"/></Relationships>""");
            Add("word/document.xml", """<?xml version="1.0"?><w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:body/></w:document>""");
            Add("docProps/core.xml", $"""<?xml version="1.0"?><cp:coreProperties xmlns:cp="http://schemas.openxmlformats.org/package/2006/metadata/core-properties" xmlns:dc="http://purl.org/dc/elements/1.1/"><dc:creator>{Marker} author</dc:creator><cp:lastModifiedBy>{Marker} editor</cp:lastModifiedBy></cp:coreProperties>""");
            Add("docProps/app.xml", $"""<?xml version="1.0"?><Properties xmlns="http://schemas.openxmlformats.org/officeDocument/2006/extended-properties"><Company>{Marker} company</Company></Properties>""");
            Add("docProps/custom.xml", $"""<?xml version="1.0"?><Properties xmlns="http://schemas.openxmlformats.org/officeDocument/2006/custom-properties"><property name="Owner">{Marker}</property></Properties>""");
        }

        return output.ToArray();
    }

    private static byte[] Segment(byte marker, byte[] payload) =>
        [0xFF, marker, (byte)((payload.Length + 2) >> 8), (byte)((payload.Length + 2) & 0xFF), .. payload];

    private static byte[] Jpeg() =>
    [
        0xFF, 0xD8,
        .. Segment(0xE0, [.. "JFIF\0"u8, 1, 1, 0, 0, 1, 0, 1, 0, 0]),
        .. Segment(0xE1, [.. "Exif\0\0"u8, .. Encoding.ASCII.GetBytes(Marker + " GPS 16.8N 96.1E")]),
        .. Segment(0xED, [.. Encoding.ASCII.GetBytes("Photoshop 3.0 " + Marker)]),
        .. Segment(0xFE, [.. Encoding.ASCII.GetBytes(Marker + " comment")]),
        .. Segment(0xDA, [1, 1, 0, 0, 63, 0]),
        0x12, 0x34, 0x56, 0x78,
        0xFF, 0xD9,
    ];

    private static byte[] Chunk(string type, byte[] data) =>
        [(byte)(data.Length >> 24), (byte)(data.Length >> 16), (byte)(data.Length >> 8), (byte)data.Length, .. Encoding.ASCII.GetBytes(type), .. data, 0, 0, 0, 0];

    private static byte[] Png() =>
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        .. Chunk("IHDR", [0, 0, 0, 1, 0, 0, 0, 1, 8, 0, 0, 0, 0]),
        .. Chunk("tEXt", [.. "Author\0"u8, .. Encoding.ASCII.GetBytes(Marker)]),
        .. Chunk("tIME", [7, 234, 11, 9, 9, 0, 0]),
        .. Chunk("IDAT", [0x78, 0x9C, 0x63, 0, 0, 0, 1, 0, 1]),
        .. Chunk("IEND", []),
    ];

    [Fact]
    public async Task An_anonymous_submitters_office_file_loses_its_document_properties()
    {
        await SeedAsync();
        var (id, version) = await AnonymousCaseAsync();

        var uploaded = ResultAssert.Succeeded(await My.UploadAttachmentAsync(id, new GrievanceFileUpload(version, "statement.docx", Docx()), default));

        var (stored, row) = OnlyStoredFile();
        Assert.Equal(stored.LongLength, row.SizeBytes);
        Assert.Equal(stored.LongLength, uploaded.Attachment.SizeBytes);
        Assert.Equal(SHA256.HashData(stored), row.Sha256);
        using var zip = new ZipArchive(new MemoryStream(stored), ZipArchiveMode.Read);
        Assert.DoesNotContain(zip.Entries, entry => entry.FullName == "docProps/custom.xml");
        Assert.NotNull(zip.GetEntry("word/document.xml"));
        foreach (var entry in zip.Entries)
        {
            using var reader = new StreamReader(entry.Open());
            var text = await reader.ReadToEndAsync();
            Assert.DoesNotContain(Marker, text, StringComparison.Ordinal);
            Assert.DoesNotContain("docProps/custom.xml", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task An_anonymous_submitters_jpeg_loses_exif_iptc_and_comments_and_keeps_the_image()
    {
        await SeedAsync();
        var (id, version) = await AnonymousCaseAsync();
        var original = Jpeg();

        ResultAssert.Succeeded(await My.UploadAttachmentAsync(id, new GrievanceFileUpload(version, "photo.jpg", original), default));

        var (stored, row) = OnlyStoredFile();
        Assert.DoesNotContain(Marker, Encoding.ASCII.GetString(stored), StringComparison.Ordinal);
        Assert.Equal(original[..2], stored[..2]);
        Assert.Equal(original[^8..], stored[^8..]);
        Assert.Contains("JFIF", Encoding.ASCII.GetString(stored), StringComparison.Ordinal);
        Assert.Equal(SHA256.HashData(stored), row.Sha256);
    }

    [Fact]
    public async Task An_anonymous_submitters_png_loses_its_text_and_time_chunks()
    {
        await SeedAsync();
        var (id, version) = await AnonymousCaseAsync();

        ResultAssert.Succeeded(await My.UploadAttachmentAsync(id, new GrievanceFileUpload(version, "screen.png", Png()), default));

        var text = Encoding.ASCII.GetString(OnlyStoredFile().Stored);
        Assert.DoesNotContain(Marker, text, StringComparison.Ordinal);
        Assert.DoesNotContain("tIME", text, StringComparison.Ordinal);
        Assert.Contains("IHDR", text, StringComparison.Ordinal);
        Assert.Contains("IDAT", text, StringComparison.Ordinal);
        Assert.EndsWith("IEND\0\0\0\0", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_pdf_and_any_named_upload_are_stored_as_sent()
    {
        await SeedAsync();
        var (anonymous, anonymousVersion) = await AnonymousCaseAsync();
        var pdf = EvidenceSamples.Pdf(256);
        ResultAssert.Succeeded(await My.UploadAttachmentAsync(anonymous, new GrievanceFileUpload(anonymousVersion, "letter.pdf", pdf), default));
        var (named, namedVersion) = await AnonymousCaseAsync(anonymous: false);
        var jpeg = Jpeg();
        ResultAssert.Succeeded(await My.UploadAttachmentAsync(named, new GrievanceFileUpload(namedVersion, "photo.jpg", jpeg), default));

        Assert.Contains(Files.Files.Values, stored => stored.SequenceEqual(pdf));
        Assert.Contains(Files.Files.Values, stored => stored.SequenceEqual(jpeg));
    }

    [Fact]
    public async Task An_anonymous_file_that_cannot_be_cleaned_is_refused_and_nothing_is_stored()
    {
        await SeedAsync();
        var (id, version) = await AnonymousCaseAsync();
        byte[] broken = [0xFF, 0xD8, 0xFF, 0xE1, 0xFF, 0xF0, .. "Exif\0\0"u8];

        var refused = ResultAssert.FailedOnField(await My.UploadAttachmentAsync(id, new GrievanceFileUpload(version, "photo.jpg", broken), default), "file");

        Assert.Contains("could not be cleaned", refused.Message, StringComparison.Ordinal);
        Assert.Empty(Files.Files);
    }

    // ---- Quotas (M4, L7) ----

    [Fact]
    public async Task The_case_team_has_its_own_twenty_files_per_case()
    {
        await SeedAsync();
        var investigating = await UnderInvestigationAsync();
        AsHana();
        for (var index = 0; index < GrievanceAttachment.MaxCaseTeamFilesPerCase; index++)
        {
            ResultAssert.Succeeded(await Work.UploadAttachmentAsync(investigating.Id, "CaseTeam", new GrievanceFileUpload(V(Version(investigating.Id)), $"team{index}.pdf", EvidenceSamples.Pdf()), default));
        }

        ResultAssert.Failed(
            await Work.UploadAttachmentAsync(investigating.Id, "CaseTeam", new GrievanceFileUpload(V(Version(investigating.Id)), "one-more.pdf", EvidenceSamples.Pdf()), default),
            ErrorType.Conflict,
            "The case team can add at most 20 files to a case.");
        AsSam();
        ResultAssert.Succeeded(await My.UploadAttachmentAsync(investigating.Id, new GrievanceFileUpload(V(Version(investigating.Id)), "mine.pdf", EvidenceSamples.Pdf()), default));
    }

    [Fact]
    public async Task A_submitters_daily_upload_bytes_are_limited_per_account()
    {
        using var small = new SmallQuota();
        await small.RunAsync();
    }

    private sealed class SmallQuota() : GrievanceTestBase(submitterDailyUploadBytes: 1024 * 1024)
    {
        public async Task RunAsync()
        {
            await SeedAsync();
            var submitted = await SubmittedAsync();
            AsSam();
            var version = V(Version(submitted.Id));
            ResultAssert.Succeeded(await My.UploadAttachmentAsync(submitted.Id, new GrievanceFileUpload(version, "a.pdf", EvidenceSamples.Pdf(600 * 1024)), default));

            var refused = ResultAssert.FailedOnField(await My.UploadAttachmentAsync(submitted.Id, new GrievanceFileUpload(version, "b.pdf", EvidenceSamples.Pdf(600 * 1024)), default), "file");

            Assert.Equal("You have reached today's upload limit for grievance files. Try again tomorrow.", refused.Message);
            ResultAssert.Succeeded(await My.UploadAttachmentAsync(submitted.Id, new GrievanceFileUpload(version, "c.pdf", EvidenceSamples.Pdf(300 * 1024)), default));
            Host.Clock.Today = Host.Clock.Today.AddDays(1);
            ResultAssert.Succeeded(await My.UploadAttachmentAsync(submitted.Id, new GrievanceFileUpload(version, "d.pdf", EvidenceSamples.Pdf(600 * 1024)), default));
        }
    }

    // ---- A reviewer-only caller (L8) ----

    [Fact]
    public async Task A_reviewer_only_reads_no_assignment_escalation_or_history_reasons_except_why_a_proposal_was_sent_back()
    {
        await SeedAsync();
        var assigned = await AssignedAsync();
        AsHana();
        await Ok(new AssignGrievanceInvestigatorCommand(assigned.Id, V(Version(assigned.Id)), W.Olga.Id, "Member", "TST workload reason"));
        await Ok(new EscalateGrievanceCommand(assigned.Id, V(Version(assigned.Id)), "TST escalation reason", false));
        AsIvan();
        await Ok(new StartGrievanceInvestigationCommand(assigned.Id, V(Version(assigned.Id)), null));
        await Ok(new ProposeGrievanceResolutionCommand(assigned.Id, V(Version(assigned.Id)), "TST proposal"));
        AsRita();
        var sentBack = await Ok(new RejectGrievanceResolutionCommand(assigned.Id, V(Version(assigned.Id)), "TST more evidence needed"));

        var history = ResultAssert.Succeeded(await Cases.HistoryAsync(assigned.Id, default));

        Assert.All(sentBack.Assignments, assignment => Assert.Null(assignment.Reason));
        Assert.All(sentBack.Escalations, escalation => Assert.Equal(string.Empty, escalation.Reason));
        Assert.Equal("TST more evidence needed", history.Single(entry => entry.Action == "RejectResolution").Reason);
        Assert.All(history.Where(entry => entry.Action != "RejectResolution"), entry => Assert.Null(entry.Reason));
        Assert.Equal("TST more evidence needed", sentBack.Proposal!.DecisionComment);

        AsHana();
        var managed = await TeamGetAsync(assigned.Id);
        Assert.Contains(managed.Assignments, assignment => assignment.Reason == "TST workload reason");
        Assert.Equal("TST escalation reason", managed.Escalations.Single().Reason);
    }

    // ---- The module-level access log (L4) ----

    [Fact]
    public async Task The_module_access_log_lists_exports_and_cases_in_the_callers_management_scope_with_date_filters()
    {
        await SeedAsync();
        var about = await AcknowledgedAsync();
        AsHana();
        await Ok(new TriageGrievanceCommand(about.Id, V(Version(about.Id)), W.Category.Id, "Medium", about.TargetResolutionDate, W.Sid.Id, null, false));
        var other = await AcknowledgedAsync();
        AsHana();
        await TeamGetAsync(other.Id);
        var module = Host.Get<IEnumerable<IExcelModule>>().Single(candidate => candidate.Info.Code == "grievance-cases");
        ResultAssert.Succeeded(await module.ExportRowsAsync(new ExportRequest(), importLayout: false, limit: 100, default));
        await Host.Get<Abstractions.IUnitOfWork>().SaveChangesAsync(default);

        var hana = ResultAssert.Succeeded(await Cases.AuditLogAsync(new(0, 200, null, null, new GrievanceAuditFilter()), default));
        AsSid();
        var sid = ResultAssert.Succeeded(await Cases.AuditLogAsync(new(0, 200, null, null, new GrievanceAuditFilter()), default));
        var exportsOnly = ResultAssert.Succeeded(await Cases.AuditLogAsync(new(0, 200, null, null, new GrievanceAuditFilter(Action: "Export")), default));
        var tomorrow = ResultAssert.Succeeded(await Cases.AuditLogAsync(new(0, 200, null, null, new GrievanceAuditFilter(From: Host.Clock.Today.AddDays(1))), default));

        Assert.Contains(hana.Items, entry => entry.CaseId == about.Id);
        Assert.Contains(hana.Items, entry => entry.CaseId == other.Id && entry.CaseNo == other.CaseNo);
        Assert.DoesNotContain(sid.Items, entry => entry.CaseId == about.Id);
        Assert.Contains(sid.Items, entry => entry.CaseId is null && entry.Action == "Export");
        Assert.All(exportsOnly.Items, entry => Assert.Equal("Export", entry.Action));
        Assert.Empty(tomorrow.Items);
        ResultAssert.FailedOnField(
            await Cases.AuditLogAsync(new(0, 20, null, null, new GrievanceAuditFilter(From: Host.Clock.Today, To: Host.Clock.Today.AddDays(-1))), default),
            "from");
    }
}
