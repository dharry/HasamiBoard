using System.IO;
using HasamiBoard.Models;
using LiteDB;

namespace HasamiBoard.Services;

/// <summary>クリップボード履歴をLiteDBへ永続化するリポジトリ。</summary>
public class ClipboardHistoryRepository : IDisposable
{
    private const string CollectionName = "clipboard_items";

    private readonly LiteDatabase _db;
    private readonly ILiteCollection<ClipboardItem> _collection;

    /// <summary>データベースファイルを開き、コレクションとインデックスを準備する。</summary>
    /// <param name="dataFolder">データフォルダパス（nullの場合は既定値を使用）</param>
    public ClipboardHistoryRepository(string? dataFolder = null)
    {
        string dbPath = Path.Combine(dataFolder ?? AppPaths.DataFolder, "ClipboardHistory.db");
        // Shared接続にすることで、アプリ実行中でもバックアップ機能等が.dbファイルを
        // 外部から読み取れるようにする (既定のDirect接続は排他ロックのため読めない)。
        _db = new LiteDatabase(new ConnectionString(dbPath) { Connection = ConnectionType.Shared });
        _collection = _db.GetCollection<ClipboardItem>(CollectionName);
        _collection.EnsureIndex(x => x.CreatedAt);
    }

    /// <summary>全件を作成日時の新しい順に取得する。</summary>
    /// <returns>クリップボード項目リスト</returns>
    public List<ClipboardItem> GetAll()
        => _collection.Query().OrderByDescending(x => x.CreatedAt).ToList();

    /// <summary>本文テキストが一致する項目を検索する。</summary>
    /// <param name="text">検索対象のテキスト</param>
    /// <returns>一致する項目、見つからない場合はnull</returns>
    public ClipboardItem? FindByText(string text)
        => _collection.FindOne(x => x.Text == text);

    /// <summary>項目を新規追加する。IDが未設定なら自動採番する。</summary>
    /// <param name="item">追加するクリップボード項目</param>
    public void Insert(ClipboardItem item)
    {
        if (string.IsNullOrEmpty(item.Id))
        {
            item.Id = Guid.NewGuid().ToString();
        }

        _collection.Insert(item);
    }

    /// <summary>既存項目を更新する。</summary>
    /// <param name="item">更新するクリップボード項目</param>
    public void Update(ClipboardItem item)
        => _collection.Update(item);

    /// <summary>指定IDの項目を削除する。</summary>
    /// <param name="id">削除対象の項目ID</param>
    public void Delete(string id)
        => _collection.Delete(id);

    /// <summary>複数IDの項目をまとめて削除する。</summary>
    /// <param name="ids">削除対象の項目ID列挙</param>
    public void DeleteMany(IEnumerable<string> ids)
    {
        foreach (string id in ids)
        {
            _collection.Delete(id);
        }
    }

    /// <summary>登録件数を取得する。</summary>
    /// <returns>クリップボード履歴の登録件数</returns>
    public int Count()
        => _collection.Count();

    /// <summary>保持上限を超えた、削除候補の非ピン留め項目を取得する。</summary>
    /// <param name="keepCount">保持する項目の件数</param>
    /// <returns>削除対象の非ピン留め項目リスト</returns>
    public List<ClipboardItem> GetTrimCandidates(int keepCount)
        => _collection.Query()
            .Where(x => x.IsPinned == false)
            .OrderByDescending(x => x.CreatedAt)
            .Offset(keepCount)
            .ToList();

    /// <summary>データベース接続を解放する。</summary>
    public void Dispose()
    {
        _db.Dispose();
    }
}
