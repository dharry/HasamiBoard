namespace HasamiBoard.Services;

/// <summary>
/// クイックフィルターの共通ロジック。スペース区切りの複数キーワードと "#タグ名" をAND評価する。
/// クリップボード履歴/スクリーンショット/メモの全タブで共通利用する。
/// </summary>
public static class QuickFilterMatcher
{
    /// <summary>フィルター文字列がテキスト・タグにAND一致するか判定する。</summary>
    /// <param name="filterText">フィルター条件文字列</param>
    /// <param name="searchableText">検索対象テキスト</param>
    /// <param name="tags">付与されたタグ一覧</param>
    /// <returns>一致する場合true</returns>
    public static bool Matches(string filterText, string searchableText, IEnumerable<string> tags)
    {
        if (string.IsNullOrWhiteSpace(filterText))
        {
            return true;
        }

        var tagList = tags.ToList();
        var tokens = filterText.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (string token in tokens)
        {
            if (token.StartsWith('#'))
            {
                string tagName = token[1..];
                if (tagName.Length == 0)
                {
                    continue;
                }

                bool hasTag = tagList.Any(t => string.Equals(t, tagName, StringComparison.OrdinalIgnoreCase));
                if (!hasTag)
                {
                    return false;
                }
            }
            else
            {
                bool containsText = searchableText.Contains(token, StringComparison.OrdinalIgnoreCase);
                if (!containsText)
                {
                    return false;
                }
            }
        }

        return true;
    }
}
