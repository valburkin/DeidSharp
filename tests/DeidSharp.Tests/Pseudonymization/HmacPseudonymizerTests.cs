using DeidSharp.Pseudonymization;
using DeidSharp.Tests.TestDoubles;

namespace DeidSharp.Tests.Pseudonymization;

public class HmacPseudonymizerTests
{
    private static readonly HmacPseudonymizer Fixed = new(TestKeys.Fixed);

    [Fact]
    public void The_same_domain_and_value_always_give_the_same_digest() =>
        Assert.Equal(Fixed.Digest("mrn", "123"), Fixed.Digest("mrn", "123"));

    [Fact]
    public void A_different_key_gives_an_unrelated_digest() =>
        Assert.NotEqual(Fixed.Digest("mrn", "123"), new HmacPseudonymizer(TestKeys.Other).Digest("mrn", "123"));

    [Fact]
    public void Domains_separate_identical_values() => Assert.NotEqual(Fixed.Digest("mrn", "123"), Fixed.Digest("phone", "123"));

    [Fact]
    public void Shifting_text_between_domain_and_value_does_not_collide() => Assert.NotEqual(Fixed.Digest("ab", "c"), Fixed.Digest("a", "bc"));

    [Fact]
    public void A_domain_containing_the_separator_is_rejected() =>
        Assert.Throws<ArgumentException>(() => Fixed.Digest("a\u001Fb", "c"));

    [Fact]
    public void Digests_are_32_bytes_and_empty_values_are_allowed() => Assert.Equal(32, Fixed.Digest("d", "").Length);

    [Fact]
    public void Rejects_null_arguments()
    {
        Assert.Throws<ArgumentNullException>(() => new HmacPseudonymizer(null!));
        Assert.Throws<ArgumentNullException>(() => Fixed.Digest(null!, "x"));
        Assert.Throws<ArgumentNullException>(() => Fixed.Digest("d", null!));
    }
}
