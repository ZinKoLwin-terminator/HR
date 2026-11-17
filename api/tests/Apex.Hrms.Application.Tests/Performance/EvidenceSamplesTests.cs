using Apex.Hrms.TestSupport;

namespace Apex.Hrms.Application.Tests.Performance;

/// <summary>
/// The generated Office samples must be byte-for-byte stable: download tests compare them with a second call, and zip
/// entries used to carry the current time, which made those comparisons fail at random.
/// </summary>
public sealed class EvidenceSamplesTests
{
    [Fact]
    public async Task Office_samples_are_identical_across_calls_even_when_time_passes()
    {
        var docx = EvidenceSamples.Docx();
        var xlsx = EvidenceSamples.Xlsx();

        // Zip timestamps have a 2-second resolution: wait past one boundary.
        await Task.Delay(TimeSpan.FromMilliseconds(2100));

        Assert.Equal(docx, EvidenceSamples.Docx());
        Assert.Equal(xlsx, EvidenceSamples.Xlsx());
    }
}
