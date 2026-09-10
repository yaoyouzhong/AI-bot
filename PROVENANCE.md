# Provenance

AI-bot is a new implementation maintained by 姚有忠.

The product direction and compatibility contract were informed by the observable behavior and documented requirements of an earlier personal ESP8266 status-clock project. AI-bot does not import that project's Git history or any third-party-authored source, documentation, screenshot, logo, sprite, or binary asset. Protocol names, source layout, user-interface text, and the clean-room implementation are written for AI-bot.

Some later functionality in the earlier fork has commits authored by the AI-bot maintainer. Such code may only be reused after file- and dependency-level provenance review confirms that the reused expression is owned by that maintainer; otherwise only behavior is treated as a specification and the implementation is rewritten.

## Maintainer-authored migration, 2026-09-08

`windows-app/AIBotBridge/CompletionChime.cs` is the maintainer-authored procedural
WAV synthesizer introduced at `c8b32d86b1da30c058dbd2e3590e479660d9d1b5`.
Full-file blame attributes it to 姚有忠; no recorded audio was imported.
The surrounding legacy `StatusService.cs` has mixed upstream authorship and was
not copied. AI-bot's bounded incremental lifecycle reader is independently written.

`windows-app/AIBotBridge/Assets/app-icon.ico` is the maintainer's robot icon,
generated on 2026-08-24 for the explicit request “小机器人样式”. The original
generation and ICO conversion record was reviewed in local task
`01a03295-faf1-7f02-87aa-c3942ca274c7`; the imported ICO hash matches the old
application's asset. This is not the upstream `happy-mac.png` or any vendor logo.

The following narrowly scoped source was reviewed against the local legacy checkout at
`2efab1d0661e24b926c0fef808522d427a0cd5d0`. File/line blame attributes the migrated
code to 姚有忠; this does not authorize copying the surrounding third-party framework.

- `windows-app/AIBotBridge/MigratedWeather/WeatherMonitor.cs` and `CredentialStore.cs`:
  legacy files under `windows-app/AIClockBridge/`, introduced in
  `2dafc6b64d8c230be0320093d331c912f59b989a`.
- `MigratedWeather/WindowsLocation.cs` and `WeatherSettingsForm.cs`: same legacy directory,
  introduced in `512707360a40e5029f5ec7f6d5d408b1f95f7e14`.
- `firmware/src/WeatherAnimations.h`: maintainer-authored geometric weather animations
  from legacy `firmware/src/main.cpp`, lines 2274–2455 at the reviewed checkout.
  `WeatherAnimations.cs` ports that geometry to the independent GDI drawing adapter.
  No bitmap, vendor logo, main pet sprite, or framework source was included.
- `windows-app/AIBotBridge/MigratedDomestic/DomesticQuotaService.cs`: reviewed in full
  (1866 legacy lines), introduced at `2dafc6b64d8c230be0320093d331c912f59b989a`.
  Git blame contains only the maintainer's 姚有忠 / yaoyouzhong aliases with the
  same maintainer email. Reuses the provider authorization UI, response and DOM
  parsing, rate-limit backoff and browser recovery. Settings and CredentialStore
  resolve to the audited adapters, not the legacy application framework.
  The cache is `AI-bot/domestic-provider-cache.json`; WebView2 uses the independent
  `AI-bot/quota-auth-profile`. Existing legacy caches and named credentials are
  read-only fallbacks. Raw browser/exception diagnostic text is not logged.

Integration changes: independent settings adapter and lifecycle; AI-bot cache and
credential targets; read-only legacy display-cache/credential fallback; RGB565 encoding
through AI-bot's existing little-endian encoder; QWeather host validation with redirects
disabled. No legacy settings or credentials are written by migration tools. The legacy
settings framework, RGB565 implementation, and firmware globals were not imported.

Third-party libraries are consumed through their normal package managers and retain their own licenses. See `THIRD_PARTY_NOTICES.md`.

Claude, Codex, OpenAI, Anthropic, and other product names may be referenced only to describe interoperability. AI-bot is not affiliated with or endorsed by those vendors. No vendor logo is distributed by this repository.

The built-in `BYTE SPROUT` pixel pet is drawn from geometric primitives in AI-bot firmware. It does not contain or derive from an imported sprite, screenshot, character, or logo.

## Source closeout review, 2026-09-10

Rechecked full-file blame at the legacy commit above: CompletionChime 84 lines,
WeatherMonitor 705, CredentialStore 79, WindowsLocation 26, WeatherSettingsForm 207,
and the weather-animation range 2274-2455 (182 lines) identify 姚有忠. The domestic
service identifies 姚有忠 for 1689 lines and yaoyouzhong for 177 lines.

A mechanical comparison of 10 consecutive nonblank normalized source lines
(blocks longer than 220 characters) found shared blocks only in the listed
maintainer migrations and the C# weather-animation port. It did not identify an
additional copied upstream implementation. This is a bounded similarity check,
not proof of copyright ownership or a complete derivative-work analysis.

The sole bundled application image/icon is the reviewed robot ICO; its SHA-256 is pinned in
`licenses/materials.json`. The similarly named `TestFixtures/claude_sprite.h` is
three synthetic red/green/blue pixels, not a legacy sprite; its LF-normalized hash
is also checked. Runtime importers, backup extraction and comparison tools contain
code only; they do not authorize public redistribution of user-provided assets.

Third-party license documents under `licenses/` are deliberately retained from
their named upstream sources with exact hashes. Firmware source materials collect
package-manager dependencies separately from AI-bot's own source archive; they
retain their original licensing and are not imported legacy project history.

## Homepage illustrations, 2026-09-10

`docs/assets/hero.zh.svg`, `hero.en.svg` and `scenes.svg` are original vector
illustrations generated by `tools/build_readme_art.py` for this repository.
The device is a conceptual enclosure; the geometric BYTE SPROUT pet follows
AI-bot's original primitive-based design. All displayed figures are fictional
examples, not screenshots or measured results. No third-party logo, photograph,
sprite, external image or font file is embedded. These illustrations are
covered by the repository MIT license; their hashes are pinned in
`licenses/materials.json`.

## Documentation captures and built-in fallback, 2026-09-10

`docs/assets/screens/*.png` are reviewed offline renders of AI-bot's own Windows
pages and controls, generated by `tools/doc-capture` against the current source.
All displayed account/market/activity values are synthetic. An isolated profile
prevents private pets, page logos, credentials and user settings from loading;
the authorization browser and online gallery are disabled. No third-party art or
vendor webpage is embedded. Each approved filename and SHA-256 is checked by the
public-content guard and `licenses/materials.json`. See `docs/SCREENSHOTS.md`.

`docs/assets/guides/*.svg` are original procedural workflow diagrams generated by
`tools/build_guide_art.py`. They are labeled as illustrations, not screenshots.

The Windows and Mac BYTE SPROUT fallback uses the same original primitive-based
geometry already present in AI-bot firmware. No external sprite was imported.
These project-authored diagrams and captures are covered by the repository MIT
license; third-party components retain their own licenses.
