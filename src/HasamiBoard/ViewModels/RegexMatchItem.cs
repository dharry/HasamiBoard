namespace HasamiBoard.ViewModels;

/// <summary>正規表現テスターの1マッチ分の表示用データ。</summary>
public record RegexMatchItem(string Header, string Value, int Index, IReadOnlyList<RegexGroupItem> Groups);
