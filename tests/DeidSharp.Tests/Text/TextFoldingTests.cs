using DeidSharp.Text;

namespace DeidSharp.Tests.Text;

public class TextFoldingTests
{
    [Theory]
    [InlineData("José Álvarez-Núñez", "Jose Alvarez-Nunez")]
    [InlineData("Müller Strauß Øster Æsir", "Muller Straus Oster Asir")]
    [InlineData("Łukasz Dvořák Şahin Ţăran", "Lukasz Dvorak Sahin Taran")]
    [InlineData("O’Brien ‘quoted’ ʼokina", "O'Brien 'quoted' 'okina")]
    [InlineData("a–b—c‐d−e", "a-b-c-d-e")]
    [InlineData("a b c　d", "a b c d")]
    [InlineData("plain ASCII 123 !?", "plain ASCII 123 !?")]
    public void Folds_to_plain_letters_and_punctuation(string input, string expected) => Assert.Equal(expected, TextFolding.Fold(input));

    [Theory]
    [InlineData("Ünïcödé")]
    [InlineData("\U0001F600 emoji \U0001F468‍\U0001F469")]
    [InlineData("é decomposed")]
    [InlineData("中文 العربية עברית")]
    [InlineData("")]
    public void Never_changes_the_length(string input) => Assert.Equal(input.Length, TextFolding.Fold(input).Length);

    [Fact]
    public void Leaves_characters_outside_its_table_alone()
    {
        const string text = "中文 \U0001F600 ñ";
        var folded = TextFolding.Fold(text);
        Assert.Equal("中文 \U0001F600 n", folded);
    }

    [Fact]
    public void FoldValue_also_strips_combining_marks() => Assert.Equal("Jose", TextFolding.FoldValue("José"));

    [Fact]
    public void Null_is_rejected()
    {
        Assert.Throws<ArgumentNullException>(() => TextFolding.Fold(null!));
        Assert.Throws<ArgumentNullException>(() => TextFolding.FoldValue(null!));
    }
}
