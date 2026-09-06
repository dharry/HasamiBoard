namespace HasamiBoard.ViewModels;

/// <summary>JWTペイロードのexp/iat/nbf等、日時系クレームの表示用データ。</summary>
/// <param name="ExpiredLabel">期限切れの場合のみローカライズ済みラベルを保持 (それ以外は空文字列)。</param>
public record JwtClaimDateItem(string ClaimName, string LocalDateTime, string UtcIso8601, string ExpiredLabel);
