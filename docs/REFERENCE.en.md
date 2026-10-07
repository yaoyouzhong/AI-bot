# Feature and development reference

Current releases: computer apps **0.6.0**, ESP8266 **0.5.0**, TAB5 **0.2.145-ui**. Components have independent versions. [Home](../README.en.md) · [简体中文](REFERENCE.zh.md)

## Using AI-bot

| Purpose | Current guide |
| --- | --- |
| Downloads, first installation, pairing and upgrades | [Installation](INSTALL.zh.md) |
| Current menus, pages and features | [Feature gallery](FEATURES.zh.md) |
| Update notices and matching packages | [Software and firmware updates](UPDATES.md) |
| Painting and calligraphy installation | [Optional collections](GALLERY-PACKS.md) |
| Changes and acceptance boundaries | [Component release notes](RELEASE-0.6.0.md) · [TAB5 .145 acceptance](TAB5-ACCEPTANCE-145.md) |

Windows supports ESP8266 and TAB5. macOS 13+ on Apple Silicon provides its own menu-bar, mirror and ESP8266 implementation; it does not include the Windows TAB5 services, unified update UI or artwork imports. Intel Macs are unverified. Mac test/build results are separate from GUI and hardware acceptance.

## Source and builds

| Component | Reference |
| --- | --- |
| `windows-app/AIBotBridge/` | .NET 8 Windows tray application; [build and packaging](BUILD_WINDOWS.zh.md) |
| `mac-app/` | Independent Swift app; run `swift test --package-path mac-app` and `swift build -c release --package-path mac-app` on macOS |
| `firmware/` | ESP8266 PlatformIO project; [build and flash](FLASH_BUILD.zh.md) |
| TAB5 | Independent ESP-IDF [public source snapshot](development/TAB5-SOURCE.md) and [firmware workflow](TAB5-FIRMWARE-WORKFLOW.md) |

See [architecture](DEVELOPMENT.md), [data sources](DATA_SOURCES.md) and [protocol](PROTOCOL.md). USB uses 460800 baud; ESP8266 small messages use version-1 JSON prefixed with `@AIBOT `. TAB5 has a separate protocol service. Firmware and flashing routes are not interchangeable.

The computer app's local read-only status endpoint defaults to `127.0.0.1:8765/status`; the TAB5 service defaults to port `18765`. LAN management requires pairing authentication. Local token metadata and provider account quotas are counted separately. See [quota-history semantics](QUOTA_TRENDS.md).

## Releases and historical records

[Component versioning](COMPONENT-VERSIONS.md) defines independent version sources, tags and changelogs. Main-branch pushes run CI; component tags enter candidate workflows, with verification and publication handled separately. An app update does not require both devices to update.

[Release-readiness records](RELEASE_READINESS.md) and the [reimplementation parity contract](FUNCTIONAL_PARITY.md) retain historical evidence. They are not current installation instructions or release status. Use the releases and acceptance records linked above.

Own source is MIT licensed; third-party fonts, components and artworks retain their terms. [Provenance](../PROVENANCE.md) · [Distribution terms](DISTRIBUTION_TERMS.md) · [Third-party notices](../THIRD_PARTY_NOTICES.md)
