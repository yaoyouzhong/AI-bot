# Data sources and privacy

## Weather

AI-bot uses the Open-Meteo forecast, air-quality, and optional geocoding APIs. The bridge sends the configured coordinate, or the configured city when coordinates are absent. Weather data are provided under CC BY 4.0 and must retain attribution to [Open-Meteo](https://open-meteo.com/).

The bridge requests only current temperature, humidity, WMO weather code, daily high/low, US AQI, and PM2.5. A successful response is cached under `%APPDATA%\AI-bot`; a failed refresh keeps the prior snapshot and marks it stale.

## Stocks

AI-bot currently reads quote responses from `qt.gtimg.cn` for configured `sh`, `sz`, `bj`, `hk`, and `us` symbols. This endpoint has no public stability or redistribution commitment documented by this project. Quote data are fetched at runtime and are not bundled in the repository or release archives.

The configured symbol list is sent to that quote endpoint. A successful response is cached under `%APPDATA%\AI-bot`; a failed refresh keeps the prior snapshot and marks it stale.

## Claude and Codex account quotas

The bridge reads the existing local CLI sign-in files only when requesting account quota data:

- Claude: `%USERPROFILE%\.claude\.credentials.json`, sent only to `api.anthropic.com`.
- Codex: `%USERPROFILE%\.codex\auth.json`, sent only to `chatgpt.com`.

Access tokens are held in memory for the request. They are not copied to AI-bot settings, status JSON, serial frames, logs, or caches. `%APPDATA%\AI-bot\usage-cache.json` contains only display data such as plan, utilization percentages, reset times, reset-credit counts, update time, and stale state. A failed request preserves the last successful snapshot. The current implementation does not refresh expired CLI credentials; the corresponding CLI must refresh its own sign-in first.

## Legacy settings compatibility

When `%APPDATA%\AI-bot\settings.json` does not yet exist, the bridge may read a strict allow-list of non-secret values from `%APPDATA%\AIClockBridge\settings.json`. It does not modify the legacy file and does not migrate API keys, cookies, OAuth credentials, passwords, or Windows Credential Manager entries.
