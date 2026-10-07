# TAB5 .118 本地候选与桥接收尾

2026-10-05。内部检查完成后，经用户授权更新桥接并通过 Wi-Fi 压缩升级至 .118；用户确认完成，USB 精确镜像及启动核验通过。屏保视觉与交互的真机验收仍单独记录。

后续结果：.119 已通过 USB 安装与精确启动核验，用户确认最终字体、预览/轻触退出、重启类型保留及闲置自动进入正常，年度点阵屏保验收完成。详见 [TAB5 .119 验收](TAB5-ACCEPTANCE-119.md)；下文保留 .118 当时的记录，不代表其他桥接/语音待验项也已通过。

Follow-up: .119 passed USB installation and exact boot verification. User hardware observations confirmed typography, preview/touch exit, reboot persistence and automatic entry. Annual screensaver acceptance is complete; other bridge/voice checks remain separate.

## 改动

- 设备“设置 → 屏幕与声音”新增经典时钟/年度点阵选择和全屏预览。年度点阵沿用参考图的浅底蓝点；每点一天，空心蓝圆表示今天；按用户要求移除右下图例。百分比数字保留 Nunito 900 字重，百分号单独使用 400 字重，均为原生 144 px 字模。标题、日期、已过及剩余天数采用 26 px 的 Nunito 800 与 Resource Han Rounded CN Bold 700，增加小字厚度并使用圆润中文笔画。已过天数不含今天，剩余天数指今天之后，百分比向下取整。按设备当前本地日期处理闰年、世纪及跨年；无有效日期时显示等待校时。
- 独立 NVS `saver_style` 默认 0，1 为年度点阵；不迁移原设置结构，不改变 Windows/macOS 显示设置协议。保存失败保留原选择。预览保持当前亮度，轻触返回原页面；正常屏保继续降亮度和触摸/电脑输入唤醒。点阵直接绘制，字体只增加必要字形子集；每分钟轻微移位，不持续动画或反复写 Flash。两种字体的来源、固定哈希及 OFL 许可见 TAB5 仓库第三方清单。
- 额度“距重置”保留原控件坐标与右对齐，仅调整蓝灰色及单位间距，显示为“距重置  4d 11h”。
- 桥接在加载配置之前只读检查真实目录/文件句柄落点。检测到启动器私有目录时拒绝启动并说明从 Windows 桌面启动，无需重新配对；不复制历史、不替换密钥。正常目录不可核实时也停止，避免默默使用空配置。
- Codex 列表继续显示最多 60 个 UTF-16 单元且不截断代理对；完整原始标题仅留在电脑端用于准确匹配。确认目录包含设备精简列表之外的标题，完整同名仍拒绝操作，读取不完整时不确认目标。语音写入/清空/发送的原有防重复与草稿保护保留。

## 内部证据

目录：`artifacts/development/tab5-closeout-118/`；桥接构建日志为同级 `tab5-closeout-build.log`。

- Windows Release 构建零警告、零错误；`profile-guard.log` 核对实际句柄与启动环境。`redirected-startup.log` 证明当前 Codex 启动环境被拒绝，退出码 2；`desktop-status.json` 为正常桌面环境下 Release DLL 的有效状态输出。规定的 `dotnet run --status-once` 在 GUI 宿主仍触发现有控制台句柄异常，日志 `desktop-run-status.log`，不将其记为通过；直接运行相同构建 DLL 验证通过。
- `quick-console.log` 包括长标题、同前缀不同完整标题、跨列表重名拒绝、代理对以及完整标题不进入设备 JSON；同时覆盖连续追加、草稿变化、清空、发送确认和不重复操作。
- `voice.log`、`rpc.log`、`public.log`：合成音频/输入法、模拟加密通道、全新隔离配置回归通过，均不代表真实语音或射频验收。
- `saver-preview.log`、`final-notes-preview.log`：实际 LVGL 页面、屏保、设置入口与真实 sidecar 预览通过；包含 365/366 天、2000/2100 年规则、年末/新年、保存失败回退、预览退出和亮度恢复。
- `saver-prefs.log` 使用生产 NVS 保存/读取代码验证默认值、持久化、非法值和 open/set/commit 失败，不触碰原设置 blob。
- `firmware-build.log`：ESP-IDF 构建通过。MSVC 默认预览构建受既有 C atomic 配置限制；使用项目文档规定的 `-Clang` 通过，保留失败日志，不修改测试断言。
- `compression.log`、`native-zlib.log`、`native-stream.log`：最终精确候选的三通道协商压缩、认证 RPC、逐字节解压及错误拒绝通过。154 向量、162 正常场景、1,636 异常拒绝；原生 PC zlib 适配不能代替设备 ROM/Flash 测试。

## 精确候选与待验

- `artifacts/firmware/tab5/latest/aibot_tab5.bin`，版本 `0.2.118-ui`，6,940,192 字节。
- SHA-256：`e99a2a0eb742072db1de0970b62c45ad303301103cbbbf92ae2c3cd79a270c82`。
- ELF SHA-256：`f7f72a3d795bac56a12a63a2766a8f92c1ffb1a062bc1522b44ebabc51018bc5`。
- 归档：`versions/0.2.118-ui/e99a2a0eb742-e13458da`；较 .117 增加 70,432 字节。压缩载荷 3,619,368 字节，Wi-Fi 含帧头 3,621,640 字节。此前字体/图例调整前的候选归档保留，但不用于本次验收。应用分区剩余约 4%，构建保留接近容量上限提示。
- 真机已验：.117 → .118 Wi-Fi 压缩接收，镜像指纹匹配、ota_0 为 VALID、完整写入 6,940,192 字节、previousStage=9、previousError=0。证据：`hardware/after-upgrade.json`。
- 真机待验：屏保触摸/自动进入与重启记忆、调整后的实际可读性、新桥接长标题及语音集中回归。USB 压缩安装仍另行安排真实后续候选。未提交、推送或发布。

## Wi-Fi 真机耗时与部署

配套桥接的五个文件按内部已测候选替换，旧文件保存在 `hardware/bridge-backup/`，指纹登记于 `hardware/deployment.json`。正常桌面配置启动；原配对时间及记录匹配，未重新配对。部署收尾正常退出后执行 `--restore-cycle-default` 再启动，保留原六页顺序及 15 秒间隔；恢复证据见 `hardware/restore-after.log` 与 `hardware/final-status.json`。

| 记录 | Wi-Fi 未压缩 .116 | Wi-Fi 压缩 .118 |
| --- | ---: | ---: |
| 实际传输字节 | 6,867,152 | 3,621,640 |
| 电脑传输计时 | 14.859 秒 | 14.032 秒 |
| 设备安装至校验完成 | 19.633 秒 | 19.800 秒 |
| Flash 准备/擦除 | 7.926 秒 | 8.037 秒 |
| Flash 写入 | 10.020 秒 | 9.893 秒 |
| 等待接收数据 | 0.220 秒 | 0.025 秒 |

这两次为不同固件、不同时间的现场记录，不是受控 A/B 测试；不能声称压缩带来 Wi-Fi 安装提速。.118 传输量相对其自身原始镜像减少 47.82%，但擦除与写入主导总耗时。网络与写入并行，传输耗时不能与写入耗时直接相加；安装计时不含完成提示停留及重启。旧记录来源 `artifacts/development/tab5-ble-ota/hardware-116/boot-verified.json`。若继续优化速度，应先分析 Flash 准备与写入，在保持备用分区、完整镜像校验和掉电恢复机制的前提下验证。

## English

The .118 firmware adds a persistent annual-dot screensaver and full-screen preview while retaining the classic style. Calendar math excludes today from both elapsed and remaining day counts and handles leap years and rollover. The quota countdown stays in its original position with softer color and clearer unit spacing. The paired bridge rejects launcher-virtualized profile access before loading user state, and keeps full Codex identity titles separate from bounded device labels; ambiguous or incompletely read catalogs remain unconfirmed. Builds, isolated bridge regressions, production NVS fault tests, actual LVGL previews and exact-image compression/decoder tests pass. The GUI-hosted dotnet-run console-handle limitation remains; direct Release DLL status succeeds from the normal desktop. The user authorized deployment and completed Wi-Fi compressed installation. USB verification confirms the exact ELF, ota_0 VALID state, full image size and zero retained OTA error. Installation through verification took 19.800 seconds versus a historical uncompressed Wi-Fi record of 19.633 seconds; these are not controlled A/B samples and do not demonstrate a speedup. Flash preparation and writing dominate despite reduced transmission bytes. Screensaver interaction and voice acceptance remain pending.
