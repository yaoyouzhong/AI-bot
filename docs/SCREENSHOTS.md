# 当前截图来源 / Screenshot provenance

2026-10-07 更新，对应 Windows AI-bot 0.6.0 与 TAB5 0.2.145-ui。主页、安装指南及视频中的 Windows 窗口和 TAB5 页面均来自当前原生 WinForms/LVGL 代码的隔离渲染。它们使用固定演示数据，**不是硬件实拍，也不替代真机验收**。本次设备验证另见 [TAB5 .145 验收记录](TAB5-ACCEPTANCE-145.md)。

## Windows 捕获

`tools/doc-capture/DocCapture.cs` 在读取设置前启用公共测试隔离目录。不启动 BridgeRuntime、串口、HTTP 服务、账户刷新或授权浏览器，也不读取用户桌宠缓存。演示设备身份、账户额度、会话和音乐信息均为虚构样例。

```powershell
dotnet build tools/doc-capture/DocCapture.csproj -c Release
dotnet tools/doc-capture/bin/Release/net8.0-windows10.0.19041.0/DocCapture.dll <output-directory> --release060
dotnet tools/doc-capture/bin/Release/net8.0-windows10.0.19041.0/DocCapture.dll <output-directory> --release-ui
dotnet tools/doc-capture/bin/Release/net8.0-windows10.0.19041.0/DocCapture.dll <output-directory> --quota-api
dotnet tools/doc-capture/bin/Release/net8.0-windows10.0.19041.0/DocCapture.dll <output-directory> --design-pet docs/assets/pet/AI-bot-mascot.gif
dotnet tools/doc-capture/bin/Release/net8.0-windows10.0.19041.0/DocCapture.dll <output-directory> --codex-pro-cover --design-pet docs/assets/pet/AI-bot-mascot.gif
```

- `--release060`：使用真实更新窗口与图库管理窗口，模拟发布查询、旧版本设备和已安装图库的元数据。`update-center.png` 是“发现可用更新”的示例；`gallery-packs.png` 的 402/411 件状态来自与发布包一致的固定数据，不是另一次导入验证。不下载、安装或刷机。
- `--release-ui`：当前托盘菜单、设备中心、添加设备、小屏刷机、TAB5 首刷、配对、连接与升级窗口；TAB5 固件演示版本已更新为 `.145`。
- `--quota-api`：固定的 DeepSeek 设置界面，输出 `api-settings-deepseek.png` 后用作公开 `api-settings.png`。
- `--design-pet`：原生镜像和设置窗口；使用批准的项目 GIF 作为桌宠设计预览和虚构曲目的封面。它不改变已安装程序或固件的默认形象。
- `--codex-pro-cover`：为视频生成与 TAB5 演示对应的 PRO、周用量 88%、四张重置卡及六帧桌宠画面。浏览器时间轴选择整页原生帧，不用图层覆盖业务控件。

## TAB5 捕获与艺术作品

当前 `.145` 工程的 `tests/ui-preview/preview.c` 生成 LVGL 原生 PPM；转为 PNG 时保留像素，竖屏图只作 90° 物理方向调整。新图包括 Codex 直达、全年点阵（默认/暗夜/暖灰）、十二月历、名画/书法的横竖排版。额度、任务、日历与语音页面也重新生成。

素材库另用隔离原生测试夹具生成余额、音乐及语音/相机流程；当前 108 秒影片选取其中部分画面，不逐项展示所有功能：实际调用原生按钮处理函数，模拟收音、识别、拍摄确认与附件返回；不调用真实语音服务、不发送消息。相机和专辑封面采用项目原创角色，曲目“桌面之光 · 示例曲目”为虚构数据。本地生成脚本在 `artifacts/product-intro-story-20261007/`，见[视频来源](PRODUCT_VIDEO.md)。

| 画面 | 作者与作品 | 馆藏与许可 | 加工 |
| --- | --- | --- | --- |
| 每日名画横/竖屏 | 林良《孔雀竹石圖》（Peacocks and Bamboo） | [Cleveland Museum of Art，1964.242](https://www.clevelandart.org/art/1964.242)，CC0 | 图库排版、缩放；竖屏原生画面转正 |
| 每日书法横/竖屏 | 王嗣奭《行书七言律诗轴》 | [东京国立博物馆 / ColBase，TB-6](https://colbase.nich.go.jp/collection_items/tnm/TB-6?locale=zh)，[CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) | 图库排版、缩放；竖屏原生画面转正 |

作品图像保留原图条款，项目 MIT 许可不替代作品许可；署名及来源同时保留在原生画面和本页。博物馆未为本项目背书。图库逐件来源与加工记录随独立 ZIP 分发。

十二月历花卉沿用项目已登记素材；新版尖顶蓝紫角色及六帧动画见[桌宠素材说明](assets/pet/README.md)。Codex 等标识仅用于识别对应产品。

## 验证与历史

本次审查发现旧截图生成器误用已退役的 `TrayMenu`。现已改为生产托盘使用的 `DeviceCenterMenu.Build`，并重新捕获、检查当前设备分组菜单。功能图鉴的入口同步改为设备中心三页布局；不再把旧七组菜单当作当前界面。

已检查 52 张重新生成或新增的公开截图，重点窗口另以原尺寸复核。精确路径和 SHA-256 登记于 `licenses/materials.json`；公开内容检查同时约束路径与字节。相同布局可能产生相同哈希，不表示仍用旧代码。Windows 字体、主题与绘图库会影响外观，不承诺跨机器字节一致。

`assets/guides/*.svg` 为独立绘制的流程示意，不是刷机结果；`hero.*.svg` 与 `scenes.svg` 保留为历史素材。历史上 2026-09-16 的导入桌宠和后续 BYTE SPROUT 预览不作为本次默认形象变更证据。源码中的显式 APET 捕获入口仍保留，但本次未读取个人 APET。安装过程中的原始备份和运行日志不公开。

## English

Updated 2026-10-07 for Windows AI-bot 0.6.0 and TAB5 0.2.145-ui. Public screenshots are native WinForms/LVGL renders with isolated synthetic data, not hardware photography or new acceptance evidence. The Windows capture tool starts no bridge, serial service, account refresh or authentication browser. Update and collection windows use fixture release/install metadata; they perform no downloads, imports or flashing. Device Center now shows a synthetic `.145` device.

Current TAB5 previews cover Codex Direct, Annual Dots in three colors, the floral calendar, landscape/portrait art, quotas, tasks, voice and calendar. Portrait screenshots are rotated to their physical viewing orientation. The 108-second film uses selected captures; it is not an exhaustive feature tour. Separate offline fixtures exercise native voice/camera handlers and simulated results without sending messages or contacting speech services. The original project character supplies sample camera and album artwork. Optional pet designs do not alter installed defaults.

Art attribution: Lin Liang, *Peacocks and Bamboo*, Cleveland Museum of Art 1964.242, CC0; Wang Sishi, running-script seven-character poem scroll, Tokyo National Museum / ColBase TB-6, CC BY 4.0. Source and license links appear in the table above. Images were resized and laid out for the gallery; portrait native frames were rotated upright. These licenses remain separate from the project's MIT terms, with no museum endorsement.

The earlier batch contained 52 regenerated or added screenshots. A subsequent audit found that the tray fixture still used the retired TrayMenu factory; it now calls the production DeviceCenterMenu.Build factory, and the corrected grouped menu was recaptured and visually checked. Exact bytes and paths remain enforced in `licenses/materials.json`. Fonts/themes may affect cross-machine appearance. Historical diagrams are identified as illustrations, and private backups/runtime data are not published. See the separate hardware acceptance and video provenance records.
