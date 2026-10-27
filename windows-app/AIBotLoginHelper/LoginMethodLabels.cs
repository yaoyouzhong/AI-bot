internal static class LoginMethodLabels
{
    internal static string Classify(string? value) => value?.Trim().ToLowerInvariant() switch {
        "登录选项" or "登陆选项" or "sign-in options" or "sign in options" => "options",
        "密码" or "password" or "使用密码登录" or "sign in with your password" => "password",
        "pin" or "pin (windows hello)" or "windows hello pin" or "pin 码" or "pin码" or "使用 pin 登录" => "pin",
        "人脸" or "人脸识别" or "人脸识别 (windows hello)" or "face" or "facial recognition" or "windows hello face" => "face",
        "指纹" or "指纹识别" or "指纹识别 (windows hello)" or "fingerprint" or "fingerprint recognition" or "windows hello fingerprint" => "fingerprint",
        _ => "other"
    };
}
