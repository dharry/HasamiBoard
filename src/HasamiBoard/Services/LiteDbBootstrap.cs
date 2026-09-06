using HasamiBoard.Models;
using LiteDB;

namespace HasamiBoard.Services;

/// <summary>
/// LiteDB の BsonMapper グローバル設定。アプリ起動時に一度だけ Configure() を呼び出すこと。
/// </summary>
public static class LiteDbBootstrap
{
    private static bool _configured;

    /// <summary>LiteDBのグローバルBsonMapper設定を初回のみ適用する。</summary>
    public static void Configure()
    {
        if (_configured)
        {
            return;
        }

        _configured = true;

        // MemoMode: 新規保存時は "Text"/"Markdown" で統一するが、
        // 旧バージョンが保存した日本語表記 ("テキスト"/"マークダウン") も読み込めるようにする。
        BsonMapper.Global.RegisterType(
            serialize: (MemoMode mode) => mode.ToString(),
            deserialize: bson =>
            {
                string value = bson.AsString;
                return value switch
                {
                    "マークダウン" or "Markdown" => MemoMode.Markdown,
                    _ => MemoMode.Text,
                };
            });
    }
}
