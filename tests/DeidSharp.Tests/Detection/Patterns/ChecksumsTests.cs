using DeidSharp.Detection.Patterns;

namespace DeidSharp.Tests.Detection.Patterns;

public class ChecksumsTests
{
    [Theory]
    [InlineData("4111111111111111", true)]
    [InlineData("4111 1111 1111 1111", true)]
    [InlineData("79927398713", true)]
    [InlineData("4111111111111112", false)]
    [InlineData("79927398710", false)]
    [InlineData("0", false)]
    [InlineData("", false)]
    [InlineData("abc", false)]
    public void Luhn(string value, bool expected) => Assert.Equal(expected, Checksums.IsValidLuhn(value));

    [Theory]
    [InlineData("1M8GDM9AXKP042788", true)]
    [InlineData("1HGCM82633A004352", true)]
    [InlineData("1hgcm82633a004352", true)]
    [InlineData("1HGCM82634A004352", false)]
    [InlineData("1HGCM8263IA004352", false)]
    [InlineData("1HGCM82633A00435", false)]
    [InlineData("1HGCM82633A0043521", false)]
    public void Vin(string value, bool expected) => Assert.Equal(expected, Checksums.IsValidVin(value));
}
