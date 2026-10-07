# Data sources and privacy

Applies to the current Windows 0.6.1 implementation unless a section names macOS. Configure shared sources under **Device Center → Account Data** and select each device's requirements under **My Devices → Data Settings**. Feature-specific flows such as [Codex Direct](TAB5-QUICK-CONSOLE.md) and [artwork imports](GALLERY-PACKS.md) have separate guides.

## Weather

Windows uses QWeather when its assigned API host and key are configured, with Open-Meteo fallback. Otherwise it uses Open-Meteo forecast, air-quality and geocoding services. The bridge sends the configured city or coordinate; optional automatic location uses Windows location services. QWeather keys stay in Windows Credential Manager and are sent to the configured `qweatherapi.com` host. Open-Meteo data retain attribution to [Open-Meteo](https://open-meteo.com/).

Current Windows weather includes conditions, temperature/range, humidity, pressure, available air-quality fields and hourly/daily forecasts. Fields and AQI standards depend on the provider; absent values remain unavailable. A successful response is cached in `%APPDATA%\AI-bot\weather-provider-cache.json`; failed refreshes preserve the last successful snapshot and mark it stale.

macOS has its own Open-Meteo implementation and stores non-secret preferences and last-successful display data in `UserDefaults`; do not assume it implements every Windows weather field or setting. CI tests/builds do not establish live-location or provider acceptance on a real Mac.

## Stocks

AI-bot currently reads quote responses from `qt.gtimg.cn` for configured `sh`, `sz`, `bj`, `hk`, and `us` symbols. This endpoint has no public stability or redistribution commitment documented by this project. Quote data are fetched at runtime and are not bundled in the repository or release archives.

The configured symbol list is sent to that quote endpoint. A successful response is cached under `%APPDATA%\AI-bot`; a failed refresh keeps the prior snapshot and marks it stale.

The macOS source uses the same quote endpoint and stores the configured symbols and last-successful display snapshot in `UserDefaults`. Current CI tests/builds do not establish live refresh or physical-display acceptance.

## Claude and Codex account quotas

The bridge reads the existing local CLI sign-in files only when requesting account quota data:

- Claude: `%USERPROFILE%\.claude\.credentials.json`, sent only to `api.anthropic.com`.
- Codex: `%USERPROFILE%\.codex\auth.json`, sent only to `chatgpt.com`.

Access tokens are held in memory for the request. They are not copied to AI-bot settings, status JSON, serial frames, logs, or caches. `%APPDATA%\AI-bot\usage-cache.json` contains only display data such as plan, utilization percentages, reset times, reset-credit counts, update time, and stale state. A failed request preserves the last successful snapshot. The current implementation does not refresh expired CLI credentials; the corresponding CLI must refresh its own sign-in first.

The macOS source follows the same credential-file, destination-host, no-log, and display-only-cache boundaries. It refreshes every two minutes while running and stores its display-only cache in `UserDefaults`. CI builds do not establish real-account acceptance.

## Music on macOS

Music access is disabled by default. After the user enables it, the macOS bridge checks whether Apple Music (`com.apple.Music`) or Spotify (`com.spotify.client`) is already running and then uses public Apple Events through `NSAppleScript` to request only title, artist, album, playback state, elapsed time, duration, and current artwork. It does not read playlists, libraries, account data, or audio, does not use the private MediaRemote framework, and does not persist music metadata or artwork.

Apple Music artwork is read from the Apple Event reply. Spotify exposes an HTTPS artwork URL, so the bridge accepts only `scdn.co` or `spotifycdn.com` hosts and downloads that exact URL with an ephemeral session, no cookies, and 5/8-second request/resource timeouts. Encoded artwork is limited to 10 MiB and 4096×4096, decoded through ImageIO, and converted to a 112×112 RGB565 resource. Decode or network failure sends a black replacement so the device does not retain a previous track's cover.

The generated app bundle contains an `NSAppleEventsUsageDescription` and the Automation Apple Events entitlement so macOS can ask for consent. Denial or an unavailable player produces no active music session. The source and permission behavior remain platform-unverified until tested on macOS 13 or later.

## Domestic-provider quotas

Windows **Device Center → Account Data → Model Accounts** opens the provider configuration. DeepSeek balance, MiniMax Token Plan and Kimi's local usage service have credential-backed API paths. Ali Token Plan, Zhipu balance and other supported browser adapters use an isolated WebView2 profile at `%APPDATA%\AI-bot\quota-auth-profile`. Browser capture filters responses according to the selected provider and its endpoint rules before parsing display fields; it is not a general network-history export. Provider-specific support and unverified adapters are listed in [the setup guide](DOMESTIC_QUOTA_SETUP.md).

Keys saved through the Windows form reside in Windows Credential Manager. MiniMax checks its saved credential first, then the compatibility environment variables `MINIMAX_SUBSCRIPTION_KEY`, `MINIMAX_TOKEN_PLAN_KEY`, and `MINIMAX_API_KEY`; its endpoint is `https://www.minimaxi.com/v1/token_plan/remains`. Kimi's local-service token goes to the configured loopback service. The configured credentials are not stored in ordinary settings, device status frames or display caches.

The current Windows display cache is `domestic-provider-cache.json`; older `domestic-quota-cache.json` data may be read for compatibility. These caches hold normalized provider/plan names, percentages, reset times, balance/cost/currency and timestamps. Failed requests retain prior successful values. The WebView2 profile persists provider cookies and browser storage, so it must not be committed, copied into releases or treated as a shareable cache. Parser/build tests are not evidence that a real account still matches a provider's current response schema.

## Legacy settings compatibility

When `%APPDATA%\AI-bot\settings.json` does not yet exist, the bridge may read a strict allow-list of non-secret values from `%APPDATA%\AIClockBridge\settings.json`. It does not modify the legacy file and does not migrate API keys, cookies, OAuth credentials, passwords, or Windows Credential Manager entries.
