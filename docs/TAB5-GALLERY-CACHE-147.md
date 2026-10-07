# TAB5 .147 图片缓存与下载 / Gallery cache and download

2026-10-07。用户授权后已安装，精确镜像启动核验通过；三通道下载与重复查看已有实机记录。缓存重复查看达到即时显示，BLE 首次下载仍慢，不能据此称首次提速目标已达成。用户无 microSD，要求首次查看同步横竖两版、重复查看不再下载，同时强调第一次请求本身也必须快。

## 改动及边界

- 设备保存经过 SHA-256 验证且解码成功的 JPEG，按类别、日期、页码、方向索引，最多 16 项、实际分配容量合计 2 MiB，按最近使用淘汰。缓存命中直接硬件解码，不请求 manifest 或图片分块；断网也能读取缓存。
- 沿用现有显示调度：当前方向先显示，接着自动下载同页另一方向。一次查看触发两版同步，协议仍是各自的 manifest/分块，并非单个网络请求。在另一版首次下载完成前旋转仍可能等待。换类或翻页时清除退役输出槽的重试期限，避免已空槽仍等待 30/60 秒。
- 无卡时只保证当前开机期间、尚未淘汰的图片；重启、内存不足及相机/附件/语音/OTA 启动可回收。取出缓存后所有权交给解码线程；回收代次阻止旧任务在前台操作中重新填回缓存。保持两块 RGB565 输出缓冲，不缓存更多大位图。同一天电脑替换图库后，已缓存图片可能保留至淘汰或重启。
- 电脑端最多复用四张 JPEG 的字节与 SHA-256，所有通道共享；每次仍核对文件长度/修改时间，更新或删除后失效。避免每发送 48 KiB 就重读并散列整张图片；鉴权、会话、防重放与哈希检查不变。
- 固件 Wi-Fi 图片下载期间使用既有临时性能策略，结束或失败后释放并恢复原省电设置。电脑端只在认证成功的 BLE 图库请求后续租 3 秒大数据调度，衔接 manifest 和分块；错误请求及 USB/Wi-Fi 请求不续租。不修改 BLE window64/native7，不减少图片清晰度或改变日常 USB → Wi-Fi → BLE 优先级。

## 验证

- Windows Release 构建 0 警告、0 错误；正常桌面环境 Release `--status-once` 成功。图库自检 726 次鉴权请求、三通道各 12 张真实可解码合成 JPEG（两类别、两方向、三页）；真实回环 HTTP 与生产 USB/BLE mailbox 的模拟 I/O 均重组精确字节。
- 电脑端缓存测试：manifest 后独占锁定源文件，所有分块继续成功，证明未重复读文件；同长度替换及删除使缓存失效。BLE 租期、拒绝请求、不阻塞自身图片读取，以及 RPC、BLE 调度/碎片/取消回归通过。
- 固件实际下载线程在 native harness 中使用模拟传输和 JPEG 硬件，三链路各填充 12 张，再断网读取：共 108 次解码、72 次离线重开，重开期间零 RPC。日期隔离、错误哈希不缓存、HTTP 初始化失败释放 Wi-Fi 策略通过；不等于真实设备 JPEG 解码或链路速度。
- 缓存容量/LRU/所有权/回收代次、DMA 同步及失败缓冲保留、相机所有权、OTA 故障矩阵、Wi-Fi 策略恢复、HTTP POST 回归通过。原生 UI 的 24 次分页/96 次方向检查和双缓冲约束通过。
- ESP-IDF `.147` 构建通过。传统 `--self-test-tab5-gallery` 入口依赖内置全年图库，标准 Release 不内置该目录，因此该入口本次未执行完成；本次没有修改图库内容，改动路径由上述独立合成图库测试覆盖，未声称重新完成全年素材验收。
- 证据：`artifacts/development/gallery-cache-147/`；实际 sidecar 通过原生更新说明页预览。下方记录 `.147` 安装；真机提速结论单独验收。

## 已授权安装与启动核验

- 20:41:11 正常退出旧桥接并启动已授权候选，未改启动项；确认标准用户资料目录、配对文件匹配、非测试模式。
- 用户在设备点击升级，Wi-Fi 压缩传输 4,071,420 / 4,071,420 字节，传输耗时 10,953 毫秒。该数字是固件升级传输，不是图片下载耗时。
- 20:43:05 核验完成：精确 ELF `e7b64e…12b56f`、`ota_1` / `0x700000`、`VALID`，三个递增 uptime 样本，显示错误与欠载均为 0；方向传感器 ready、error=0、样本年龄 38 毫秒。证据为 `boot-standard147-verified.json`。
- 核验后正常启动配套桥接，恢复并确认原自动轮播、六页顺序及 15 秒间隔。设备图片请求/缓存计数仍全 0，首次性能验证先从 BLE 开始，避免预先填入缓存。

## 图库与更新路径复核

- 第一轮实机预览返回 `gallery_pack_missing`。部署前只确认了标准资料目录和配对，遗漏原图库仍在旧运行目录 `artifacts/development/tab5-ble-ota/bridge-all-transports/Assets/DailyArt`；此时标准独立图库目录不存在，不能把这些 404 作为下载性能数据。
- 已将与旧目录 catalog 完全一致的本地 `art-packs-145` 两包，通过正式 `GalleryPack.Install` 导入 `C:\Users\yaououzhong\AppData\Local\AI-bot\DailyArt`。20:52:10 完成 402 件名画/804 张图片、411 件书法/3,000 张图片的逐文件 SHA-256、目录及 JPEG 校验；旧运行目录及包保留。恢复记录：`restore-gallery-result.json`。之后预览无需重启桥接。
- 本地固件路径为本仓库 `artifacts/firmware/tab5/latest/aibot_tab5.bin`，当前 BIN、sidecar 和已启动 ELF 对应 `.147`。手动固件选择与图库 ZIP 导入均使用系统文件选择器，未设置固定初始目录；选择窗口所在目录并非导入目的地。
- 正式图库导入及“打开图库目录”共用 `%LOCALAPPDATA%\AI-bot\DailyArt`；电脑更新下载使用 `%LOCALAPPDATA%\AI-bot\updates\<随机目录>`。先写 `.part`，完整校验后才提供安装；TAB5 下载 ZIP 解出匹配的 BIN/sidecar 交给已有升级窗口，未误用首刷包。
- 本机登录启动任务指向 `windows-app/AIBotBridge/bin/Release/net8.0-windows10.0.19041.0/AIBotBridge.exe`；其 DLL 与本次候选 SHA-256 相同，文件存在。本机尚无默认安装目录 `%LOCALAPPDATA%\Programs\AI-bot\AIBotBridge.exe`；该路径是安装器的默认目标，不是本次临时运行路径。未改系统启动配置。
- 更新路由/哈希/取消/版本与设备限制自检通过（`update-path-regression.log`），未实际执行公开版安装器；不能据此宣称已完成安装器升级验收。

## 候选身份

- 固件：`artifacts/firmware/tab5/latest/aibot_tab5.bin`，`0.2.147-ui`，6,530,000 字节；历史 `.146` 包保留在 `versions/`。
- 镜像 SHA-256：`d78ec25572ca206617f8f907725cca48a0a89be188869fadc8a75cc4ae38472a`。
- ELF SHA-256：`e7b64e63d10ccbaec96104c5b7eb8f1f7e55fa777930e73a1fa34db92812b56f`。
- 说明 SHA-256：`ec754304a6c62abef4d9fb22ab5948a0dbe017e0be433b5725e313fbf4d55733`。
- 电脑端本地运行候选：`artifacts/development/gallery-cache-147/bridge/AIBotBridge.exe`；仍属未发布的 `0.6.0` 本地构建，DLL SHA-256 `8c2429adf5c6ed26e47d61a575bbeffc611a3348dce4aabf91518ede91910474`，目录附许可证、依赖说明及逐文件校验。不改变 Windows 启动项。

## 实机性能与未完成项

同一竖屏书法 JPEG 为 250,678 字节，同一竖屏名画为 435,373 字节。以下是固件下载线程的测量，单位毫秒，不包含点击到触发下载的等待；这是当前版本的链路比较，没有同条件旧版本样本，不能当作升级加速比例。

| 内容 / Content | BLE 下载 / Fetch | Wi-Fi 下载 / Fetch | USB 下载 / Fetch |
| --- | ---: | ---: | ---: |
| 书法 / Calligraphy | 6,953 | 1,469 | 1,050 |
| 名画 / Painting | 9,484 | 2,042 | 1,716 |

BLE 书法解码 134 毫秒，总计 7,087 毫秒；BLE 名画解码 18 毫秒，总计 9,503 毫秒。名画缓存命中下载为 0、解码 19 毫秒，未新增图片 RPC。用户确认重复进入与旋转即时显示；BLE 首次横屏约 5 秒、竖屏约 10 秒，仍不满足首次也要快的目标。逐样本证据在 `artifacts/development/gallery-cache-147/hardware-observations.jsonl`，归纳脚本为同目录 `summarize-gallery.py`。

后续 USB 重连采样将延迟定位到 COM9 出现之前：重启后约 24.9 秒才枚举出串口，随后约 0.8 秒完成连接，其中桥接握手 172 毫秒。用户观察拔插在 10 秒以内、重启约 20 秒；随后四轮恢复快速，管理员跟踪中的三轮应用枚举约 200 毫秒、零重试，用户确认基本界面启动后即连上。慢案例底层根因尚未捕获，不能称已修复，详情见 [USB 重连定位](TAB5-USB-RECONNECT.md)。历史 Codex 偶发“未确认”仍无可复现样本，本轮 BLE Codex 操作成功不能证明偶发问题已消失。原自动轮播、六页顺序及 15 秒间隔保留。

`galleryDiag` 前九项保留，追加 `cacheHits,cacheMisses,cacheBytes,fetchMs,decodeMs,totalMs`。命中缓存的 `fetchMs=0`；这不包括 UI 首次触发前的等待和刷新时间。缓存统计为开机累计，缓存容量为当前保留容量，不包括工作线程暂时取出的 JPEG。

## English

This installed local candidate retains validated JPEGs in a bounded 2 MiB device RAM cache, automatically warms the other layout after the current page appears and supports offline cache hits without image RPC. Reboot, eviction or foreground memory reclamation requires another download; same-day collection replacement may remain stale on the device until eviction/reboot. Preserve two decoded frame buffers and full image quality. Desktop snapshots reuse bytes/digests for up to four JPEGs while checking file metadata. Wi-Fi temporarily uses the existing bulk power policy, and authenticated BLE gallery requests retain throughput scheduling across ranges. All three links retain their original priority.

Release/native builds, 726 authenticated simulated/loopback requests, exclusive-file-lock and replacement tests, 72 offline worker reopens with zero RPC, bounded ownership/reclaim/DMA checks and relevant transport/UI regressions passed. The legacy bundled-collection test lacks its optional data in the standard build; this change did not recurate or revalidate annual assets. Authorized installation passed exact .147 ELF/ota_1 VALID checks with three advancing uptime samples, zero display errors/underruns and a healthy orientation sensor. Firmware transfer took 10,953 ms; this is not image download timing. Original auto cycling was restored. Cold image transfer speed and screen acceptance are now being measured. The earlier USB indicator delay and intermittent Codex message remain unresolved.

Hardware image fetch timings are listed above. Cache hits required no image RPC (0 ms fetch, 19 ms decode in the recorded painting sample), and the user confirmed immediate repeated viewing and rotation. First BLE fetches still took 6,953/9,484 ms for the recorded calligraphy/painting, so the first-download speed goal remains unmet. There is no matched old-version baseline for a speedup claim. USB startup delay was localized before COM9 enumeration; the measured desktop handshake then took 172 ms. The USB delay and intermittent Codex message remain open.

The first hardware attempt exposed a deployment omission: collections were still bundled in the old executable directory. Both identical local packs were then imported through the production importer into the independent user-data directory, validating all 3,804 images and retaining the originals. Early 404 attempts are excluded from performance evidence. Update downloads and collection import use separate LocalAppData directories. Manual file pickers do not fix their initial folder. The login task targets the Release executable with the same DLL hash as this candidate; startup configuration was not changed. Update routing regressions passed, but no public installer was executed.
