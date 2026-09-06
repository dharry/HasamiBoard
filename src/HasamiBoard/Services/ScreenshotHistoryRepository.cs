using System.IO;
using HasamiBoard.Models;
using LiteDB;

namespace HasamiBoard.Services;

/// <summary>スクリーンショット履歴をLiteDBに永続化するリポジトリ。</summary>
public class ScreenshotHistoryRepository : IDisposable
{
    private const string CollectionName = "screenshot_items";

    private readonly LiteDatabase _db;
    private readonly ILiteCollection<ScreenshotItem> _collection;

    /// <summary>データベースファイルを開き、コレクションとインデックスを準備する。</summary>
    /// <param name="dataFolder">データフォルダパス（nullの場合は既定値を使用）</param>
    public ScreenshotHistoryRepository(string? dataFolder = null)
    {
        string dbPath = Path.Combine(dataFolder ?? AppPaths.DataFolder, "ScreenshotHistory.db");
        // Shared接続にすることで、アプリ実行中でもバックアップ機能等が.dbファイルを
        // 外部から読み取れるようにする (既定のDirect接続は排他ロックのため読めない)。
        _db = new LiteDatabase(new ConnectionString(dbPath) { Connection = ConnectionType.Shared });
        _collection = _db.GetCollection<ScreenshotItem>(CollectionName);
        _collection.EnsureIndex(x => x.CreatedAt);
    }

    /// <summary>全スクリーンショット項目を作成日時の新しい順で取得する。</summary>
    /// <returns>スクリーンショット項目リスト</returns>
    public List<ScreenshotItem> GetAll()
        => _collection.Query().OrderByDescending(x => x.CreatedAt).ToList();

    /// <summary>新規スクリーンショット項目を挿入する。IDが未設定なら自動採番する。</summary>
    /// <param name="item">挿入するスクリーンショット項目</param>
    public void Insert(ScreenshotItem item)
    {
        if (string.IsNullOrEmpty(item.Id))
        {
            item.Id = Guid.NewGuid().ToString();
        }

        _collection.Insert(item);
    }

    /// <summary>既存のスクリーンショット項目を更新する。</summary>
    /// <param name="item">更新するスクリーンショット項目</param>
    public void Update(ScreenshotItem item)
        => _collection.Update(item);

    /// <summary>指定IDのスクリーンショット項目を削除する。</summary>
    /// <param name="id">削除対象の項目ID</param>
    public void Delete(string id)
        => _collection.Delete(id);

    /// <summary>保持数上限超過分の、トリミング対象となる非ピン留め項目を取得する。</summary>
    /// <param name="keepCount">保持する項目の件数</param>
    /// <returns>削除対象の非ピン留め項目リスト</returns>
    public List<ScreenshotItem> GetTrimCandidates(int keepCount)
        => _collection.Query()
            .Where(x => x.IsPinned == false)
            .OrderByDescending(x => x.CreatedAt)
            .Offset(keepCount)
            .ToList();

    /// <summary>データベース接続を破棄する。</summary>
    public void Dispose()
    {
        _db.Dispose();
    }
}
