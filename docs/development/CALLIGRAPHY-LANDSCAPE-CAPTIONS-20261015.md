# 书法横屏信息整库修正

今天的《黄州寒食诗帖》横向原图把作品名、作者写在 y=635/641；TAB5 的不透明导航栏从 y=622 开始覆盖，且横向分支没有绘制年代。设备端已经按“横屏信息在图片左侧”的约定隐藏重复标题，因此旧横向图片会把三项信息全部漏掉。

本次从已安装的 `2026.10.12.32` 书法包出发，核验 1,371 个独立原图哈希后重新排版全部 366 件作品的 1,901 张横屏图。三项信息统一完整换行置于 x=100–450、y<622，作品在右侧完整等比展示；长标题不省略。1,901 张竖屏图逐字节保留；年度作品身份、顺序、图片页数、已审定的空白页排除记录、原文和七篇长文全文均保持不变。名画样本重排前后 JPEG 哈希一致。

32 件原来为空的年代已补齐：31 件采用 ColBase 日文接口的同馆藏官方年代记录，一件采用 Met 的官方时期字段。原始记录、馆藏身份和 SHA-256 见 [reviewed-periods.zh.json](../art-guides/reviewed-periods.zh.json)。拓本单列原刻年代与拓制年代；TB-495 馆方年号/公元年存在冲突，本次仅保留“唐”，不推断具体年份。

两个图像生成入口共用同一个信息面板；年度组包保留最新年代核实记录。打包器拒绝仍使用旧排版、缺少三项信息或图片说明与导读不同的书法包，避免旧资源重新进入交付。

## 本地验证

- 全部 1,901 张横屏 JPEG 的标题、作者、年代像素与导读逐页对照通过；完整文字没有省略，全部位于底栏上方。所需 861 个字符无渲染缺字。
- 基于实际 TAB5 LVGL 源码重新编译预览，七组实际图片夹具覆盖今天三页、长英文标题、长年代、补充年代和已排除空白页的作品。四个方向、详情、返回通过；横屏信息区与输入 RGB565 图逐像素一致，误差 0。
- 缺布局、图文不符、空年代和原故障图片的反向用例均被拒绝。既有导读打包回归通过：2,632 条导读身份/边界、12 种拒绝用例、名画/书法独立组包。
- 年度锁定顺序、3,802 张 JPEG、完整参考正文和来源哈希验证通过。Windows 正式消费者隔离导入通过，逐日双朝向读取、七篇长文分段全文 SHA-256、旧协议升级提示和错误页拒绝均通过；见 `consumer-verification/annual-pack-verification.json`。
- `git diff --check` 通过。本次未修改 Windows 或 TAB5 固件代码。

## 交付及现场边界

包：`artifacts/development/calligraphy-complete-caption-20261015/packs/AI-bot-DailyCalligraphy-2026.10.15.1.zip`，911,365,786 字节。

SHA-256：`92a8cc39c352f263741c9e7029cb3ac693a90756db9edeb711e3e40b93ead650`。包内含许可证、第三方声明和导读，外附 SHA-256。

证据目录：`artifacts/development/calligraphy-complete-caption-20261015/`，包括 `validation.json`、`annual-validation.json`、`caption-pixels.json`、`native-verification.json` 和 `consumer-verification/`。

已由用户通过当前桥接的图库管理正常导入 `2026.10.15.1`，界面确认 366 件、图片和导读一起更新；旧包保存在 `calligraphy.backup-f657a263304848faa948906d0fba93ea`。名画 PACK.json 哈希保持不变。TAB5 已通过 Wi-Fi 请求新版书法数据，设备重新建立的离线书法缓存仍在后台补齐，不能把今天显示成功称为全部离线文件已完成。

用户在真实 TAB5 上确认今天《黄州寒食诗帖》横屏的作品名、作者和含 1082 年的年代说明均完整显示。这是本次故障的实机验收；全年度逐页覆盖仍是 JPEG 像素检查与原生 UI 回放，不称逐件实机查看。

收尾 `/status` 确认 `displayPolicy.selectedMode=auto`、`cycleEnabled=true`，原六页顺序与 15 秒间隔保持不变。正常退出/重启桥接的命令被自动审批拦截（未给出具体原因），采用现有显示设置界面完成相同模式的实时恢复，未执行该重启命令。临时副本的递归清理也被自动审批拦截，文件保留；交付包、旧版备份及证据均保留。本次未刷机、未推送或公开发布。

English: The installed annual calligraphy pack had horizontal image captions underneath the opaque TAB5 footer and no rendered period. All 1,901 landscape pages now share a complete left information panel. Museum-backed corrections fill 32 missing period fields; portraits, source images, annual identity/order and complete texts are preserved. Every caption was checked against actual JPEG pixels; seven native LVGL fixture groups passed four orientations and reader return with zero information-area pixel differences. Packaging rejects unsafe or stale calligraphy captions. The corrected pack is now imported into the running bridge, with the previous edition backed up and painting unchanged. The user confirmed all three fields on today's actual TAB5 landscape display. Background offline-cache rebuilding continues; full-library pixel checks are not represented as viewing every work on hardware. Automatic cycling was restored through the live UI with the original pages, order and interval, after automatic approval rejected the restart command. No firmware flashing or public release occurred.
