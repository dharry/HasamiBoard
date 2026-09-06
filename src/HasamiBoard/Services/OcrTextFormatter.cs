using System.Text;
using System.Text.RegularExpressions;

namespace HasamiBoard.Services;

/// <summary>
/// Windows.Media.Ocr の認識結果を整形する。
/// 同API は単語ごとに半角スペースで連結するため、日本語等の全角文字が続く場合でも
/// 不要な空白が挿入されてしまう。また句読点・記号が独立した単語として認識され、
/// その前後にまで空白が入ってしまうことや、全角長音符「ー」が半角ハイフン「-」に
/// 誤認識されることが多いため、あわせて補正する。
/// </summary>
public static class OcrTextFormatter
{
    private static readonly HashSet<char> NoSpacePunctuation = new()
    {
        '.', ',', ';', ':', '!', '?', '(', ')', '[', ']', '{', '}', '\'', '"', '-', '_', '/', '\\', '~',
        '、', '。', '！', '？', '：', '；', '（', '）', '［', '］', '｛', '｝', '「', '」', '『', '』',
        '・', 'ー', '〜', '～', '…',
    };

    /// <summary>OCR単語列を空白の要不要を判定しながら1行の文字列に連結する。</summary>
    /// <param name="words">OCR認識単語リスト</param>
    /// <returns>空白を調整して連結した文字列</returns>
    public static string JoinWords(IReadOnlyList<string> words)
    {
        if (words.Count == 0)
        {
            return string.Empty;
        }

        var normalized = NormalizeDashes(words);

        var sb = new StringBuilder();
        string? previous = null;

        foreach (string word in normalized)
        {
            if (!string.IsNullOrEmpty(previous) && !string.IsNullOrEmpty(word))
            {
                char lastChar = previous[^1];
                char firstChar = word[0];
                if (NeedsSpace(lastChar, firstChar))
                {
                    sb.Append(' ');
                }
            }

            sb.Append(word);
            previous = word;
        }

        return sb.ToString();
    }

    /// <summary>複数行を改行文字で連結する。</summary>
    /// <param name="lines">改行で連結する文字列行</param>
    /// <returns>改行文字で連結した文字列</returns>
    public static string JoinLines(IEnumerable<string> lines) => string.Join("\n", lines);

    // "C:" "D:" のようなドライブレター表記の直後に続く区切り記号。
    // "\" 本来の記号に加え、OCRで誤認識されやすい全角/半角の "¥" "%" も対象に含める。
    private static readonly Regex DriveLetterPathStartRegex = new(@"\b[A-Za-z]:[%¥\\/]", RegexOptions.Compiled);

    // パスの終端とみなす句読点 (これが現れたらパス範囲の走査を打ち切る)
    private static readonly HashSet<char> PathTerminators = new()
    {
        ',', ';', '!', '?', '、', '。', '！', '？', '，', '；',
    };

    /// <summary>
    /// "C:\" や "D:¥" のようなドライブレター区切りのWindowsパスは、OCRでは区切り文字の
    /// "\" が全角/半角の "¥" や "%" に誤認識されることが多い。ドライブレター表記から
    /// 始まる範囲を検出できた場合に限り、その範囲内の "¥" "%" を "\" へ正規化する。
    /// (Unixスタイルの "/path/to/x" は "/" 自体が誤認識されにくく、既存の空白抑制ルールで
    /// 問題なく扱えるため、ここでは対応しない)
    /// </summary>
    /// <param name="line">正規化する文字列行</param>
    /// <returns>正規化されたファイルパス文字列</returns>
    public static string NormalizeFilePaths(string line)
    {
        if (string.IsNullOrEmpty(line))
        {
            return line;
        }

        char[] chars = line.ToCharArray();
        int searchStart = 0;

        while (searchStart < chars.Length)
        {
            var match = DriveLetterPathStartRegex.Match(new string(chars), searchStart);
            if (!match.Success)
            {
                break;
            }

            int pathEnd = chars.Length;
            for (int i = match.Index; i < chars.Length; i++)
            {
                if (PathTerminators.Contains(chars[i]))
                {
                    pathEnd = i;
                    break;
                }

                // 文末の句点 "." (直後が空白または行末) はパスの一部ではなく文の終端とみなす。
                // ただしファイル名中の拡張子区切り "file.txt" のような "." (直後に空白が続かない)
                // はパスの一部として扱い続ける。
                if (chars[i] == '.' && (i + 1 >= chars.Length || char.IsWhiteSpace(chars[i + 1])))
                {
                    pathEnd = i;
                    break;
                }
            }

            for (int i = match.Index; i < pathEnd; i++)
            {
                if (chars[i] is '%' or '¥')
                {
                    chars[i] = '\\';
                }
            }

            searchStart = pathEnd;
        }

        return new string(chars);
    }

    /// <summary>2文字の間に空白が必要かどうか。句読点・記号が絡む境界には空白を入れない。</summary>
    /// <param name="left">左側の文字</param>
    /// <param name="right">右側の文字</param>
    /// <returns>空白が必要な場合true</returns>
    public static bool NeedsSpace(char left, char right)
    {
        if (IsNoSpacePunctuation(left) || IsNoSpacePunctuation(right))
        {
            return false;
        }

        return !(IsFullWidth(left) && IsFullWidth(right));
    }

    /// <summary>スペースを前後に置かず直接連結すべき句読点・記号かどうか。</summary>
    /// <param name="c">判定対象の文字</param>
    /// <returns>スペース不要な句読点の場合true</returns>
    public static bool IsNoSpacePunctuation(char c) => NoSpacePunctuation.Contains(c);

    /// <summary>全角文字 (CJK統合漢字・拡張A・ひらがな・カタカナ・CJK記号/句読点・CJK互換漢字・全角形) かどうか。</summary>
    /// <param name="c">判定対象の文字</param>
    /// <returns>全角文字の場合true</returns>
    public static bool IsFullWidth(char c) =>
        (c >= '　' && c <= 'ヿ') ||
        (c >= '㐀' && c <= '䶿') ||
        (c >= '一' && c <= '鿿') ||
        (c >= '豈' && c <= '﫿') ||
        (c >= '＀' && c <= '￯');

    /// <summary>
    /// 全角文字に挟まれた単独の "-" (半角ハイフン) は、全角長音符「ー」の誤認識である
    /// 可能性が高いため「ー」に置き換える。
    /// </summary>
    private static IReadOnlyList<string> NormalizeDashes(IReadOnlyList<string> words)
    {
        if (words.Count < 3)
        {
            return words;
        }

        List<string>? result = null;

        for (int i = 1; i < words.Count - 1; i++)
        {
            string word = words[i];
            if (word.Length == 0 || word.Any(c => c != '-'))
            {
                continue;
            }

            string prev = words[i - 1];
            string next = words[i + 1];
            if (prev.Length == 0 || next.Length == 0 ||
                !IsFullWidth(prev[^1]) || !IsFullWidth(next[0]))
            {
                continue;
            }

            result ??= new List<string>(words);
            result[i] = new string('ー', word.Length);
        }

        return result ?? words;
    }
}
