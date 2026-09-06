namespace HasamiBoard.Services;

/// <summary>
/// OCRエンジンを作成できなかった場合にスローされる。主な原因は、OSに
/// 対応する言語のOCR言語パック (Windows の「時刻と言語」設定から追加可能) が
/// インストールされていないこと。
/// </summary>
public class OcrEngineUnavailableException : Exception
{
    /// <summary>既定のエラーメッセージで例外を初期化する。</summary>
    public OcrEngineUnavailableException()
        : base("OCR engine is unavailable. The OCR language pack may not be installed.")
    {
    }
}
