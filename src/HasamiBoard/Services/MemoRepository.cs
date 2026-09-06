using System.IO;
using HasamiBoard.Models;
using LiteDB;

namespace HasamiBoard.Services;

/// <summary>メモをLiteDBへ永続化するリポジトリ。</summary>
public class MemoRepository : IDisposable
{
    private const string CollectionName = "memo_items";

    private readonly LiteDatabase _db;
    private readonly ILiteCollection<MemoItem> _collection;

    /// <summary>データベースファイルを開き、コレクションとインデックスを準備する。</summary>
    /// <param name="dataFolder">データフォルダパス（nullの場合は既定値を使用）</param>
    public MemoRepository(string? dataFolder = null)
    {
        string dbPath = Path.Combine(dataFolder ?? AppPaths.DataFolder, "Memos.db");
        // Shared接続にすることで、アプリ実行中でもバックアップ機能等が.dbファイルを
        // 外部から読み取れるようにする (既定のDirect接続は排他ロックのため読めない)。
        _db = new LiteDatabase(new ConnectionString(dbPath) { Connection = ConnectionType.Shared });
        _collection = _db.GetCollection<MemoItem>(CollectionName);
        _collection.EnsureIndex(x => x.CreatedAt);
    }

    /// <summary>全件を作成日時の新しい順に取得する。</summary>
    /// <returns>メモ項目リスト</returns>
    public List<MemoItem> GetAll()
        => _collection.Query().OrderByDescending(x => x.CreatedAt).ToList();

    /// <summary>メモを新規追加する。IDが未設定なら自動採番する。</summary>
    /// <param name="item">追加するメモ項目</param>
    public void Insert(MemoItem item)
    {
        if (string.IsNullOrEmpty(item.Id))
        {
            item.Id = Guid.NewGuid().ToString();
        }

        _collection.Insert(item);
    }

    /// <summary>既存メモを更新する。</summary>
    /// <param name="item">更新するメモ項目</param>
    public void Update(MemoItem item)
        => _collection.Update(item);

    /// <summary>指定IDのメモを削除する。</summary>
    /// <param name="id">削除対象のメモID</param>
    public void Delete(string id)
        => _collection.Delete(id);

    /// <summary>データベース接続を解放する。</summary>
    public void Dispose()
    {
        _db.Dispose();
    }
}
