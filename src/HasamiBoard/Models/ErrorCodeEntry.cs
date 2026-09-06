namespace HasamiBoard.Models;

/// <summary>エラーメッセージ一覧の1行分のデータ (コード、マクロ名、英語/日本語の説明文)。</summary>
public sealed record ErrorCodeEntry(string Code, string? MacroName, string DescriptionEn, string DescriptionJa);
