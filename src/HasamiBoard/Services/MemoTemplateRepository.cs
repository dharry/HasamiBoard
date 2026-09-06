using System.IO;
using HasamiBoard.Models;
using LiteDB;

namespace HasamiBoard.Services;

/// <summary>メモテンプレートをLiteDBに永続化するリポジトリ。</summary>
public class MemoTemplateRepository : IDisposable
{
    private const string CollectionName = "memo_templates";

    private readonly LiteDatabase _db;
    private readonly ILiteCollection<MemoTemplate> _collection;

    /// <summary>データベースファイルを開き、コレクションとインデックスを準備する。</summary>
    /// <param name="dataFolder">データフォルダパス（nullの場合は既定値を使用）</param>
    public MemoTemplateRepository(string? dataFolder = null)
    {
        string dbPath = Path.Combine(dataFolder ?? AppPaths.DataFolder, "MemoTemplates.db");
        // Shared接続にすることで、アプリ実行中でもバックアップ機能等が.dbファイルを
        // 外部から読み取れるようにする (既定のDirect接続は排他ロックのため読めない)。
        _db = new LiteDatabase(new ConnectionString(dbPath) { Connection = ConnectionType.Shared });
        _collection = _db.GetCollection<MemoTemplate>(CollectionName);
        _collection.EnsureIndex(x => x.Name);
    }

    /// <summary>全テンプレートを名前順で取得する。</summary>
    /// <returns>メモテンプレートリスト</returns>
    public List<MemoTemplate> GetAll()
        => _collection.Query().OrderBy(x => x.Name).ToList();

    /// <summary>新規テンプレートを挿入する。IDが未設定なら自動採番する。</summary>
    /// <param name="template">挿入するテンプレート</param>
    public void Insert(MemoTemplate template)
    {
        if (string.IsNullOrEmpty(template.Id))
        {
            template.Id = Guid.NewGuid().ToString();
        }

        _collection.Insert(template);
    }

    /// <summary>既存テンプレートを更新する。</summary>
    /// <param name="template">更新するテンプレート</param>
    public void Update(MemoTemplate template)
        => _collection.Update(template);

    /// <summary>指定IDのテンプレートを削除する。</summary>
    /// <param name="id">削除対象のテンプレートID</param>
    public void Delete(string id)
        => _collection.Delete(id);

    /// <summary>データベース接続を破棄する。</summary>
    public void Dispose()
    {
        _db.Dispose();
    }
}
