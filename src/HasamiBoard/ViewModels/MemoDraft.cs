namespace HasamiBoard.ViewModels;

/// <summary>他機能（スクリーンショット・クリップボード履歴等）から新規メモを開く際の初期本文・モード・タグ。</summary>
public record MemoDraft(string Body, bool IsMarkdown, IReadOnlyList<string>? Tags = null);
