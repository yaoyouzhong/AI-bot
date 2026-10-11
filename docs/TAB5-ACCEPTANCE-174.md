# TAB5 0.2.174-ui 应用时钟与九款表盘

2026-10-11，用户确认当前预览效果并授权提供新固件，在设备上检查实际外观。

## 内容与本地验证

- 采用精修时钟布局和九宫格表盘选择，保留原有编号、保存记录及独立倒计时入口。
- 昼夜环采用普通 12 小时内表盘与当地 24 小时外环；金色为白昼、蓝色为夜间，白色光点对应当前时间。日出使用太阳、日落使用月亮，事件时间位于右侧，移除表盘下方的昼夜和城市说明行。
- 液晶时光采用经典橄榄绿底色、七段数字和独立秒数。
- 原生完整回归、保存失败回滚、城市时区／日期、缓存／默认、极昼极夜、像素探针及 ESP-IDF 构建通过。
- ELF 显示与 Flash 安全链路检查通过，生产镜像校验器通过；实际 sidecar 原生预览通过，已审阅首屏和滚动说明。

证据位于 `artifacts/development/clock-faces-20261011/`，包括 `solar-lcd-preview-final.log`、`solar-lcd-firmware-build.log`、`display-flash-safe.json` 和 `actual-sidecar-preview.log`。

## 镜像身份

| 项目 | 值 |
| --- | --- |
| 版本 | `0.2.174-ui` |
| 规范镜像 | `artifacts/firmware/tab5/latest/aibot_tab5.bin` |
| 字节数 | 6,895,040 |
| 镜像 SHA-256 | `d3aec7da5d15ed24e32308e164509da9c4fa1d55471fe03f2b2baba63489485e` |
| ELF SHA-256 | `d35d574ef8a0194c84a8ca2f09ba67ebe93f8090e108c5500b52f16c2e863cb0` |
| 归档 | `versions/0.2.174-ui/d3aec7da5d15-06208151` |

## 设备交付与验收边界

通过 `scripts/prepare_tab5_firmware.ps1` 生成规范镜像与配套说明，并保留旧包。准备完成后用户表示“已在升级中”；只读查看现有“ M5Stack TAB5 · 连接与固件升级”窗口，确认显示“0.2.174-ui · 设备已运行此版本”，说明与当前包一致，USB／Wi-Fi／蓝牙均显示已连接。

已在现有桥接点击只读“核验启动（USB）”，窗口显示“正在核验启动…”及禁用的核验按钮。下一次读回被用户通过物理 Esc 中止，未继续桌面自动操作。因此版本回报已确认，但完整 ELF、有效 OTA 分区及连续显示心跳尚不能标记为通过；用户实际目视和触控结果仍待反馈。

本轮未退出或替换桥接、切设备页面、修改轮播设置或导入图库。回环状态接口连接失败，LAN 状态接口未鉴权请求返回 401，未绕过保护；轮播配置未取得当前读回确认。先前记录不能代替此次升级后的配置验收。

新的 Windows 天气字段采集尚未部署验收；固件没有当天数据时使用 06:00／18:00 默认值。完整 Windows TAB5 回归仍有既有 USB 清理异常断言失败，天气专项验证不能替代完整回归。没有提交、推送、标签或公开发布。

## English

The authorized .174 image was prepared at the canonical local OTA path with matching structured notes and an archived copy. Native UI regressions, ESP-IDF build, display/flash safety, production image validation and actual sidecar previews passed.

The user started the device update. The existing desktop window subsequently reported .174 running on TAB5, matching notes and all three links connected. Read-only USB boot verification was started, but the user stopped Computer Use with Escape before its result could be read. Exact ELF/partition/heartbeat acceptance and physical appearance remain pending. No desktop binary, gallery or carousel settings were changed, and the current carousel state was not independently read back. New desktop weather collection is also unverified; the firmware retains its documented fallback times. No public release.
