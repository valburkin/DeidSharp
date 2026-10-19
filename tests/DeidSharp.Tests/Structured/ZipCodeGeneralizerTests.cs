using DeidSharp.Structured;

namespace DeidSharp.Tests.Structured;

public class ZipCodeGeneralizerTests
{
    private static readonly ZipCodeGeneralizer SafeHarbor = ZipCodeGeneralizer.SafeHarbor;

    [Theory]
    [InlineData("02115", "021")]
    [InlineData("02115-1234", "021")]
    [InlineData("021151234", "021")]
    [InlineData(" 90210 ", "902")]
    public void Keeps_the_first_three_digits(string zip, string expected) => Assert.Equal(expected, SafeHarbor.Generalize(zip));

    [Fact]
    public void Sparse_areas_become_000()
    {
        foreach (var prefix in ZipCodeGeneralizer.Census2000SparsePrefixes)
        {
            Assert.Equal("000", SafeHarbor.Generalize(prefix + "01"));
        }

        Assert.Equal(17, ZipCodeGeneralizer.Census2000SparsePrefixes.Count);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("0211")]
    [InlineData("021156")]
    [InlineData("02I15")]
    [InlineData("K1A 0B1")]
    [InlineData("SW1A 1AA")]
    [InlineData("02115-12")]
    public void Anything_that_is_not_a_us_zip_gives_null_so_the_caller_drops_it(string? zip) => Assert.Null(SafeHarbor.Generalize(zip));

    [Fact]
    public void Takes_an_updated_list_of_sparse_areas()
    {
        var generalizer = new ZipCodeGeneralizer(["021"]);
        Assert.Equal("000", generalizer.Generalize("02115"));
        Assert.Equal("036", generalizer.Generalize("03601"));
    }

    [Theory]
    [InlineData("21")]
    [InlineData("0211")]
    [InlineData("abc")]
    public void Rejects_a_malformed_sparse_list(string prefix) => Assert.Throws<ArgumentException>(() => new ZipCodeGeneralizer([prefix]));
}
