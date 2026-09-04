# Data sources and privacy

## Weather

AI-bot uses the Open-Meteo forecast, air-quality, and optional geocoding APIs. The bridge sends the configured coordinate, or the configured city when coordinates are absent. Weather data are provided under CC BY 4.0 and must retain attribution to [Open-Meteo](https://open-meteo.com/).

The bridge requests only current temperature, humidity, WMO weather code, daily high/low, US AQI, and PM2.5. A successful response is cached under `%APPDATA%\AI-bot`; a failed refresh keeps the prior snapshot and marks it stale.

The macOS source uses the same Open-Meteo fields and stores only non-secret preferences and last-successful display data in the app's `UserDefaults` domain. This path remains platform-unverified until it is built and exercised on macOS 13 or later.

## Stocks

AI-bot currently reads quote responses from `qt.gtimg.cn` for configured `sh`, `sz`, `bj`, `hk`, and `us` symbols. This endpoint has no public stability or redistribution commitment documented by this project. Quote data are fetched at runtime and are not bundled in the repository or release archives.

The configured symbol list is sent to that quote endpoint. A successful response is cached under `%APPDATA%\AI-bot`; a failed refresh keeps the prior snapshot and marks it stale.

The macOS source uses the same quote endpoint and stores only the configured symbols and last-successful display snapshot in `UserDefaults`. Its GB18030 decoding and live refresh still require a real macOS build and network test.

## Claude and Codex account quotas

The bridge reads the existing local CLI sign-in files only when requesting account quota data:

- Claude: `%USERPROFILE%\.claude\.credentials.json`, sent only to `api.anthropic.com`.
- Codex: `%USERPROFILE%\.codex\auth.json`, sent only to `chatgpt.com`.

Access tokens are held in memory for the request. They are not copied to AI-bot settings, status JSON, serial frames, logs, or caches. `%APPDATA%\AI-bot\usage-cache.json` contains only display data such as plan, utilization percentages, reset times, reset-credit counts, update time, and stale state. A failed request preserves the last successful snapshot. The current implementation does not refresh expired CLI credentials; the corresponding CLI must refresh its own sign-in first.

## Domestic-provider quotas

AI-bot has normalized parsers and last-successful cache fields for Alibaba Bailian Token Plan, Kimi Coding Plan, MiniMax Token Plan, and DeepSeek balance/cost responses. The Windows tray's `国产额度授权…` command opens each provider in an isolated WebView2 profile at `%APPDATA%\AI-bot\quota-auth-profile`. The browser observes JSON responses only from an explicit exact-host allow-list (`bailian.console.aliyun.com`, `www.kimi.com`, `platform.minimaxi.com`, `www.minimaxi.com`, and `platform.deepseek.com`) and passes candidate bodies to the selected provider parser. Unrelated JSON and unrecognized schemas are ignored; response bodies, cookies, and tokens are never logged or written to the display cache.

MiniMax also has an automatic request path: it calls `https://www.minimaxi.com/v1/token_plan/remains` when the bridge process has one of `MINIMAX_SUBSCRIPTION_KEY`, `MINIMAX_TOKEN_PLAN_KEY`, or `MINIMAX_API_KEY`. The key is read from process environment, held in memory, and sent only to `www.minimaxi.com`. It is not copied to settings, status, serial, logs, or `%APPDATA%\AI-bot\domestic-quota-cache.json`.

`domestic-quota-cache.json` stores only normalized display fields: provider/plan names, percentages, reset times, balance/cost/currency, update time, and stale state. The isolated WebView2 profile necessarily persists the providers' own cookies and browser storage, so it must not be committed, copied into release archives, or treated as a shareable cache. Parser and build tests are not evidence that a real account login still matches a provider's current response schema.

## Legacy settings compatibility

When `%APPDATA%\AI-bot\settings.json` does not yet exist, the bridge may read a strict allow-list of non-secret values from `%APPDATA%\AIClockBridge\settings.json`. It does not modify the legacy file and does not migrate API keys, cookies, OAuth credentials, passwords, or Windows Credential Manager entries.
