using System.IO.Compression;
using Apex.Hrms.TestSupport;

namespace Apex.Hrms.Application.Tests.Performance;

/// <summary>
/// The generated Office samples must be byte-for-byte stable: download tests compare them with a second call, and zip
/// entries used to carry the current time, which made those comparisons fail at random.
/// </summary>
public sealed class EvidenceSamplesTests
{
    /// <summary>The clock time every sample entry is stamped with (zip keeps a local date and time, with no offset).</summary>
    private static readonly DateTime FixedTime = new(2026, 1, 1, 0, 0, 0);

    public static TheoryData<string> Samples => ["docx", "xlsx"];

    private static byte[] Sample(string kind) => kind == "docx" ? EvidenceSamples.Docx() : EvidenceSamples.Xlsx();

    [Theory]
    [MemberData(nameof(Samples))]
    public void Every_entry_of_an_office_sample_carries_the_fixed_time_not_the_current_one(string kind)
    {
        using var archive = new ZipArchive(new MemoryStream(Sample(kind)), ZipArchiveMode.Read);

        Assert.NotEmpty(archive.Entries);
        Assert.All(archive.Entries, entry => Assert.Equal(FixedTime, entry.LastWriteTime.DateTime));
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void Office_samples_are_identical_across_calls(string kind) =>
        Assert.Equal(Sample(kind), Sample(kind));
}
