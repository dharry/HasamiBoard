using System.IO;
using HasamiBoard.Services;
using Xunit;

namespace HasamiBoard.Tests;

public class QuickFilterMatcherTests
{
    [Fact]
    public void EmptyFilter_MatchesEverything()
    {
        Assert.True(QuickFilterMatcher.Matches("", "any text", Array.Empty<string>()));
        Assert.True(QuickFilterMatcher.Matches("   ", "any text", Array.Empty<string>()));
    }

    [Theory]
    [InlineData("hello", "Hello World", true)]
    [InlineData("HELLO", "hello world", true)]
    [InlineData("missing", "hello world", false)]
    public void PlainKeyword_IsCaseInsensitiveSubstringMatch(string filter, string text, bool expected)
    {
        Assert.Equal(expected, QuickFilterMatcher.Matches(filter, text, Array.Empty<string>()));
    }

    [Fact]
    public void MultipleKeywords_RequireAllToMatch_And()
    {
        Assert.True(QuickFilterMatcher.Matches("foo bar", "foo and bar together", Array.Empty<string>()));
        Assert.False(QuickFilterMatcher.Matches("foo baz", "foo and bar together", Array.Empty<string>()));
    }

    [Fact]
    public void TagToken_MatchesExactTagNameCaseInsensitive()
    {
        var tags = new[] { "重要", "ColorPicker" };
        Assert.True(QuickFilterMatcher.Matches("#重要", "text", tags));
        Assert.True(QuickFilterMatcher.Matches("#colorpicker", "text", tags));
        Assert.False(QuickFilterMatcher.Matches("#未知", "text", tags));
    }

    [Fact]
    public void TagToken_DoesNotSubstringMatchTagNames()
    {
        // "#重" は "重要" タグの部分一致であってはならない (完全一致のみ)
        var tags = new[] { "重要" };
        Assert.False(QuickFilterMatcher.Matches("#重", "text", tags));
    }

    [Fact]
    public void MixedKeywordAndTag_BothMustMatch()
    {
        var tags = new[] { "重要" };
        Assert.True(QuickFilterMatcher.Matches("hello #重要", "hello world", tags));
        Assert.False(QuickFilterMatcher.Matches("hello #重要", "goodbye world", tags));
        Assert.False(QuickFilterMatcher.Matches("hello #重要", "hello world", Array.Empty<string>()));
    }
}
