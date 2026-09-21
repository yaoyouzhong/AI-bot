# 文档图片来源与复现 / Documentation image provenance

## 图片是什么

- `assets/screens/*.png`：28 张应用离线捕获图，其中 17 张页面/状态帧和 11 张窗口/菜单。
  页面由当前 Windows `MirrorForm.RenderSnapshot` 生成，窗口由实际 WinForms 控件 `DrawToBitmap` 捕获。
  没有重画控件、覆盖数值、替换文本或美化截图；系统主题和字体可能影响外观。
- `assets/guides/*.svg`：原创安装/刷写流程图，明确标注为示意图，不是安装器或上传日志截图。
- 首页功能区使用 `assets/screens/` 中的真实程序捕获图，以中文标题说明 AI 额度、天气、股票、系统监控、音乐和农历屏保；不改写截图中的文字或数值。
- `assets/hero.*.svg` 与 `assets/scenes.svg` 是保留的历史概念素材；首页已不再引用 `scenes.svg`，不能把这些概念素材作为实际界面或验收证据。

图中日期、城市、歌曲、股票报价、额度和系统指标均为程序内固定的虚构样例。
配置/图库/趋势窗口使用独立空配置；授权窗口禁用浏览器初始化，右侧未加载任何厂商网页。
默认捕获不包含厂商 logo、第三方桌宠、真实用户名、Cookie、API Key、对话或维护者运行目录内容。2026-09-16 按维护者明确要求替换的 Codex/Claude 两张图例外：仅在隔离配置中读取其当前选用的萌宠，数值仍为虚构样例；原始动画缓存不进入仓库或发布包。
界面中出现的字体由 Windows 绘制，仓库不分发字体文件。

## 如何复现

在 Windows、.NET 8 SDK 环境下，从仓库根目录运行：

```powershell
dotnet run --project tools/doc-capture/DocCapture.csproj -c Release -- artifacts/docs-capture
python tools/build_guide_art.py
```

`DocCapture` 把当前应用源码编译到独立的工具输出目录，以自己的入口运行；
先调用 `AppPaths.BeginPublicSelfTest()` 再访问任何配置/缓存，所有数据落在忽略的 `artifacts/`。
不启动 `BridgeRuntime`、串口发送、HTTP 监听、厂商刷新或联网图库；构建恢复 NuGet 包仍需联网。
真实账号、正在运行的桥接和设备不参与捕获。

工具的 PackageReference 与应用项目保持一致。输出会受 Windows 字体、系统主题和绘图版本影响，
复现不承诺跨机器字节一致。若要替换公开图片，应逐张复核，再更新 `licenses/materials.json` 的 SHA-256。
内容检查只允许明确列名且哈希一致的 PNG，不放开整个图片目录。

## 这次实际验证

- Windows 独立 Release 构建通过，0 警告/0 错误；`--status-once` 返回协议版本 1。
- `--self-test-public` 通过，新增全新配置默认桌宠测试：Claude/Codex/桌宠页均有图形、工作时帧变化、无需生成导入资源；已有选择持久化测试也通过。
- PlatformIO `nodemcuv2` 固件构建通过；既有触摸引脚、缩进、未用变量及框架工具警告仍存在。
- 本轮**未刷写设备、未替换正在运行的程序**。因此不能把这里的新默认图认定为当前实机已经更新。
- macOS 默认回退源码同步；当前环境无法运行 Swift/AppKit，Mac 测试、Release 编译及 Apple Silicon 候选打包已通过；真机截图和实机验收仍待完成。

## English summary

The PNGs are unretouched captures from the actual Windows renderer/controls, using
synthetic data and a fresh isolated profile. The authorization browser is disabled;
the gallery and history are empty. They are not hardware photographs or evidence of
live-account/macOS acceptance. Workflow SVGs are original instructional diagrams.
The capture tool does not start the bridge, contact providers or access a device.
Public PNGs require an individually reviewed filename and matching SHA-256.

## 2026-09-16 教程图示

安装、刷写、下载选择、串口识别和成功提示五张 SVG 为本项目独立绘制的操作示意，已检查文字排版；不是安装器截图或刷机实测记录。相应 SHA-256 已更新到 `licenses/materials.json`。既有真实离线 PNG 未改动。

v0.1.3 新增 `setup.svg` 联网安装步骤示意，并明确 `install.svg` 仅用于 ZIP 手动安装。示意图不代表安装器实测截图。

## 当前萌宠额度图 / Selected-pet quota captures

2026-09-16，`codex.png` 与 `claude.png` 按维护者要求使用其当前选择的萌宠重新渲染。驻留程序只读诊断确认两个角色均已加载；捕获工具读取 Codex 的角色选择及 Claude 的本地默认动画，不修改驻留程序配置。两张图只用于展示应用界面，不将角色图案声明为本项目原创或 MIT 授权，不分发 APET 或源素材。

可选捕获入口：`dotnet run --project tools/doc-capture/DocCapture.csproj -c Release -- <output> <claude.apet> <codex.apet>`。此模式只生成两张额度图；调用者须明确选择获准展示的素材路径。默认无素材参数的捕获仍使用内置桌宠。

On 2026-09-16, the maintainer requested Codex/Claude quota screenshots with their currently selected pets. This explicit opt-in mode reads only the two supplied APET files into an isolated profile and renders synthetic quota values. The original animations are not distributed; depicted characters are not claimed as original AI-bot artwork or MIT-licensed assets. Default capture still uses built-in pets.

## 农历屏保预览 / Lunar screensaver preview

`screensaver.png` 按 v0.2.0 所含的农历实现渲染，显示示例日期 2026-09-10 对应的农历七月廿九；是源码离线预览，不是发布二进制或实体屏幕的验收照片。只捕获屏保可使用 `dotnet run --project tools/doc-capture/DocCapture.csproj -c Release -- <output> --screensaver`。

The screensaver image reflects the lunar implementation included in v0.2.0 with a synthetic date, not release-binary or hardware acceptance.

## 国产额度接口设置预览 / Domestic quota API settings

`api-settings.png` 已更新为 v0.2.0 设置布局的真实 WinForms 离线捕获，使用空凭据输入框，不初始化 WebView2、不访问真实凭据。复现：`dotnet run --project tools/doc-capture/DocCapture.csproj -c Release -- <output> --quota-api`，人工检查后取 `api-settings-deepseek.png`。

The API settings screenshot uses an isolated empty profile and does not access real credentials or initialize the browser. It is not proof of live-account API acceptance.

2026-09-16：主页保留九张功能图，国产模型示例改为新版 DeepSeek 官方 API 余额渲染（28.50 CNY 为虚构数据）；Codex/Claude 萌宠图不变。接口设置图更新为完整厂商名称和换行布局；截图来自隔离预览，不含真实账号或密钥。

Homepage refresh: nine feature images, including the new DeepSeek API balance render with fictional data and the lunar screensaver. Codex/Claude pets are preserved. The settings capture shows the current wrapping layout and contains no live credentials.

## 小屏刷机窗口 / Firmware flasher

`assets/screens/firmware-flasher.png` 是 Windows 原生刷机窗口的离线控件捕获。“USB 设备已连接”与固件包名是固定样例；捕获模式禁止执行刷机，不枚举真实串口、不下载工具，也不读取用户配置。截图展示精简主界面，“更多选项”默认折叠。它不代表 Mac 截图或硬件刷写成功记录。

复现：`dotnet <独立构建目录>/AIBotBridge.dll --capture-flasher <输出.png>`。图片逐张检查后加入固定文件名与 SHA-256 清单。

The flasher PNG captures actual Windows controls with synthetic input and a non-operational preview mode. It is not a Mac screenshot or hardware acceptance record.
