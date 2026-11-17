using System.Buffers.Binary;
using Apex.Hrms.Domain.Performance;
using Apex.Hrms.TestSupport;

namespace Apex.Hrms.Domain.Tests.Performance;

/// <summary>
/// Corrupt and hostile ZIP and Office Open XML content, all generated here from the fictional samples: whatever the bytes, the content
/// check answers with a result (never an exception, which would be a 500) and a broken package is never accepted.
/// </summary>
public sealed class EvidenceFilesCorruptZipTests
{
    private const int EndRecordLength = 22;

    private static readonly uint EndSignature = 0x06054B50;

    /// <summary>A Word sample whose central directory is followed directly by the end record (no archive comment).</summary>
    private static byte[] Docx()
    {
        var docx = EvidenceSamples.Docx();
        Assert.Equal(EndSignature, BinaryPrimitives.ReadUInt32LittleEndian(docx.AsSpan(docx.Length - EndRecordLength)));
        return docx;
    }

    private static void AssertNotAllowed(byte[] content, string because)
    {
        var result = EvidenceFiles.Inspect("evidence.docx", content);

        Assert.True(result.IsFailure, $"Accepted: {because}");
        Assert.Equal(EvidenceFiles.NotAllowedMessage, result.Error.Message);
    }

    /// <summary>The bytes of <paramref name="source"/> with a little-endian value written at <paramref name="offset"/> from the end record.</summary>
    private static byte[] WithEndField(byte[] source, int offset, uint value, int width)
    {
        var content = (byte[])source.Clone();
        var at = content.Length - EndRecordLength + offset;
        if (width == 2)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(content.AsSpan(at), (ushort)value);
        }
        else
        {
            BinaryPrimitives.WriteUInt32LittleEndian(content.AsSpan(at), value);
        }

        return content;
    }

    [Fact]
    public void The_untouched_samples_are_accepted_so_the_corruptions_below_are_what_is_refused()
    {
        Assert.Equal(EvidenceKind.Docx, EvidenceFiles.Inspect("plan.docx", Docx()).Value.Kind);
        Assert.Equal(EvidenceKind.Xlsx, EvidenceFiles.Inspect("plan.xlsx", EvidenceSamples.Xlsx()).Value.Kind);
    }

    public static TheoryData<uint> BadDirectoryOffsets => [0u, 1u, 30u, 0x7FFFFFFFu, 0xFFFFFFFEu, 0x80000000u];

    [Theory]
    [MemberData(nameof(BadDirectoryOffsets))]
    public void A_central_directory_offset_that_points_anywhere_else_is_refused_without_throwing(uint offset)
    {
        var docx = Docx();
        var real = BinaryPrimitives.ReadUInt32LittleEndian(docx.AsSpan(docx.Length - EndRecordLength + 16));

        foreach (var value in new[] { offset, real + 1, real - 1, (uint)docx.Length, (uint)docx.Length + 1000 })
        {
            AssertNotAllowed(WithEndField(docx, 16, value, 4), $"directory offset {value}");
        }
    }

    [Fact]
    public void A_central_directory_size_or_entry_count_that_does_not_match_is_refused_without_throwing()
    {
        var docx = Docx();
        var size = BinaryPrimitives.ReadUInt32LittleEndian(docx.AsSpan(docx.Length - EndRecordLength + 12));

        foreach (var value in new[] { 0u, 1u, size - 1, size + 1, 0x7FFFFFFFu })
        {
            AssertNotAllowed(WithEndField(docx, 12, value, 4), $"directory size {value}");
        }

        foreach (var count in new uint[] { 0, 1, 3, 9999, 10_001, 0xFFFE })
        {
            AssertNotAllowed(WithEndField(WithEndField(docx, 8, count, 2), 10, count, 2), $"{count} entries");
        }
    }

    [Fact]
    public void An_archive_that_claims_to_span_several_disks_is_refused_without_throwing()
    {
        var docx = Docx();

        AssertNotAllowed(WithEndField(docx, 4, 1, 2), "this disk 1");
        AssertNotAllowed(WithEndField(docx, 6, 1, 2), "directory on disk 1");
        AssertNotAllowed(WithEndField(WithEndField(docx, 4, 1, 2), 6, 1, 2), "both on disk 1");
        AssertNotAllowed(WithEndField(docx, 4, 0xFFFF, 2), "disk number masked without a ZIP64 record");
        AssertNotAllowed(WithEndField(docx, 8, 1, 2), "entries on this disk differ from the total");
    }

    /// <summary>
    /// The sample with a ZIP64 end record and locator inserted before the classic end record, which is masked (offset 0xFFFFFFFF) so that
    /// a reader goes to the ZIP64 record. The values default to the real ones: the result is a valid ZIP64 package.
    /// </summary>
    private static byte[] Zip64(
        ulong? entriesOnDisk = null,
        ulong? entries = null,
        ulong? directorySize = null,
        ulong? directoryOffset = null,
        uint recordDisk = 0,
        uint locatorDisk = 0,
        uint disks = 1,
        long? recordAt = null,
        bool recordSignature = true)
    {
        var docx = Docx();
        var end = docx.Length - EndRecordLength;
        var count = BinaryPrimitives.ReadUInt16LittleEndian(docx.AsSpan(end + 10));
        var size = BinaryPrimitives.ReadUInt32LittleEndian(docx.AsSpan(end + 12));
        var offset = BinaryPrimitives.ReadUInt32LittleEndian(docx.AsSpan(end + 16));

        var record = new byte[56];
        BinaryPrimitives.WriteUInt32LittleEndian(record, recordSignature ? 0x06064B50u : 0x12345678u);
        BinaryPrimitives.WriteUInt64LittleEndian(record.AsSpan(4), 44);
        BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(12), 45);
        BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(14), 45);
        BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(16), recordDisk);
        BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(20), recordDisk);
        BinaryPrimitives.WriteUInt64LittleEndian(record.AsSpan(24), entriesOnDisk ?? entries ?? count);
        BinaryPrimitives.WriteUInt64LittleEndian(record.AsSpan(32), entries ?? count);
        BinaryPrimitives.WriteUInt64LittleEndian(record.AsSpan(40), directorySize ?? size);
        BinaryPrimitives.WriteUInt64LittleEndian(record.AsSpan(48), directoryOffset ?? offset);

        var locator = new byte[20];
        BinaryPrimitives.WriteUInt32LittleEndian(locator, 0x07064B50);
        BinaryPrimitives.WriteUInt32LittleEndian(locator.AsSpan(4), locatorDisk);
        BinaryPrimitives.WriteUInt64LittleEndian(locator.AsSpan(8), unchecked((ulong)(recordAt ?? end)));
        BinaryPrimitives.WriteUInt32LittleEndian(locator.AsSpan(16), disks);

        var classic = docx.AsSpan(end).ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(classic.AsSpan(16), 0xFFFFFFFF);
        return [.. docx.AsSpan(0, end), .. record, .. locator, .. classic];
    }

    [Fact]
    public void A_well_formed_zip64_package_is_still_recognised()
    {
        Assert.Equal(EvidenceKind.Docx, EvidenceFiles.Inspect("plan.docx", Zip64()).Value.Kind);
    }

    [Fact]
    public void A_zip64_record_with_a_huge_or_inconsistent_entry_count_or_bad_offsets_is_refused_without_throwing()
    {
        AssertNotAllowed(Zip64(entries: ulong.MaxValue), "2^64 - 1 entries");
        AssertNotAllowed(Zip64(entries: (ulong)int.MaxValue + 1), "2^31 entries");
        AssertNotAllowed(Zip64(entries: 2_000_000_000), "two billion entries");
        AssertNotAllowed(Zip64(entries: 10_001), "just over the entry cap");
        AssertNotAllowed(Zip64(entries: 0), "no entries");
        AssertNotAllowed(Zip64(entriesOnDisk: 1, entries: 2), "entries on the disk differ from the total");
        AssertNotAllowed(Zip64(directoryOffset: ulong.MaxValue), "directory offset 2^64 - 1");
        AssertNotAllowed(Zip64(directoryOffset: 1UL << 40), "directory offset past the content");
        AssertNotAllowed(Zip64(directoryOffset: 0), "directory offset 0");
        AssertNotAllowed(Zip64(directorySize: ulong.MaxValue), "directory size 2^64 - 1");
        AssertNotAllowed(Zip64(directorySize: 0), "directory size 0");
        AssertNotAllowed(Zip64(recordDisk: 1), "record on disk 1");
        AssertNotAllowed(Zip64(locatorDisk: 1), "locator names disk 1");
        AssertNotAllowed(Zip64(disks: 2), "two disks");
        AssertNotAllowed(Zip64(recordAt: -1), "record before the start");
        AssertNotAllowed(Zip64(recordAt: long.MaxValue), "record past the end");
        AssertNotAllowed(Zip64(recordAt: 3), "record at a random place");
        AssertNotAllowed(Zip64(recordSignature: false), "no ZIP64 record signature");
    }

    [Fact]
    public void A_masked_end_record_without_a_zip64_locator_is_refused_without_throwing()
    {
        AssertNotAllowed(WithEndField(Docx(), 16, 0xFFFFFFFF, 4), "masked offset, no locator");
        AssertNotAllowed(WithEndField(WithEndField(Docx(), 8, 0xFFFF, 2), 10, 0xFFFF, 2), "masked count, no locator");
    }

    [Fact]
    public void An_empty_central_directory_is_refused_without_throwing()
    {
        var end = new byte[EndRecordLength];
        BinaryPrimitives.WriteUInt32LittleEndian(end, EndSignature);
        byte[] header = [0x50, 0x4B, 0x03, 0x04];
        var withOffset = (byte[])end.Clone();
        BinaryPrimitives.WriteUInt32LittleEndian(withOffset.AsSpan(16), (uint)header.Length);

        AssertNotAllowed([.. header, .. end], "a header and an empty end record");
        AssertNotAllowed([.. header, .. withOffset], "an empty directory right after the header");
        AssertNotAllowed([.. header, .. new byte[26], .. withOffset], "an empty directory after a blank local header");
    }

    [Fact]
    public void Random_bytes_after_a_zip_signature_are_refused_without_throwing_with_or_without_an_end_record()
    {
        var random = new Random(20261118);
        for (var sample = 0; sample < 400; sample++)
        {
            var body = new byte[random.Next(0, 2048)];
            random.NextBytes(body);
            byte[] plain = [0x50, 0x4B, 0x03, 0x04, .. body];
            AssertNotAllowed(plain, $"random sample {sample}");

            var end = new byte[EndRecordLength];
            BinaryPrimitives.WriteUInt32LittleEndian(end, EndSignature);
            var entries = (ushort)random.Next(1, 20);
            BinaryPrimitives.WriteUInt16LittleEndian(end.AsSpan(8), entries);
            BinaryPrimitives.WriteUInt16LittleEndian(end.AsSpan(10), entries);
            BinaryPrimitives.WriteUInt32LittleEndian(end.AsSpan(12), (uint)random.Next(0, plain.Length + 64));
            BinaryPrimitives.WriteUInt32LittleEndian(end.AsSpan(16), (uint)random.Next(0, plain.Length + 64));
            AssertNotAllowed([.. plain, .. end], $"random sample {sample} with a plausible end record");
        }
    }

    [Fact]
    public void A_package_cut_at_any_length_is_refused_without_throwing()
    {
        foreach (var sample in new[] { Docx(), EvidenceSamples.Xlsx() })
        {
            for (var length = 4; length < sample.Length; length++)
            {
                AssertNotAllowed(sample[..length], $"cut at {length} of {sample.Length}");
            }
        }
    }

    [Fact]
    public void A_package_with_random_bytes_changed_never_throws_and_a_broken_directory_is_never_accepted()
    {
        var random = new Random(711);
        foreach (var sample in new[] { Docx(), EvidenceSamples.Xlsx() })
        {
            for (var round = 0; round < 1500; round++)
            {
                var mutated = (byte[])sample.Clone();
                var changes = random.Next(1, 6);
                for (var change = 0; change < changes; change++)
                {
                    mutated[random.Next(0, mutated.Length)] = (byte)random.Next(0, 256);
                }

                // Never an exception, whatever was changed. A change inside the data or a time stamp can leave a valid package, so
                // only the answer is checked to be one of the two possible ones.
                var result = EvidenceFiles.Inspect("evidence.docx", mutated);
                Assert.True(result.IsFailure ? result.Error.Message == EvidenceFiles.NotAllowedMessage : result.Value.Kind == EvidenceKind.Docx);

                // The end record's signature, disk numbers and counts broken: never accepted.
                var end = mutated.Length - EndRecordLength;
                mutated[end + random.Next(0, 4)] ^= 0xFF;
                AssertNotAllowed(mutated, $"round {round} with a broken end-record signature");
            }

        }
    }
}
