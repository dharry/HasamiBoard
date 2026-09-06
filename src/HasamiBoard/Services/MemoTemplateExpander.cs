namespace HasamiBoard.Services;

/// <summary>
/// メモテンプレート本文に含まれる予約文字 ({date}, {time} 等) を、
/// テンプレート適用時点の日時に展開する。
/// </summary>
public static class MemoTemplateExpander
{
    /// <summary>テンプレート文字列内の予約プレースホルダーを日時文字列に置換する。</summary>
    /// <param name="template">テンプレート文字列</param>
    /// <param name="now">展開に使用する基準日時</param>
    /// <returns>展開後のテンプレート文字列</returns>
    public static string Expand(string template, DateTime now)
    {
        return (template ?? string.Empty)
            .Replace("{datetime}", now.ToString("yyyy/MM/dd HH:mm:ss"))
            .Replace("{date}", now.ToString("yyyy/MM/dd"))
            .Replace("{time}", now.ToString("HH:mm:ss"))
            .Replace("{year}", now.ToString("yyyy"))
            .Replace("{month}", now.ToString("MM"))
            .Replace("{day}", now.ToString("dd"));
    }
}
