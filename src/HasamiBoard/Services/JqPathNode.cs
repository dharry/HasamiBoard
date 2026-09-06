namespace HasamiBoard.Services;

/// <summary>jqテストのキーパス・ナビゲーターで表示する、JSON構造上の1ノード (WPF非依存の純粋なデータ)。</summary>
/// <param name="Label">ツリー表示用のラベル (例: "name: &quot;Alice&quot;", "items [3]")</param>
/// <param name="JqPath">クリック時にフィルタ欄へ挿入するjqパス文字列 (例: ".store.book[0].title")</param>
/// <param name="Children">子ノード一覧 (末端ノードは空)</param>
public sealed record JqPathNode(string Label, string JqPath, IReadOnlyList<JqPathNode> Children);
