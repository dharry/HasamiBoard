namespace HasamiBoard.ViewModels;

/// <summary>エラーメッセージ一覧で選択可能なエラーコード体系の種類。</summary>
public enum ErrorListMode
{
    Http,
    Errno,
    WindowsError,
    Smtp,
}
