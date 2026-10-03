# 当前截图来源 / Current screenshot provenance

2026-10-03，v0.5.0 本地候选。设备中心、添加设备、ESP8266 刷机、TAB5 首次安装、连接/USB 配对/升级、显示设置及镜像页面均使用当前原生 WinForms 代码生成。`tools/doc-capture/DocCapture.cs` 在访问设置之前启用公共测试隔离目录；不启动 BridgeRuntime、串口服务、HTTP 监听或账户刷新。所有显示数据和设备身份为固定虚构样例，授权页面禁用浏览器初始化；没有导入私人桌宠。

```powershell
dotnet build tools/doc-capture/DocCapture.csproj -c Release
dotnet tools/doc-capture/bin/Release/net8.0-windows10.0.19041.0/DocCapture.dll <output-directory> --release-ui
dotnet tools/doc-capture/bin/Release/net8.0-windows10.0.19041.0/DocCapture.dll <output-directory>
dotnet tools/doc-capture/bin/Release/net8.0-windows10.0.19041.0/DocCapture.dll <output-directory> --quota-api
```

`tab5-quota.png`、`tab5-activity.png`、`tab5-voice.png` 和 `tab5-calendar.png` 来自 TAB5 相邻工程同日生成的 LVGL 原生预览 PPM，转成 PNG 时保持原像素，没有重绘控件或添加虚构硬件外观。它们分别对应额度四卡片、活动、语音结果恢复和十月日历场景。

画面用于说明当前布局，不是实体设备照片，也不是新的真机功能验收。WinForms 和 LVGL 无法直接导入浏览器视频工程，视频使用这些原生像素图与局部裁切放大，并标注“演示数据 / 固件预览”。桌宠统一使用项目原创 BYTE SPROUT。

截图精确路径和 SHA-256 登记于 `licenses/materials.json`，公开内容检查仍拒绝未登记图片或已登记路径的不同字节。未发生变化的原生镜像可保持相同哈希，不表示沿用旧源代码。刷机教程中使用当前窗口截图；ESP8266 引脚示意只适用于标明的硬件，不用于 TAB5。

Windows 字体、系统主题和绘图版本会影响捕获外观，复现不承诺跨机器字节一致。每次替换公开图片仍须逐张复核，再登记精确哈希。工具编译需要 NuGet 依赖，但界面捕获不使用真实账号、桥接或设备。API 设置图取 `--quota-api` 输出的 `api-settings-deepseek.png`，不加载厂商网页。

## 历史素材与捕获入口

`assets/guides/*.svg` 是本项目独立绘制的流程示意，不是安装器或刷机结果截图；`hero.*.svg`、`scenes.svg` 和未在新版指南中使用的 `tray-menu.png` 保留为历史素材，不能证明当前布局或验收。示意生成入口为 `python tools/build_guide_art.py`，仅在需要更新这些图时运行。

2026-09-16 曾按维护者明确要求在隔离环境中生成带当时所选萌宠的 Codex/Claude 图，没有分发原始 APET，也未将角色声明为项目原创或 MIT 作品；本轮公开图已替换为原创 BYTE SPROUT。工具的显式素材入口 `<output> <claude.apet> <codex.apet>` 仍保留，须由调用者选择获准展示的素材，本轮未使用。旧农历日期、厂商设置布局和早期 Mac/Windows 构建记录是当时证据，不能作为本轮新版验收结果。

只生成屏保可使用捕获 DLL 的 `<output> --screensaver`。本轮日期样例已更新，不再展示旧版的 2026-09-10。

## English

Current native Windows Forms and mirror views are rendered from the v0.5.0 candidate with isolated synthetic data. No bridge services, serial workers, provider refresh or authentication browser are started. TAB5 images are pixel-preserving conversions of same-day LVGL firmware previews. They are not hardware photos or new device acceptance. The video embeds original pixels and selected crops because WinForms/LVGL components cannot run inside its browser renderer. Only the original BYTE SPROUT pet is shown. Exact image paths and hashes remain enforced by the public-content guard.
