namespace HasamiBoard.ViewModels;

/// <summary>Basic認証作成ツールで選択可能な .htpasswd 用パスワードハッシュ方式。</summary>
public enum HtpasswdAlgorithm
{
    Bcrypt,
    Apr1Md5,
    Sha1,
}
