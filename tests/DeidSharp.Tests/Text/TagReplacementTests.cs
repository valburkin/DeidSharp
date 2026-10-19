using DeidSharp.Text;

namespace DeidSharp.Tests.Text;

public class TagReplacementTests
{
    [Fact]
    public void Every_category_has_its_own_tag()
    {
        var tags = Enum.GetValues<PhiCategory>().Select(TagReplacement.TagName).ToList();
        Assert.DoesNotContain("PHI", tags);
        Assert.Equal(tags.Count, tags.Distinct().Count());
    }

    [Theory]
    [InlineData(PhiCategory.Name, "[NAME]")]
    [InlineData(PhiCategory.MedicalRecordNumber, "[MRN]")]
    [InlineData(PhiCategory.Date, "[DATE]")]
    public void Writes_the_tag_in_brackets(PhiCategory category, string expected) =>
        Assert.Equal(expected, new TagReplacement().Replace(new PhiSpan(0, 1, category, 1, "t"), "x", TextContext.None));

    [Fact]
    public void The_format_is_configurable() =>
        Assert.Equal("<<NAME>>", new TagReplacement("<<{0}>>").Replace(new PhiSpan(0, 1, PhiCategory.Name, 1, "t"), "x", TextContext.None));

    [Theory]
    [InlineData("")]
    [InlineData("[NAME]")]
    [InlineData("{0} {1}")]
    public void Rejects_a_format_without_exactly_one_placeholder(string format) =>
        Assert.ThrowsAny<ArgumentException>(() => new TagReplacement(format));

    [Fact]
    public void The_original_text_never_appears_in_the_tag() =>
        Assert.DoesNotContain("Jane", new TagReplacement().Replace(new PhiSpan(0, 4, PhiCategory.Name, 1, "t"), "Jane", TextContext.None), StringComparison.Ordinal);
}
