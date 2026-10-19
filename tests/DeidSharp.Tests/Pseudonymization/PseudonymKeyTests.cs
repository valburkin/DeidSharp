using DeidSharp.Pseudonymization;

namespace DeidSharp.Tests.Pseudonymization;

public class PseudonymKeyTests
{
    [Fact]
    public void Keys_shorter_than_256_bits_are_rejected() =>
        Assert.Throws<ArgumentException>(() => PseudonymKey.FromBytes(new byte[31]));

    [Fact]
    public void Base64_keys_are_read_and_bad_base64_is_rejected()
    {
        var key = PseudonymKey.FromBase64(Convert.ToBase64String(new byte[32]));
        Assert.NotNull(key);
        Assert.Throws<ArgumentException>(() => PseudonymKey.FromBase64("not base64!!"));
        Assert.Throws<ArgumentException>(() => PseudonymKey.FromBase64(" "));
    }

    [Fact]
    public void The_key_never_shows_in_its_string_form() => Assert.Equal("PseudonymKey(***)", PseudonymKey.Generate().ToString());

    [Fact]
    public void Generated_keys_differ() =>
        Assert.NotEqual(new HmacPseudonymizer(PseudonymKey.Generate()).Digest("d", "v"), new HmacPseudonymizer(PseudonymKey.Generate()).Digest("d", "v"));

    [Fact]
    public void A_key_copies_its_bytes_so_later_changes_to_the_source_do_not_affect_it()
    {
        var bytes = new byte[32];
        var key = PseudonymKey.FromBytes(bytes);
        var before = new HmacPseudonymizer(key).Digest("d", "v");
        bytes[0] = 99;
        Assert.Equal(before, new HmacPseudonymizer(key).Digest("d", "v"));
    }
}
