# 当前截图来源 / Screenshot provenance

2026-10-07 同步至 Windows AI-bot 0.6.1 与 TAB5 0.2.149-ui。仅刷新显示版本变化的更新中心、设备中心及 TAB5 固件设置页，其他截图和视频沿用已复核素材。Windows 窗口和 TAB5 页面来自原生 WinForms/LVGL 代码的隔离渲染，使用固定演示数据，**不是硬件实拍，也不替代真机验收**。基础功能见 [.145 验收记录](TAB5-ACCEPTANCE-145.md)，本次补丁另见 [.149 BLE 验收记录](TAB5-BLE-GALLERY-148.md)。

## Windows 捕获

`tools/doc-capture/DocCapture.cs` 在读取设置前启用公共测试隔离目录。不启动 BridgeRuntime、串口、HTTP 服务、账户刷新或授权浏览器，也不读取用户桌宠缓存。演示设备身份、账户额度、会话和音乐信息均为虚构样例。

在仓库根目录运行：

```powershell
dotnet build tools/doc-capture/DocCapture.csproj -c Release
dotnet tools/doc-capture/bin/Release/net8.0-windows10.0.19041.0/DocCapture.dll <output-directory> --release-current
dotnet tools/doc-capture/bin/Release/net8.0-windows10.0.19041.0/DocCapture.dll <output-directory> --release-ui
dotnet tools/doc-capture/bin/Release/net8.0-windows10.0.19041.0/DocCapture.dll <output-directory> --quota-api
dotnet tools/doc-capture/bin/Release/net8.0-windows10.0.19041.0/DocCapture.dll <output-directory> --design-pet docs/assets/pet/AI-bot-mascot.gif
dotnet tools/doc-capture/bin/Release/net8.0-windows10.0.19041.0/DocCapture.dll <output-directory> --codex-pro-cover --design-pet docs/assets/pet/AI-bot-mascot.gif
```

- `--release-current`：使用真实更新窗口与图库管理窗口，模拟发布查询、旧版本设备和已安装图库的元数据。`update-center.png` 是“发现可用更新”的示例；`gallery-packs.png` 的 402/411 件状态来自与发布包一致的固定数据，不是另一次导入验证。不下载、安装或刷机。
- `--release-ui`：当前托盘菜单、设备中心、添加设备、小屏刷机、TAB5 首刷、配对、连接与升级窗口；TAB5 固件演示版本从 `versions/TAB5` 读取，当前为 `.149`。
- `--quota-api`：固定的 DeepSeek 设置界面，输出 `api-settings-deepseek.png` 后用作公开 `api-settings.png`。
- `--design-pet`：原生镜像和设置窗口；使用批准的项目 GIF 作为桌宠设计预览和虚构曲目的封面。它不改变已安装程序或固件的默认形象。
- `--codex-pro-cover`：为视频生成与 TAB5 演示对应的 PRO、周用量 88%、四张重置卡及六帧桌宠画面。浏览器时间轴选择整页原生帧，不用图层覆盖业务控件。

## TAB5 捕获与艺术作品

此前 `.145` 工程的 `tests/ui-preview/preview.c` 生成 LVGL 原生 PPM；转为 PNG 时保留像素，竖屏图只作 90° 物理方向调整。新图包括 Codex 直达、全年点阵（默认/暗夜/暖灰）、十二月历、名画/书法的横竖排版。额度、任务、日历与语音页面也重新生成。

素材库另用隔离原生测试夹具生成余额、音乐及语音/相机流程；当前 108 秒影片选取其中部分画面，不逐项展示所有功能：实际调用原生按钮处理函数，模拟收音、识别、拍摄确认与附件返回；不调用真实语音服务、不发送消息。相机和专辑封面采用项目原创角色，曲目“桌面之光 · 示例曲目”为虚构数据。本地生成脚本在 `artifacts/product-intro-story-20261007/`，见[视频来源](PRODUCT_VIDEO.md)。

此前界面图鉴另补充 **26 张 TAB5 原生截图**：总览、应用、任务列表、四个设置分区、桌宠和经典圆盘 9 张在隔离夹具中重新生成，其余 17 张从上述当前影片的原生素材库选用并逐张复核。覆盖天气三种视图、行情、电脑状态、音乐、余额、回复、五种时钟及语音／相机草稿。重新生成批次的 LVGL 界面源码对应 TAB5 commit `02df4f2dbcb1c8535af7f5ffd518ae39f52844c1`；固件版本标签使用 `.145` 演示值，桌宠加载项目原创角色。脚本和捕获日志保存在本地 `artifacts/tab5-feature-guide-20261007/`，不操作设备或运行中的桥接。

小屏的 Claude / Codex、双额度、活动、其他模型、天气、股票、系统、音乐、桌宠和时钟屏保截图也已逐项复核，沿用当前 Windows 镜像渲染；它们不是 ESP8266 的新一轮实机截图。本次只完善公开图鉴，没有替换程序或固件。

| 画面 | 作者与作品 | 馆藏与许可 | 加工 |
| --- | --- | --- | --- |
| 每日名画横/竖屏 | 林良《孔雀竹石圖》（Peacocks and Bamboo） | [Cleveland Museum of Art，1964.242](https://www.clevelandart.org/art/1964.242)，CC0 | 图库排版、缩放；竖屏原生画面转正 |
| 每日书法横/竖屏 | 王嗣奭《行书七言律诗轴》 | [东京国立博物馆 / ColBase，TB-6](https://colbase.nich.go.jp/collection_items/tnm/TB-6?locale=zh)，[CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) | 图库排版、缩放；竖屏原生画面转正 |

作品图像保留原图条款，项目 MIT 许可不替代作品许可；署名及来源同时保留在原生画面和本页。博物馆未为本项目背书。图库逐件来源与加工记录随独立 ZIP 分发。

十二月历花卉沿用项目已登记素材；新版尖顶蓝紫角色及六帧动画见[桌宠素材说明](assets/pet/README.md)。Codex 等标识仅用于识别对应产品。

## 本次选择性同步

发布前核对当前界面、版本及下载入口；只有画面内容变化时才替换截图。保留未变化的截图与视频，注明原捕获版本；历史验收结果不改写为新版本的验收。新图逐张复核，并同步 `licenses/materials.json` 的精确路径与 SHA-256。

本次三张图的来源：

- `update-center.png`、`device-center.png`：当前 Windows 源码及隔离捕获工具；更新示例显示桥接 0.6.0 → 0.6.1、TAB5 .147 → .149，设备示例显示 .149。捕获工具从根目录 `VERSION` 和 `versions/TAB5` 读取可用版本；旧 `--release060` 参数保留为兼容别名。
- `tab5-settings-firmware.png`：TAB5 源码 commit `08de19b62e2e36cad0aac6d26b5e21e735ece401` 的原生 LVGL 设置页，演示版本为 .149。其余控件和布局未变。

捕获与逐图哈希记录保存在本地 `artifacts/docs-sync-061-149/`；不启动运行中的桥接，不读取私人配置，不操作设备。

## 验证与历史

本次审查发现旧截图生成器误用已退役的 `TrayMenu`。现已改为生产托盘使用的 `DeviceCenterMenu.Build`，并重新捕获、检查当前设备分组菜单。功能图鉴的入口同步改为设备中心三页布局；不再把旧七组菜单当作当前界面。

早前批次已检查 52 张重新生成或新增的公开截图，重点窗口另以原尺寸复核。精确路径和 SHA-256 登记于 `licenses/materials.json`；公开内容检查同时约束路径与字节。相同布局可能产生相同哈希，不表示仍用旧代码。Windows 字体、主题与绘图库会影响外观，不承诺跨机器字节一致。

`assets/guides/*.svg` 为独立绘制的流程示意，不是刷机结果；`hero.*.svg` 与 `scenes.svg` 保留为历史素材。历史上 2026-09-16 的导入桌宠和后续 BYTE SPROUT 预览不作为本次默认形象变更证据。源码中的显式 APET 捕获入口仍保留，但本次未读取个人 APET。安装过程中的原始备份和运行日志不公开。

## English

Synchronized 2026-10-07 for Windows AI-bot 0.6.1 and TAB5 0.2.149-ui. Only the version-bearing Update Center, Device Center and TAB5 firmware settings frames were refreshed; unchanged screenshots and the existing film are retained with their original capture provenance. Public screenshots are native WinForms/LVGL renders with isolated synthetic data, not hardware photography or new acceptance evidence. The Windows capture tool starts no bridge, serial service, account refresh or authentication browser. Update and collection windows use fixture release/install metadata; they perform no downloads, imports or flashing. Device Center now shows a synthetic `.149` device. Available update versions come from VERSION and versions/TAB5; --release-current replaces the legacy --release060 name, which remains an alias. The native firmware settings frame uses TAB5 source commit 08de19b62e2e36cad0aac6d26b5e21e735ece401 and a .149 fixture label. Reviewed bytes are registered in licenses/materials.json. Future release checks compare visible content before replacing media, preserving unchanged captures and historical acceptance records.

The earlier .145 TAB5 previews cover Codex Direct, Annual Dots in three colors, the floral calendar, landscape/portrait art, quotas, tasks, voice and calendar. Portrait screenshots are rotated to their physical viewing orientation. The 108-second film uses selected captures; it is not an exhaustive feature tour. Separate offline fixtures exercise native voice/camera handlers and simulated results without sending messages or contacting speech services. The original project character supplies sample camera and album artwork. Optional pet designs do not alter installed defaults.

The earlier gallery update added 26 reviewed native TAB5 captures: nine freshly rendered overview/apps/tasks/settings/pet/classic-clock frames and 17 selected from the current film capture library. They cover the ordinary pages and draft workflows as well as highlights. Fresh LVGL captures use source commit `02df4f2dbcb1c8535af7f5ffd518ae39f52844c1`, a synthetic `.145` label and original project pet artwork. The existing ESP8266 mirror frames were also reviewed across all displayed page groups; they are not new physical-device captures. This update changes documentation and reviewed media only.

Art attribution: Lin Liang, *Peacocks and Bamboo*, Cleveland Museum of Art 1964.242, CC0; Wang Sishi, running-script seven-character poem scroll, Tokyo National Museum / ColBase TB-6, CC BY 4.0. Source and license links appear in the table above. Images were resized and laid out for the gallery; portrait native frames were rotated upright. These licenses remain separate from the project's MIT terms, with no museum endorsement.

The earlier batch contained 52 regenerated or added screenshots. A subsequent audit found that the tray fixture still used the retired TrayMenu factory; it now calls the production DeviceCenterMenu.Build factory, and the corrected grouped menu was recaptured and visually checked. Exact bytes and paths remain enforced in `licenses/materials.json`. Fonts/themes may affect cross-machine appearance. Historical diagrams are identified as illustrations, and private backups/runtime data are not published. See the separate hardware acceptance and video provenance records.
