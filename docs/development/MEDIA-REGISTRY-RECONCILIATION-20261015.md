# 媒体登记核对 / Media registry reconciliation

2026-10-15。公开内容检查的 `unreviewed-binary-or-artwork` 表示文件路径或 SHA-256 未匹配已审核登记，不是许可证等级冲突，也不能仅据此判断侵权。

本次 11 项文件的实际 SHA-256 全部与提交 `27fc0041aaaf2f6e85927864a937ca6e2166beec` 中已审核的 `licenses/materials.json` 一致。问题来自本地登记表恢复了旧值或遗漏了新版条目：8 项哈希过期，3 项缺失。已从该提交恢复对应登记及现有 2026-10-14 视频来源记录，未修改媒体字节、作品来源、署名或许可。此前其他本地截图、图库和程序工作保持独立，不随本次标题与文字修正发布。

| 类别 | 已核对文件 |
| --- | --- |
| 视频与封面 | `AI-bot-product-intro.mp4`、`AI-bot-cover.png` |
| Windows 运行监控与设置 | `bridge-monitor.png`、`device-center-bridge.png` |
| TAB5 连接、传输与升级 | `tab5-connection.png`、`tab5-transfer-test.png`、`tab5-upgrade.png`、`tab5-usb-pairing.png` |
| TAB5 作品与监控 | `tab5-painting.png`、`tab5-painting-portrait.png`、`tab5-runtime-health.png` |

视频位于 `docs/assets/product-intro/`，截图位于 `docs/assets/screens/`。原来源与加工说明见[视频说明](../PRODUCT_VIDEO.md)和[截图来源](../SCREENSHOTS.md)。本机逐项旧值、已核对值及修改前登记表保留在忽略的 `artifacts/development/release-title-text-sync-20261015/`，不进入发布包。

处理同类告警时，先确认实际字节与对应来源、许可和审核记录：匹配已审核版本时恢复记录；确为新素材时先审核来源、署名与加工，再登记新哈希；来源不能确认时保留告警并停止该素材的公开交付。不得仅计算新哈希就当作审核通过，也不得放宽检查器绕过告警。

## English

The guard checks reviewed paths and SHA-256 identities, not license grades. All eleven reported files exactly match the reviewed media registry in commit `27fc0041aaaf2f6e85927864a937ca6e2166beec`. The local registry had eight outdated hashes and three missing entries. Restoring those existing records and the established 2026-10-14 video provenance resolves the mismatch without changing media, attribution or license terms. Other local artwork, screenshot and application work remains separate from this title/documentation delivery. Future new media still requires source and attribution review before registration; changing a hash alone is insufficient.
