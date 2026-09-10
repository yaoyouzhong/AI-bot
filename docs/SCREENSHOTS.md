# 文档图片来源与复现 / Documentation image provenance

## 图片是什么

- `assets/screens/*.png`：26 张应用离线捕获图，其中 17 张页面/状态帧和 9 张窗口/菜单。
  页面由当前 Windows `MirrorForm.RenderSnapshot` 生成，窗口由实际 WinForms 控件 `DrawToBitmap` 捕获。
  没有重画控件、覆盖数值、替换文本或美化截图；系统主题和字体可能影响外观。
- `assets/guides/*.svg`：原创安装/刷写流程图，明确标注为示意图，不是安装器或上传日志截图。
- 首页 `assets/hero.*.svg` 与 `assets/scenes.svg` 是概念图；真实程序图另列，不能相互当作验收证据。

图中日期、城市、歌曲、股票报价、额度和系统指标均为程序内固定的虚构样例。
配置/图库/趋势窗口使用独立空配置；授权窗口禁用浏览器初始化，右侧未加载任何厂商网页。
不包含厂商 logo、第三方桌宠、真实用户名、Cookie、API Key、对话或维护者运行目录内容。
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
- macOS 默认回退源码同步；当前环境无法运行 Swift/AppKit，Mac 测试及 Release 编译已通过，候选打包与真机截图仍待完成。

## English summary

The PNGs are unretouched captures from the actual Windows renderer/controls, using
synthetic data and a fresh isolated profile. The authorization browser is disabled;
the gallery and history are empty. They are not hardware photographs or evidence of
live-account/macOS acceptance. Workflow SVGs are original instructional diagrams.
The capture tool does not start the bridge, contact providers or access a device.
Public PNGs require an individually reviewed filename and matching SHA-256.
