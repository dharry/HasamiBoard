using HasamiBoard.Services;
using Xunit;

namespace HasamiBoard.Tests;

public class MemoTemplateExpanderTests
{
    private static readonly DateTime SampleDate = new(2026, 3, 5, 9, 7, 3);

    [Fact]
    public void Expand_ReplacesDatePlaceholder()
    {
        string result = MemoTemplateExpander.Expand("Date: {date}", SampleDate);

        Assert.Equal("Date: 2026/03/05", result);
    }

    [Fact]
    public void Expand_ReplacesTimePlaceholder()
    {
        string result = MemoTemplateExpander.Expand("Time: {time}", SampleDate);

        Assert.Equal("Time: 09:07:03", result);
    }

    [Fact]
    public void Expand_ReplacesDatetimePlaceholder_WithoutLeavingDateOrTimeTokens()
    {
        string result = MemoTemplateExpander.Expand("{datetime}", SampleDate);

        Assert.Equal("2026/03/05 09:07:03", result);
        Assert.DoesNotContain("{date}", result);
        Assert.DoesNotContain("{time}", result);
    }

    [Fact]
    public void Expand_ReplacesYearMonthDayPlaceholders()
    {
        string result = MemoTemplateExpander.Expand("{year}-{month}-{day}", SampleDate);

        Assert.Equal("2026-03-05", result);
    }

    [Fact]
    public void Expand_ReplacesMultipleOccurrencesOfSamePlaceholder()
    {
        string result = MemoTemplateExpander.Expand("{date} to {date}", SampleDate);

        Assert.Equal("2026/03/05 to 2026/03/05", result);
    }

    [Fact]
    public void Expand_NoPlaceholders_ReturnsTextUnchanged()
    {
        string result = MemoTemplateExpander.Expand("Plain memo body with no tokens.", SampleDate);

        Assert.Equal("Plain memo body with no tokens.", result);
    }

    [Fact]
    public void Expand_EmptyTemplate_ReturnsEmptyString()
    {
        Assert.Equal(string.Empty, MemoTemplateExpander.Expand(string.Empty, SampleDate));
    }

    [Fact]
    public void Expand_UnrecognizedBraceToken_IsLeftUntouched()
    {
        string result = MemoTemplateExpander.Expand("{unknown}", SampleDate);

        Assert.Equal("{unknown}", result);
    }
}
