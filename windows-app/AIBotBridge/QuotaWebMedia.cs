using Microsoft.Web.WebView2.Core;

namespace AIBotBridge;

internal static class QuotaWebMedia
{
    // Some login pages enumerate capture devices before WebView2 raises a
    // permission event. Stop that call before it can reach Windows.
    private const string BlockCapture = """
        (() => {
            const denied = () => new DOMException("Media capture is unavailable in quota pages.", "NotAllowedError");
            if (typeof MediaDevices !== "undefined") {
                MediaDevices.prototype.enumerateDevices = () => Promise.resolve([]);
                MediaDevices.prototype.getUserMedia = () => Promise.reject(denied());
            }
            for (const name of ["getUserMedia", "webkitGetUserMedia"]) {
                if (typeof Navigator.prototype[name] === "function") {
                    Navigator.prototype[name] = (_, success, failure) => {
                        if (typeof failure === "function") queueMicrotask(() => failure(denied()));
                    };
                }
            }
        })();
        """;

    internal static async Task ConfigureAsync(CoreWebView2 web)
    {
        web.PermissionRequested += (_, request) =>
        {
            if (request.PermissionKind is CoreWebView2PermissionKind.Camera or CoreWebView2PermissionKind.Microphone)
            {
                request.State = CoreWebView2PermissionState.Deny;
                request.SavesInProfile = false;
                request.Handled = true;
            }
        };
        await web.AddScriptToExecuteOnDocumentCreatedAsync(BlockCapture);
    }
}
