using HasamiBoard.Services;
using Xunit;

namespace HasamiBoard.Tests;

public class OcrTextFormatterTests
{
    [Fact]
    public void JoinWords_RemovesSpacesBetweenFullWidthWords()
    {
        // Windows.Media.Ocr は単語ごとに空白区切りで返すため、日本語では
        // "す" "べ" "て" のように1文字ずつ分割されることが多い。
        string result = OcrTextFormatter.JoinWords(new[] { "す", "べ", "て", "クリア" });

        Assert.Equal("すべてクリア", result);
    }

    [Fact]
    public void JoinWords_KeepsSpaceBetweenHalfWidthWords()
    {
        string result = OcrTextFormatter.JoinWords(new[] { "Hello", "World" });

        Assert.Equal("Hello World", result);
    }

    [Fact]
    public void JoinWords_KeepsSpaceAtHalfWidthFullWidthBoundary()
    {
        string result = OcrTextFormatter.JoinWords(new[] { "HasamiBoard", "すべて" });

        Assert.Equal("HasamiBoard すべて", result);
    }

    [Fact]
    public void JoinWords_EmptyList_ReturnsEmptyString()
    {
        Assert.Equal(string.Empty, OcrTextFormatter.JoinWords(Array.Empty<string>()));
    }

    [Fact]
    public void JoinLines_JoinsWithNewline()
    {
        string result = OcrTextFormatter.JoinLines(new[] { "line1", "line2" });

        Assert.Equal("line1\nline2", result);
    }

    [Theory]
    [InlineData('あ', true)]
    [InlineData('ア', true)]
    [InlineData('漢', true)]
    [InlineData('　', true)]
    [InlineData('Ａ', true)]
    [InlineData('A', false)]
    [InlineData(' ', false)]
    [InlineData('1', false)]
    public void IsFullWidth_ClassifiesCharactersCorrectly(char c, bool expected)
    {
        Assert.Equal(expected, OcrTextFormatter.IsFullWidth(c));
    }

    [Fact]
    public void JoinWords_PeriodBetweenHalfWidthWords_NoSurroundingSpace()
    {
        // "abc" "." "txt" のように句読点が単独の単語として認識されるケース
        string result = OcrTextFormatter.JoinWords(new[] { "abc", ".", "txt" });

        Assert.Equal("abc.txt", result);
    }

    [Fact]
    public void JoinWords_PeriodBetweenFullWidthWords_NoSurroundingSpace()
    {
        string result = OcrTextFormatter.JoinWords(new[] { "本文", ".", "続き" });

        Assert.Equal("本文.続き", result);
    }

    [Fact]
    public void JoinWords_BackslashBetweenHalfWidthWords_NoSurroundingSpace()
    {
        // Windowsパスの "\" が独立した単語として認識されるケース
        string result = OcrTextFormatter.JoinWords(new[] { "C:", "\\", "Users" });

        Assert.Equal(@"C:\Users", result);
    }

    [Fact]
    public void JoinWords_HyphenBetweenHalfWidthWords_NoSurroundingSpace()
    {
        string result = OcrTextFormatter.JoinWords(new[] { "self", "-", "driving" });

        Assert.Equal("self-driving", result);
    }

    [Fact]
    public void JoinWords_DashBetweenFullWidthWords_IsNormalizedToChoonpuWithNoSpace()
    {
        // 全角文字に挟まれた単独の "-" は、全角長音符「ー」の誤認識とみなして補正する
        // (「ー」自体も句読点扱いのため前後に空白は入らない)
        string result = OcrTextFormatter.JoinWords(new[] { "とても", "-", "良い" });

        Assert.Equal("とてもー良い", result);
    }

    [Fact]
    public void JoinWords_DashBetweenHalfWidthAndFullWidth_IsNotNormalized()
    {
        // 片側が半角の場合は誤認識の可能性が低いため、そのまま "-" を維持する
        string result = OcrTextFormatter.JoinWords(new[] { "Word", "-", "です" });

        Assert.Equal("Word-です", result);
    }

    [Fact]
    public void NormalizeFilePaths_YenSignAfterDriveLetter_IsNormalizedToBackslash()
    {
        string result = OcrTextFormatter.NormalizeFilePaths("D:¥work¥HasamiBoard");

        Assert.Equal(@"D:\work\HasamiBoard", result);
    }

    [Fact]
    public void NormalizeFilePaths_PercentSignsAfterDriveLetter_AreNormalizedToBackslash()
    {
        string result = OcrTextFormatter.NormalizeFilePaths("D:%work_tosgit%HasamiBoard%HasamiBoard%images");

        Assert.Equal(@"D:\work_tosgit\HasamiBoard\HasamiBoard\images", result);
    }

    [Fact]
    public void NormalizeFilePaths_ReproducesReportedGarbledPath_FixesSeparatorsOnly()
    {
        // 実際に報告された誤認識例。区切り記号の "%" は "\" へ正規化されるが、
        // "i" が単独トークンとして分割されたことによる余分な空白は、この処理の対象外
        // (元の1単語がどこで区切られるべきだったかは文字列からは判別できないため)。
        string input = "D:%work_tosg i t%Hasam i Board%Hasam i Board% i mages[EOF]";

        string result = OcrTextFormatter.NormalizeFilePaths(input);

        Assert.Equal(@"D:\work_tosg i t\Hasam i Board\Hasam i Board\ i mages[EOF]", result);
    }

    [Fact]
    public void NormalizeFilePaths_NoDriveLetter_LeavesPercentSignUntouched()
    {
        string result = OcrTextFormatter.NormalizeFilePaths("50% off today");

        Assert.Equal("50% off today", result);
    }

    [Fact]
    public void NormalizeFilePaths_StopsAtTerminatorPunctuation_LeavesLaterPercentUntouched()
    {
        string result = OcrTextFormatter.NormalizeFilePaths("D:%foo、50%引き");

        Assert.Equal("D:\\foo、50%引き", result);
    }

    [Fact]
    public void NormalizeFilePaths_SentenceEndingPeriodAfterPath_LeavesLaterPercentUntouched()
    {
        // パス直後の文末の句点 (直後に空白) でパス走査を打ち切り、無関係な後続文中の
        // "%" (例: "45%") までパスの一部として誤って正規化してしまわないようにする。
        string result = OcrTextFormatter.NormalizeFilePaths(
            @"The file is at C:¥Users¥bob¥file.txt. It contains 45% of data.");

        Assert.Equal(
            @"The file is at C:\Users\bob\file.txt. It contains 45% of data.",
            result);
    }

    [Fact]
    public void NormalizeFilePaths_ExtensionPeriodWithNoTrailingSpace_IsTreatedAsPartOfPath()
    {
        // "file.txt" のような拡張子区切りの "." (直後に空白なし) は文末とみなさず、
        // その後に続く区切り記号も引き続きパスの一部として正規化する。
        string result = OcrTextFormatter.NormalizeFilePaths(@"C:%Users%bob%file.txt%backup");

        Assert.Equal(@"C:\Users\bob\file.txt\backup", result);
    }

    [Theory]
    [InlineData('.', true)]
    [InlineData(',', true)]
    [InlineData('-', true)]
    [InlineData('\\', true)]
    [InlineData('ー', true)]
    [InlineData('。', true)]
    [InlineData('あ', false)]
    [InlineData('A', false)]
    public void IsNoSpacePunctuation_ClassifiesCharactersCorrectly(char c, bool expected)
    {
        Assert.Equal(expected, OcrTextFormatter.IsNoSpacePunctuation(c));
    }
}
