# TAB5 .161 本地安装与复测 / Local installation and retest

2026-10-10：用户已按下列精确新哈希批准安装并复测；在 .160 完整一小时观察结束后，本地部署配套桥接并完成 .161 Wi-Fi 升级、精确启动核验。三轮测速及完整一小时观察结束，USB 验收失败。原自动轮播已恢复，不能称为全部验收通过。未提交、推送或公开发布。

| 对象 | SHA-256 |
| --- | --- |
| 固件 `0.2.161-ui` | `400e62568267ac75025b6719721044f1cc8c5c5afeb6639e8b9943172e4645f0` |
| 内嵌 ELF | `d18c9da685d21344c680e5b5261aca3c9cb455309cf5406fde655ddd0499f63a` |
| 实际升级说明 | `bb61913ded6498db4c41a645fc2d6c8d60f4ad0c57af7e57868b30b9c5d2d3fd` |
| 配套桥接 DLL | `38ab55303bcaf6cd331619fbc8887753921c4ad455e7dede6b34dd9e482d9257` |

固件固定路径为 `artifacts/firmware/tab5/latest/aibot_tab5.bin`，6,677,568 字节，分区余量 531,392 字节；历史归档为 `versions/0.2.161-ui/400e62568267-bb61913d/`。配套桥接在 `artifacts/development/saver-layout-20261010/bridge/`。旧 .160 镜像归档、运行桥接和验收证据保留。

## 修正与验证边界

- 竖屏图片已预旋转 90°，详情和底部叠加层按实际图片及正反方向对齐。底部仅旋转 98/138 像素区域，避免整个屏幕的透明画布开销。原 JPEG 与图库资源包没有改变。
- 底部以左侧馆藏、居中页码／左右翻页、右侧详情排列；设计时假设横屏标题在原画面中保留，实机反馈确认名画的底部标题被遮住，且年代缺失，后续单独准备参考书法的信息布局包。竖屏保留作品标题与作者。隐藏日期与独立第一页按钮，日期仍在内部参与作品匹配。馆藏过长用省略，详情保留完整信息。
- 原生 LVGL 使用两种分类的真实图片及匹配导读检查正、反横竖屏，要求叠加角度与图片一致、馆藏不碰页码，模拟转换后的真实指针点击 Details/Return。已显示预览并检查完整画面。字库与全部导读覆盖、原页返回、最长标题、未知导读隐藏及缓存保护保持通过；此证据仍不是实机确认。
- USB 拒收增加数值阶段，不记录正文或身份／会话文本。Windows 保持串口锁及原序号，只对压缩状态的旧端未知、解析或展开拒收尝试一次原文。仍要求有效设备／序号 ACK。身份、序号、未知新代码、原文失败、I/O、超时及取消均保持失败；所有原有边界和认证检查保留。
- Windows Release 构建为 0 警告、0 错误；正常桌面 `--status-once` 通过，未覆盖运行桥接。注入压缩拒收、重复拒收、旧端、身份／序号／未知代码拒收、超时、断连与取消，检查一次原文、相同字节／序号与失败传播，通过。固件接收测试验证展开工作区分配失败、解析、信封、身份和旧序号原因，并成功接收相同原文。
- 固件隔离 ESP-IDF 构建通过，源 main 文件与快照逐项 SHA-256 核对；正式准备脚本校验、归档通过。USB CDC 完成／堵塞／重连代次、二进制边界与换行、指标和 delta 接收测试通过。原生接收测试曾因新测试建立 USB 会话后未隔离，以及旧 Git zlib 路径而失败；保留初始日志，修正测试设置和依赖路径后正常通过，未弱化业务断言。

## 实机结果与剩余验收

.160 的真实 USB 仍会拒收和超时；[.160 记录](TAB5-ACCEPTANCE-160.md)永久保留。.161 的一次原文恢复是一项有边界的缓解和诊断措施，不能据此宣布根因已修复。

- 配套桥接于 13:53:35 部署完成，原桥接正常退出并备份至 `runtime-backup-20261010-135328/`，固定 Release DLL 核对为 `38ab55303bca…`。`policy-after-deploy.json` 确认原六个页面、顺序、auto、循环开启及 15 秒间隔保持。
- 实际 OTA 使用 Wi-Fi，4,179,802 字节压缩传输完成，桥接统计 13,093 ms。启动核验确认精确内嵌 ELF、`ota_0`、`VALID`，前次总字节 6,677,568、保留错误 0、栈余量 3892；保留设备时序和 Wi-Fi 通道代码见 `after-boot-verification.txt`。传输时间不等同于含准备、校验和重启的全过程。
- 用户确认横屏底部馆藏、翻页、详情清楚且无重叠，竖屏详情的方向、位置和返回正常。尚未单独确认全部正反方向。同时报告每日名画预览曾等待约 20 秒，以及横屏作品名、作者和年代缺项，要求参考书法信息布局，竖屏保持精简。新增布局已准备为独立候选图库包并通过本地验证，未安装，也未包含在已安装的精确 .161 镜像中。
- 第一轮三通道测速为 `failed`：13 项完成，USB 下载首项失败，后续四项 USB 因通道失败未执行；Wi-Fi/BLE 各六项完成。USB 上传首项 13,290 ms，下载 0 字节、错误 -1、2710 ms；桥接记录 `write response 0/49312` / `0x80070079`。证据为 `hardware-benchmark-1.json`，不能写成 18 项通过。
- 第二轮为 `failed`：17 项完成，USB 下载第三项在 98,304 字节处失败，错误 -18，设备诊断为 RPC 解密校验阶段；其他五项 USB 与 Wi-Fi/BLE 各六项完成。第三轮为 `complete`，18 项全部完整且错误 0。三轮合计执行 50 项、48 项成功、2 项失败，另四项未执行，不能将第三轮成功视为持续稳定性通过。
- 持续观察从 13:55:49.085580 至 14:55:49.691703，完整 3,600.609 秒、1,782 样本，HTTP 诊断读取失败 0。首失败为 13:56:27（38.422 秒）：USB 回复写入超时，设备 RPC 解码错误；首失败保存在 `hardware-observation-first-failure.json/txt`。
- 相对基线新增 7 条不同 USB RPC 中断文本、6 条不同控制中断文本；USB 不可用 4 样本、两段，最长约 6.062 秒。末次设备诊断为 USB 解密校验失败累计 3，二进制接收过期 1、CDC 写不完整 0；未在上报运行时间中观察到重启。文本数量与 2 秒采样不能等同于物理断连次数或精确不可用时长。
- 一次原文恢复累计 `attempts=7; confirmed=7`，此缓解已实际触发；其他 USB 数据／控制中断仍保留，整轮 RAM 完整性与持续稳定性均判定失败。最终汇总为 `hardware-acceptance.json`；另保留 14:09:47 至 14:54:48 的脱敏运行时异常记录，异常多层传播记录不计为独立物理故障。
- 14:56:19 正常退出桥接、执行 Release `--restore-cycle-default` 并正常启动。`closeout-161-result.json` 和 `policy-after-161-closeout.json` 确认 auto、循环开启、原六个页面／顺序及 15 秒间隔保持，桥接 DLL 仍为 `38ab55303bca…`。

设备测试所需的网络自动模式暂时保留；后续 .162 安装与名画布局包需单独批准，最终仍须恢复用户原仅蓝牙选择。每日名画约 20 秒等待的用户反馈尚未完成端到端复测，不能用后来单次下载／解码时长宣称修复。

## English

The separately approved exact .161 firmware and matching bridge were installed locally. Actual OTA used Wi-Fi; USB boot verification matched the ELF and VALID partition. The user confirmed the landscape footer and normal portrait Details direction/return, while reporting a roughly 20-second painting-preview delay and missing landscape artwork metadata. The separately prepared painting candidate is not installed. Three hardware suites executed 50 cases: 48 succeeded, two USB cases failed and four USB cases were skipped. The complete 3,600.609-second observation collected 1,782 samples without HTTP read failures; USB stability failed after 38.422 seconds. Seven raw recoveries were confirmed, but later data/control interruptions remain. Final reports preserve diagnostic sampling limitations. Normal bridge exit, Release carousel restoration and restart completed, confirming the original six pages/order at 15 seconds. Temporary automatic network mode remains for pending retesting; gallery latency and all physical orientations are not fully accepted. No public release or Git submission occurred.

Evidence: `artifacts/development/saver-layout-20261010/`; original .160 evidence: `artifacts/development/saver-usb-20261010/`.
