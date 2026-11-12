# Mac 对齐 Windows / TAB5 320-R5

- 来源：Windows 分支 `origin/codex/tab5-status-reliability`，`cc7fe0d2426d3136281015c0182b291fd46f7de1`；接收：`codex/mac-tab5-status-reliability-20261111`，保留 Mac `af26b504` 的 Wi-Fi 回包修复。
- 范围：本地合并、实现、验证和交接；沿用桥接及固件版本，TAB5 共用 R5，不改变既有发布范围。

## 界面设计

天气设置沿用现有 640 × 680 pt 可调整窗口、原生系统字号和系统颜色。保存和取消仍在底部，回车保存、Esc 取消；定位和测试保留为表单内的辅助操作，沿用现有 8–12 pt 间距和原生按钮点击区域。首次授权从「获取当前位置」触发；若直接保存但定位未授权，沿用底部状态区提示操作，保留草稿。后台不弹授权框，定位暂不可用时在已有诊断行说明使用上次天气。升级保留原状态区布局和按钮；写入后等待设备重启及健康自检，自动核验期间禁用固件操作按钮，结束后恢复可用；只有精确核验通过才显示成功。Wi-Fi 触控沿用 CLI 和设备报告的逻辑宽高、方向及触控区域。

## 对齐与证据

| 项目 | Windows / TAB5 交接 | Mac 本轮结果 | 剩余验收 |
| --- | --- | --- | --- |
| 天气定位、刷新与权限 | `MigratedWeather/WeatherMonitor.cs` 和 `QWeatherCoordinates.cs` | **已完成实现和自动验证。** `MacWeatherLocation` 在主线程请求定位；后台只使用既有授权。和风自动定位先识别地区，再对中国大陆做 GCJ-02 修正，预报用区县 ID；手填位置和 Open-Meteo 不转换。自动定位约 100 米网格仅用于内存查询，编辑器沿用更粗的显示和保存精度。位置检查每分钟一次，静止天气每 15 分钟刷新，移动、设置变化、同坐标恢复按最近一次成功查询决定。定位或请求失败保留最近天气并标记旧数据；未授权保存保留原设置。 | 真机 Core Location 拒绝／恢复，实际和风区县及 TAB5 天气展示待验证。 |
| Wi-Fi 触控与发现 | Windows `Tab5UIAutomation`、`Tab5AutomationRunner` | **已完成实现和回环验证。** Mac 复用已配对状态 GET，单独校验报告证明、配对、请求 ID 与通道；不会把 USB 或上一个手势的回执当作当前无线手势成功。显式 `wifi-test` 从 USB 发起，使用 Wi-Fi 点击、滑动与截图；`restore`、`stop` 和输入结束走恢复路径并以 USB 探测确认。多网卡发现优先使用设备所在子网，沿用旧 USB 诊断和 Wi-Fi 状态缓存。 | Mac 配对的 R5 上确认截图、点击、滑动、主动恢复、失联恢复和设置读回；双网卡回环夹具之外尚无实机证据。 |
| OTA 自动启动核验 | Windows `Tab5UpgradeVerification` 与 320-R5 记录 | **已完成实现和自动验证。** Mac 按最后一段传输建立本次重启基准；重复最后一段不重启计时。匹配镜像和分区的 `PENDING_VERIFY` 有界等待，错误镜像或分区拒绝，只有 `VALID` 加三个实时样本才成功。无线优先在活跃通道核验，保留 USB 认证重启提前核验。 | Mac 配对设备的同版本 OTA、重启、自动核验与资料保留待验证；未重装桥接或刷机。 |
| R5 农历、总览天气、屏闪 | [R5 固件交接](MAC-TAB5-R5-WEATHER-HANDOFF-20261112.md) | **已有无需修改桥接。** 沿用 Mac 的 `calendar`、生日和 `weather.humidity` 投影，保留 Windows 已验收的 R1–R5 源码增量。 | Windows 的 R5 实屏和刷机证据仅属于 Windows 场景；Mac 总览与断联提示待真机验证。 |

和风接口依据：[坐标约定](https://dev.qweather.com/docs/resource/glossary/#坐标)、[城市搜索](https://dev.qweather.com/docs/api/geoapi/city-lookup/)、[空气质量接口](https://dev.qweather.com/docs/api/air-quality/air-current/)。AQI 按独立接口的纬度／经度顺序与两位小数格式发出。

Mac 真机接续：使用已配对并核过指纹的 R5；在隔离的 `--tab5-automation <新目录>` 会话内，从 USB 依次输入 `{"op":"wifi-test"}`、所需的 `probe` / `capture` / `tap` / `swipe`、`{"op":"restore"}` 和 `{"op":"stop"}`。确认截图与切页、恢复后的 USB 回执和 `settingsPreserved=true`。另做一次仅 Wi-Fi 回包中断后的固件自恢复；升级验证只观察自动核验，不点「核验启动」，保留设备当前展示设置。

本轮只交付本地源码和验证记录，无冻结安装包。Windows／TAB5 来源分支保持原远端身份，未变更版本、推送、打标签或发布。

## Mac 验证记录

- `swift test --package-path mac-app`：519 项，4 项需要额外环境而跳过，0 失败。覆盖天气请求调度、坐标校验、真实回环 HTTP 上的 Wi-Fi 报告认证，以及多次启动自检后进入 `VALID`、错误指纹和离线超时。
- `swift build -c release --package-path mac-app`：通过。
- `--self-test-tab5-ui` 通过。已查看真实 AppKit 离屏渲染：天气授权提示未截断，保存和取消位置清晰；等待重启和等待自检时按钮禁用，超时后恢复操作，固件状态与更新说明分区明确。本机预览在 `artifacts/mac-alignment-20261112/final-preview/`。
- 本机未枚举到 TAB5 USB 串口；未替换常驻桥接、未触发实际定位授权、未调用个人和风账户、未刷机。Windows 的既有验收记录未重新归类为 Mac 结果。
