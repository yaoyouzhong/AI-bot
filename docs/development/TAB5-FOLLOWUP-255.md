# TAB5 .255：添加照片静态画面、蓝牙恢复与停止升级

## 当前状态与范围

2026-10-23，本地候选 `0.2.255-ui` 已构建；Windows 桥接与精确固件均已安装并核验。新增 ATT 崩溃修复见 [蓝牙修复记录](TAB5-BLE-CRASH-20261023.md)。首次 BLE 图片上传已通过；蓝闪仍失败，原 BLE OTA 崩溃场景及传输中停止仍待验收。Mac 源码、既有配对、绑定、Wi-Fi、显示设置及草稿附件不在修改范围。不提交或推送。

`.254` 精确启动核验通过，但用户确认添加照片仍蓝闪；蓝牙首次添加等待很久后失败，第二次成功，Wi-Fi 很快。这些反馈仍判为未修复。构建、模拟界面或零欠载计数不能代替可见画面和首次上传验收。

## 相机静态整屏 hold 候选

- 点击“添加照片”的同一 LVGL turn 接管已经完成刷新的整屏 `fb0`，排空在途显示拷贝并暂缓普通刷新。编码和上传期间保持照片及当时控件的静态画面，避免控件重绘并发搬运整帧。
- 直接持有已有屏幕帧缓冲，不为 hold 再分配或复制全屏照片。保留原分辨率、quality 85、YUV420 逐行软件 JPEG 与不可变相机租约。
- 成功后先归还扫描输出，再在同一界面轮次转交附件并显示会话；失败保留照片并恢复错误/重试页。恢复 fence 未完成时保持冻结和控件禁用，后续界面轮次继续恢复，避免画面与触摸目标错位。
- 保留四向、三张附件增删、相机重开和屏保借帧边界。此机制仍是蓝闪修复候选，真机效果未确认。

## 蓝牙照片分块恢复

- Windows 在既有鉴权状态的 `data` 顶层通告可选 `imageChunkRetry=1`，表示 `image-chunk` 支持相同图片范围及最终结果的幂等确认。固件仅从当前绑定电脑已鉴权的 BLE snapshot 读取精确数值 1；缺失/其他值不自动恢复。Mac 未新增此能力通告。
- 仅 BLE `image-chunk` 交换使用 15 秒超时；一次照片上传最多一次恢复机会，等待原 host generation、桥接 session 和连接模式恢复最多 20 秒，再用新 nonce 重发同一 SHA-256/offset/字节范围。不是每块各重试一次，不重发会话消息。
- 仅无有效回复的传输失败（本地 status 0）可恢复。身份/模式/session 改变、鉴权/校验错误及其他明确非成功状态均结束；不自动转投其他电脑或通道，不重发 Codex 发送、凭据、HID、USB 请求。Wi-Fi 保持原快速路径。
- `photoUploadDiag` 主四字段描述最近上传；追加首次失败与恢复计数。计数自本次启动累计，首次失败不会被后来的成功覆盖。完整字段见 [协议](../PROTOCOL.md)。

## Windows 停止升级

桥接撤回当前 offer 并终止未完成的普通/压缩 HTTP 流；USB/BLE 后续分段沿用 `offer_changed`，设备通过既有失败清理保留当前启动分区。最后一段开始交付后保守禁用停止，不保证撤回已经交出的最后数据。

- 普通健康缓存、停止之前发起的无线 probe、普通 USB ACK 不能确认停止。无线必须完成停止之后开始的唯一诊断挑战，并匹配原设备、host、版本与 `otaDiag` busy=0。
- USB 使用停止之后 status 的精确 sequence，校验 `currentHostId`、`usbHostAuthenticated`、原固件及空闲状态，修复 USB ACK 没有 `hostId` 导致无法确认的问题。
- ready 已 commit 但尚无真实数据时，至少等原启动命令到期及 6 秒新鲜窗口后再发起确认，避免把“尚未开始”的空闲当作“已经停止”。无 commit 的本地意图可立即撤销。
- 明确 rejected 的启动恢复为未传输状态，晚到 ready 不可重新提交。断连/超时不伪报停止成功；同一电脑恢复且取得新确认后才允许重选固件。

停止功能不新增设备取消命令或修改 OTA 鉴权，Mac 与旧固件兼容。最后段附近连接失败的状态恢复仍保守，不宣称该阶段可以安全强制中止。

## 候选标识

| 对象 | 当前记录 |
| --- | --- |
| TAB5 内嵌版本 | `0.2.255-ui` |
| 固件大小 | 7,176,384 字节（`0x6d80c0`） |
| 固件 SHA-256 | `f7e32ff3da799338017b039ca0565ab5249b0bd8d3e649e2add324ce4081f48d` |
| 原 OTA 槽剩余 | 32,576 字节（`0x7f40`），分区未变 |
| ELF SHA-256 | f9851a2e9b9098f479a58cd6ed04c288e6033e4a805087c8e3d31257ecabc816 |
| Windows DLL SHA-256 | `51c2e3641716f22ff626d701e873e97d5a6e465a566257775411b0a9dae61a1d` |
| 固定交付入口 | `artifacts/firmware/tab5/latest/aibot_tab5.bin`；新镜像和 sidecar 已由正式加载器校验归档 |
| 源码补丁 | 最终 patch 重放与哈希待主 Agent 补入 |

## 已有验证与限制

- 固件构建通过，大小和槽余量如上，分区未改。
- 当前 17 个 native 项顺序通过：camera-viewport、camera-memory-handoff、camera-ownership、camera-end、photo-memory、photo-encode、photo-software-jpeg、photo-upload-chunks、display-direct-state、display-flush-wait、display-refresh-gate、display-idle、runtime-diagnostics、rpc-admission、rpc-exchange、rpc-library-route、boot-verify-worker。包括实际 static hold/恢复、128 对重开/重试、首/中/末块丢回复、精确字节与唯一最终保存、host/mode/session/auth 边界。日志为 TAB5 源码工程 `artifacts/development/photo-followup-255/native-tests.log`；没有按要求提供夹具的额外调用不计为通过。
- 本轮完整 LVGL 回归返回 `LVGL_PREVIEW_OK`，日志为同目录 `ui-regression.log`。模拟结果不能证明真机蓝闪消失。
- Windows Release 构建通过，0 警告/0 错误。OTA/停止升级、runtime-diagnostics（57 checks）、RPC、升级状态及 telemetry 自测通过。日志为本 feature 工作区 `artifacts/development/tab5-followup-255-20261023/`。
- `--status-once` 被既有数据目录保护拒绝，exit 2；没有有效 JSON，不记为运行通过，也未修改或绕过资料目录保护。
- 新 DLL 与精确 .255 均已安装并核验。首次 BLE 图片成功，照片蓝闪仍失败；原 BLE OTA 崩溃及传输中停止需后续实测，未读取用户照片。

## 安装后的验收

1. 核对安装 DLL hash；刷入后核对版本、精确 ELF、运行分区、VALID 状态及递增心跳。
2. 横屏、竖屏各拍照添加，观察照片仍显示时是否蓝闪及耗时；补齐三张，删除首/中/末后各补拍，确认恢复正常。
3. 绑定 Windows 的纯 BLE 连接下，观察第一次添加的结果、耗时与诊断，再确认 Wi-Fi 速度。保留首次失败，不能以第二次成功替代。
4. 可取消阶段停止一次升级，确认设备保留原固件、收到新确认才显示已停止，随后能重新选择/升级；最后段开始后只检查按钮禁用及原因，不强制断电。

## English

The Windows bridge is installed. The exact .255 firmware is installed and boot-verified; its updated payload includes the separately documented ATT crash fix. Blue flashes and a failed first BLE upload on .254 remain failures; a successful retry and fast Wi-Fi do not replace first-attempt acceptance.

The camera candidate fences the already rendered canonical fb0 in the click's LVGL turn, retaining that complete static review through encoding/upload without another full-frame allocation or copy. It preserves full-resolution scanline software JPEG and three attachments. Scanout is restored before ownership/page changes; failed restoration keeps controls disabled until recovery.

Only authenticated data.imageChunkRetry=1 from the selected BLE host authorizes automatic image-chunk recovery. A BLE chunk has a 15-second exchange deadline; one image gets one recovery opportunity of up to 20 seconds, followed by the identical immutable range under a fresh nonce. Only a transport failure without a valid reply is eligible. Host/session/mode changes and explicit errors stop; commands, credentials, HID and USB are not replayed. Mac is unchanged. Numeric diagnostics retain the first failure and cumulative retry counts without photo content.

Windows stopping withdraws the exact offer and incomplete streams. The final range remains non-cancellable. Confirmation requires a new post-stop wireless challenge or exact authenticated USB status sequence, matching host/device/original firmware and idle OTA. Pre-data ready commits wait for original request expiry plus a 6-second grace; rejected starts cannot be rearmed by late ready replies.

Firmware size is 7,176,384 bytes, with 32,576 bytes free in the unchanged slot; SHA-256 is f7e32ff3da799338017b039ca0565ab5249b0bd8d3e649e2add324ce4081f48d. Windows DLL SHA-256 is 51c2e3641716f22ff626d701e873e97d5a6e465a566257775411b0a9dae61a1d. The ELF hash and prepared sidecar are verified. Physical installation passed; see the separate crash record for the passed and pending acceptance items. Seventeen current native tests, full LVGL preview and Windows build/OTA-stop/runtime/RPC/upgrade/telemetry regressions passed. status-once was refused by the existing profile-data guard (exit 2), so runtime success is not claimed. First BLE photo acceptance passed; blue flashing failed. The original BLE OTA crash case and active-transfer cancellation remain pending physical regression.


本轮最终部署及真实结果见 [蓝牙崩溃记录](TAB5-BLE-CRASH-20261023.md#安装及用户反馈本轮最终结果)：首次 BLE 图片成功，照片蓝闪仍失败；传输中停止和原 BLE OTA 崩溃场景待继续验收。没有把最终阶段禁用当作停止成功。
