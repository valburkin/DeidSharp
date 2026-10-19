using DeidSharp.Presidio;

namespace DeidSharp.Tests.Presidio;

public class CodePointIndexTests
{
    [Fact]
    public void Plain_text_maps_one_to_one()
    {
        var index = new CodePointIndex("hello");
        Assert.Equal(0, index.ToUtf16(0));
        Assert.Equal(5, index.ToUtf16(5));
        Assert.Equal(-1, index.ToUtf16(6));
        Assert.Equal(-1, index.ToUtf16(-1));
    }

    [Fact]
    public void Each_astral_character_shifts_later_offsets_by_one()
    {
        // code points: 😀 ␠ a 😀 b
        var index = new CodePointIndex("\U0001F600 a\U0001F600b");
        Assert.Equal([0, 2, 3, 4, 6, 7], Enumerable.Range(0, 6).Select(index.ToUtf16));
        Assert.Equal(-1, index.ToUtf16(6));
    }

    [Fact]
    public void A_lone_surrogate_counts_as_one_code_point_as_python_counts_it()
    {
        var index = new CodePointIndex("a\uD83Db");
        Assert.Equal([0, 1, 2, 3], Enumerable.Range(0, 4).Select(index.ToUtf16));
    }

    [Fact]
    public void Empty_text_has_only_offset_zero()
    {
        var index = new CodePointIndex("");
        Assert.Equal(0, index.ToUtf16(0));
        Assert.Equal(-1, index.ToUtf16(1));
    }
}
