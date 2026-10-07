# TAB5 .123 屏保验收 / Screensaver acceptance

2026-10-06。用户已授权将本次台历、年度点阵暗夜款及此前确认的倒计时配色一并构建、更新设备并验收。桥接二进制保持现有版本。

## 内容 / Changes

- 十二月历：12 组季节配色与图形，农历、节气、官方放假/补班标记。正式屏保只显示当月，跨月/跨年自动更新，没有月份按钮，轻触退出。设备全屏预览保留月份选择，切月不退出。
- 年度点阵：新增暗夜蓝与暖灰，保留浅色款、原字体及空心今日标记。三款共享原有闰年规则、百分比取整及不含今天的已过/剩余天数。
- 设置使用既有独立 `saver_style` 键，旧值 0/1 含义不变；新增 2/3/4，不迁移轮播或配对配置。沿用屏保亮度上限 12（用户亮度更低时保留低值），退出恢复原亮度，预览维持当前亮度。
- 日期/节气/假期使用既有日历模块及桥接数据；未取得对应年份官方安排时显示待更新，不预测补班。新增字体为已有 Noto OFL 字体的 20px 子集；季节图形为本项目几何绘制。
- 额度倒计时保留灰蓝提示，将时间提亮为此前已确认的浅白色。

## 本地验证 / Local validation

证据目录：`artifacts/development/tab5-123-screensavers/`。

- `preview-final.log`：原生 LVGL 全套回归通过，包括 12 个月、五/六行月历、春节/国庆安排、闰日、跨月跨年、未来年份待更新、月份按钮仅在预览可用、屏保退出及电脑输入唤醒。首次测试因合成键鼠样本未增加 sequence 被正确忽略，已修正测试输入并保留失败日志；未放宽生产判定。
- `saver-prefs-test`：所有五种样式持久化、非法值回退、NVS open/set/commit 失败保留原值。
- `calendar-math-test`：72,319 天公农历往返及闰月规则通过。
- 新样式截图来自实际 LVGL 固件 UI，不是前一轮网页设计稿。
- 初次 ESP-IDF 构建被新增行的 misleading-indentation 检查阻止，已拆开独立条件；`build-final.log` 最终 ESP-IDF 构建通过。
- `notes-preview.log` 使用最终 sidecar 完成原生更新说明预览；`compression.log`、`decoder.log`、`stream-decoder.log` 完成本包的 .NET 压缩和固件原生解码比对，包括损坏/截断输入检查。此处时间是本地测试耗时，不是实际 OTA 时间。
- `git diff --check` 在两个仓库通过，仅提示既有换行符转换。

本地包：`artifacts/firmware/tab5/latest/aibot_tab5.bin`，版本 `0.2.123-ui`，6,969,888 字节；镜像 SHA-256 `bae1fb9c17d4c9ab686a453ab0cec3c32144da883f15aaf3c0e5e9f373350d64`；ELF SHA-256 `bba4e5dfb32044003ca2a9bd4baaade55b694d8178b0e651256e404b512c497f`。

## 设备状态 / Hardware status

安装前在线设备为 .122，USB COM9 / Wi-Fi / BLE 均已连接，语音空闲。桥接显示策略为 auto、cycleEnabled=true、原六页顺序及 15 秒间隔，基线保存在 `display-before.json`。

已正常退出原桥接，并使用同一二进制以 `--tab5-offer-ota` 提供新包。真实桌面配置路径、配对一致、auto、cycleEnabled=true、原六页顺序及 15 秒间隔已复核。首次桌面启动中介未指定 Explorer 父进程，配置保护拒绝退出请求，原桥接未受影响；指定真实桌面父进程后正常退出和重启成功，未更改保护规则或复制凭据。

现场刷新确认设备已运行 .123。Wi-Fi 分块压缩 OTA 实际传输 3,641,042 字节，电脑发送 8,687 毫秒；设备 prepareMs=7887、writeMs=9524、verifyMs=927、installMs=19795。USB 三次身份/版本/递增 uptime/界面心跳检查通过，精确 ELF 与镜像相符，`ota_1 VALID`，previousStage=9、previousError=0、previousOffset=previousTotal=6,969,888。证据在 `boot-verified.json`、`ota-hardware.txt`。

核验后正常退出桥接、执行 `--restore-cycle-default` 并正常启动。`status-final.json` 确认 auto、cycleEnabled=true、原六页顺序及 15 秒间隔；`profile-final.txt` 确认配对一致。

用户实机反馈：下方说明文字太多，效果图中的短句未出现，点阵希望自动昼夜切换。此前 .123 原生实现确实遗漏短句，正式模式也没有；改进并入 .124。用户随后明确短句应按月不同、替代左侧两个节气。最终实机视觉/触摸/自动进入验收仍待修订版本完成。编译和原生预览不等于真实屏幕验收。

## English

The user authorized building and installing all changes discussed in this chat. Version .123 adds a seasonal monthly calendar plus night-blue and warm-gray annual styles, and includes the previously approved lighter countdown time. Existing classic/light annual styles, pairing and cycle preferences remain compatible. Normal calendar mode follows the current month without navigation; preview alone exposes all twelve months. Missing official year data is explicitly marked pending. Native UI, calendar math and preference fault checks pass. Compressed Wi-Fi installation and exact-image USB boot verification passed (ota_1 VALID, complete bytes, no upgrade or display error). Original automatic cycling, order, interval and pairing were restored and checked. The user requested a cleaner footer, monthly phrases replacing the cover term pair, and automatic day/night annual colors; these revisions are tracked in .124. Final visual/touch/idle acceptance remains pending. No Git commit, push or public release is included.
