# 天气定位刷新修复交接

## 范围与状态

- 工作分支：`codex/tab5-status-reliability`；基线 `3f3dacf977f7cd2c8a1867eb1b909517beeb4895`。本文随修复提交；最终交接范围见 [R5 与天气交接](MAC-TAB5-R5-WEATHER-HANDOFF-20261112.md)。
- Windows 实现、自测、真实和风定位查询已完成；新候选尚未覆盖安装，TAB5 上的最终展示待验收。Mac 原生构建与对齐待完成。
- 天气修复没有新增协议字段，不要求升级固件。日历与显示修订属于独立固件变更，不能用其安装结果代替天气桥接的安装验收。
- 不修改系统定位权限、凭据或用户指定地区，不把真实坐标及密钥写入验收记录。

## 已确认的问题

1. 运行中的桥接曾报告定位权限不可用，只能沿用上次位置。权限恢复后，它确实取得了 Windows 新位置，但和风仍显示「南京 秦淮」。用户确认实际所在区县为雨花台区。
2. Windows 定位质量检查返回 Wi-Fi 来源、86 米精度、15 秒缓存年龄。此检查未访问天气服务，未保存坐标。
3. 和风在中国大陆要求 GCJ-02；原实现将 Windows WGS-84 直接用于地区查询。用户明确授权一次当前坐标向已配置和风服务的修复验收后，候选实际返回 `南京 雨花台`。
4. 原刷新逻辑只比较本轮定位前后的已保存坐标；另一个设置窗口先保存坐标时，采集器可能错过位置变化并等到 15 分钟定时刷新。

服务契约依据：[和风坐标说明](https://dev.qweather.com/docs/resource/glossary/#坐标)。权限异常的具体 Windows 内部来源未证实，不将“程序路径变化”当作已确认根因。

## 实现与 Mac 对齐

| 项目 | Windows 实现 | Mac 要求与状态 |
| --- | --- | --- |
| 坐标系边界 | `MigratedWeather/QWeatherCoordinates.cs`；由 Geo 元数据确认中国大陆，转换后重新查询地区；港澳台与海外不转换，手动城市查询不转换 | 核对 `MacQWeatherService.swift` 的 CoreLocation 坐标输入，在服务边界处理；待对齐及原生测试 |
| 刷新失效 | `WeatherRefreshSchedule.cs` 独立记录上次成功查询的位置、来源和主机；权限恢复、设置变化立即刷新 | 对照 `MacWeatherLocation.swift` 与天气采集调度，保留 OS 原始坐标；待验证 |
| 位置采样 | Windows 请求 100 米精度，缓存最长 30 秒；移动超过 100 米更新；低精度标明区县可能偏差 | 使用原生精度信息，不机械移植 Windows 权限调用；待验证 |
| 权限与保存 | UI 线程请求位置；保存自动定位设置前核验；拒绝授权时保持设置；托盘在权限不可用时提示一次 | 按 macOS 原生授权生命周期处理；待验证 |
| 数据保留 | 经纬度成对保存；失败保留最近成功天气；Open-Meteo 自动定位继续使用 WGS-84 | 不清空已有地区、密钥或缓存；待验证 |

## 本轮验证

- `dotnet build windows-app/AIBotBridge/AIBotBridge.csproj -c Release --no-restore`：零错误、零警告。
- `--self-test-weather-location`：坐标转换公开参考点、境外不偏移、港澳台边界、权限恢复、同坐标恢复、设置变化、定时刷新、失败重试和拒绝授权保留设置通过。
- `--self-test-weather-forecast`：天气预报、日出日落及旧协议兼容回归通过。
- `--self-test-settings-layout`：实际离屏渲染与关闭/取消回归通过，已查看天气设置图。
- `dotnet run --project windows-app/AIBotBridge/AIBotBridge.csproj -c Release --no-build -- --status-once`：退出码 0。
- 实际和风修复查询：`{"status":"ok","city":"南京 雨花台"}`。不代表已安装候选或设备端完整验收。
- 本机证据目录：`artifacts/t5-320-r4-split/`，包括 `corrected-qweather.json`、`location-quality.json`、`settings-test.log` 与 `weather-settings-render/`。运行状态原文仅留本机，不入 Git。

## 独立验收边界

- 320-R4 已安装并自动核验，通过约 10 分钟总览采样，欠载与显示错误均为零；用户报告暂未看到闪屏。R5 接续该显示实现并调整农历布局，独立记录镜像与设备证据。
- R3 观测到的 LCD 欠载从 3 增至 4 是历史问题证据。R4 的短时良好结果不证明永久消除屏闪；天气定位修复也不能作为显示修复证据。
- Windows 天气代码的构建、自测与一次授权的真实服务查询已通过，但该天气桥接尚未覆盖安装。Mac 必须自行完成原生构建和受影响路径验收，不能复用 Windows 结果冒充 Mac 通过。
- 本记录不授予公开发布或修改用户定位权限的权限。
