using DeidSharp.Pseudonymization;
using DeidSharp.Tests.TestDoubles;

namespace DeidSharp.Tests.Pseudonymization;

public class PseudonymFormatsTests
{
    private static readonly byte[] Digest = new HmacPseudonymizer(TestKeys.Fixed).Digest("d", "v");

    [Fact]
    public void Hex_is_lowercase_and_of_the_requested_length()
    {
        Assert.Matches("^[0-9a-f]{12}$", PseudonymFormats.Hex(Digest, 12));
        Assert.Equal(64, PseudonymFormats.Hex(Digest, 64).Length);
        Assert.Throws<ArgumentOutOfRangeException>(() => PseudonymFormats.Hex(Digest, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => PseudonymFormats.Hex(Digest, 65));
    }

    [Fact]
    public void Uuids_are_shaped_as_version_4()
    {
        var text = PseudonymFormats.Uuid(Digest).ToString("D");
        Assert.Matches("^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$", text);
        Assert.Throws<ArgumentOutOfRangeException>(() => PseudonymFormats.Uuid(new byte[15]));
    }

    [Fact]
    public void Indices_stay_within_range()
    {
        for (var max = 1; max < 500; max += 7)
        {
            Assert.InRange(PseudonymFormats.Index(Digest, max), 0, max - 1);
        }

        Assert.Throws<ArgumentOutOfRangeException>(() => PseudonymFormats.Index(Digest, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => PseudonymFormats.Index(Digest, 10, 25));
        Assert.Throws<ArgumentOutOfRangeException>(() => PseudonymFormats.Index(Digest, 10, -1));
    }
}
