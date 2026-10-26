# Mac 最新源码参考与移植边界（2026-10-22）

本记录汇总 Windows 分支 `codex/windows-tab5-multihost-20261021` 与 TAB5 `0.2.252-ui`，供 Mac 当前本地开发代码对照。用户已授权把本轮改动本地提交并提供参考；没有推送、发布、修改 Mac 工作区或再次刷机。参考包 `MANIFEST.json` 记录精确提交、固件身份及逐文件 SHA-256。

## 包内内容

- `reference/`：该提交的 Windows 桥接、登录方式辅助程序、实验探针、协议、额度说明、版本补丁及许可文件。Windows 特有实现仅作参考。
- `tab5-source/`：当前 `.252` 固件源码、相应脚本和测试，按已公开 `.234` 源码清单及后续补丁新增路径选取；不含编译输出、下载工具链、配对数据、用户照片和运行日志。
- `tab5-baseline/`：公开 `.234` 的重建说明、SDK 差异及许可信息，明确标注为基线材料。新增 SDK 修正见 `tab5-source/scripts/apply-jpeg-dma-memory.py`；不能把 `.234` 的旧源码清单当作 `.252` 清单。
- `verification/`：本轮合成专项检查结果和固件补丁重放结果，不包含真实用户采样。

Mac 正在开发的代码尚未提交且基线未知。先保存本地工作，再逐项对照移植；不要用本包覆盖整个 Mac 工程，不直接对未知基线执行旧补丁。`MAC-HOST-BINDING.patch` 的原适配基线为 `bf16f1f9becd172407c51297fadb0b0bce57fb8f`；`MAC-BLE-OWNER-237.reference.patch` 建立在前一适配之上，两者均为参考。

## 优先对照的接口

| 功能 | Windows / 固件参考 | Mac 应保留的行为 |
| --- | --- | --- |
| 多电脑绑定与 USB 接管 | `Tab5HostBinding.cs`、`Tab5Service.cs`、协议中的 trusted computers；固件 `host_binding*`、`usb_link.c` | 每台电脑独立密钥；仅已绑定电脑通过 USB 认证后自动接管。选定电脑后，通道不可用不能改连另一台电脑；旧会话及输入失效。名称备注不能改变身份。 |
| 蓝牙归属 | `Tab5BleClient.cs`、`Tab5Protocol.cs`、两份 Mac 参考补丁 | 先检查 host-binding 特征，再完成原有加密鉴权及短归属证明。非当前电脑待命；公开 hostId 不是授权依据。保留系统配对。 |
| Wi-Fi / BLE 诊断 | `Tab5RuntimeDiagnostics.cs`、`RuntimeDiagnosticsSelfTest.cs`、`LocalStatusServer.cs`；固件 `runtime_diagnostics*` | 使用绑定通道、会话、一次性请求 ID 和有效期；验证设备、电脑及通道。首通道超时可尝试同一绑定设备的另一在线通道；过期数据显示年龄，拒绝迟到旧回复。 |
| 额度累计 | `QuotaHistory.cs`、`QuotaHistorySelfTest.cs`、`Tab5QuotaTrend.cs`、`docs/QUOTA_TRENDS.md` | 以前一次有效读数接续，按可验证重置分段；跨日补记与全天完整统计分开。账号或套餐不同不能跨接，缺采样不当作零。Windows 实际昨日 4% 尚未解决，不能照抄夹具结果作为现场结论。 |
| 扩展密码 | `Tab5PinStore.cs`、`Tab5PinSelfTest.cs`、协议中的 extended unlock credentials | 1–64 字节可打印 ASCII，保留大小写与首尾空格；默认数字键盘。Mac 使用本机安全存储，不能移植 DPAPI 文件或 Windows 登录辅助程序。Mac 实现与实测另行完成。 |
| Codex 会话切换 | `.243` 补丁与 `TAB5-HOST-FEEDBACK-243.md` | 保留会话关联和新鲜度检查；会话已在前台时，固件同步状态能恢复。Mac / Wi-Fi 用户已验证，不应以删掉校验来规避问题。 |
| 相机、附件、屏保、日程 | `.240`–`.252` 源码与记录 | 三张附件增删、四向布局、照片引用和相机/图库内存交接由固件处理；日程超长语音截取 32 字并回填。Mac 只适配实际存在的协议差异。 |

上述 `.cs` 路径位于 `reference/windows-app/AIBotBridge/`；协议位于 `reference/docs/PROTOCOL.md`。Windows 的 PIN/密码入口选择、计划任务辅助组件、TightVNC 默认安装属于 Windows 实现，不能推定 macOS 支持同样机制。

## 固件补丁重放

从公开 `.234` 对应源码开始，按下列顺序应用 `reference/docs/development/` 中的补丁：

1. `TAB5-CAMERA-PASSWORD-235.patch`
2. `TAB5-MULTI-HOST-236.patch`
3. `TAB5-SINGLE-HOST-237.patch`
4. `TAB5-GATT-CACHE-238.patch`
5. `TAB5-CAMERA-240.patch`
6. `TAB5-CAMERA-241.patch`
7. `TAB5-CAMERA-242.patch`
8. `TAB5-HOST-FEEDBACK-243.patch`
9. `TAB5-CAMERA-244.patch`
10. `TAB5-CAMERA-245.patch`
11. `TAB5-CAMERA-246.patch`
12. `TAB5-CAMERA-247.patch`
13. `TAB5-CAMERA-248.patch`
14. `TAB5-CAMERA-249.patch`
15. `TAB5-CAMERA-WIRELESS-250.patch`
16. `TAB5-250-PATCH-CONTEXT.patch`
17. `TAB5-FOLLOWUP-251.patch`
18. `TAB5-FOLLOWUP-252.patch`

`.239` 只作诊断历史保存，`.240` 已包含其改动，不重复应用。`.250` 的编译基线有一处 `ui_dashboard.h` 语句换行未进入旧增量；单独的 PATCH-CONTEXT 补丁补齐该换行，保持旧补丁哈希和最终源码行为不变。

本轮从公开清单核对的基线重放上述 18 份补丁，92 个文件与当前 `.252` 源码一致（仅归一化 LF/CRLF）。另对 `.252` 构建记录中的 21 个源文件核对原始 SHA-256。未重新编译或刷入固件，也不宣称独立逐字节重建相同 BIN。

## 验证状态与未完成项

- 本次 Windows Release 构建零警告、零错误；主机绑定、无线诊断、额度、凭据、Wi-Fi 解锁、统一安装配置、远程升级、登录辅助程序共 8 组自检通过。探针另行构建及自检通过。均不替代真实登录测试。
- 设备中心布局已在前一提交 `54c8d0c` 保存，相关布局检查通过；最终安装后的重启目视核验因用户停止桌面控制而未完成。
- 用户已确认三张照片增删/反复开关、Mac Wi-Fi 的 Codex 直达、电脑备注与状态提示、日程截断回填与布局正常。Windows 现有凭据从壁纸和指纹入口自动解锁通过；一位真实密码仍待测试。
- `.252` 已完成精确镜像启动核验：应用 SHA-256 `ddc7183ffb1d977f4bc08f34617a661681becf54bbf01f6296a3f0fad00a4246`。添加照片蓝闪、附件查看大小、屏保竖向及相机往返仍待用户目视验收；不能把上一版通过项扩大为这些问题已解决。
- 仅蓝牙模式的诊断回退、Mac 当前开发版完整移植及 USB 接管/无线跟随组合、大小写和特殊符号真实登录仍需对应平台验收；Windows 昨日额度仍显示 4 个百分点，现场问题保留。
- “自动选择”连接方式暂无受支持的远程修改接口，未代改设备。`--status-once` 此前被资料目录保护拒绝，未绕过。更大范围的 BridgeUiSelfTest 存在旧版本同样复现的服务状态最小窗口断言失败。
- 在 Mac 上完成实际改动后，运行其当前分支的 `swift test`、`swift build -c release`，再验收实际配对、切换、断线恢复和诊断；Windows 上没有执行或宣称 Mac 构建通过。

## English

This reference snapshot contains the locally committed Windows implementation and TAB5 .252 sources, protocol changes and patch history. Preserve the Mac developer's uncommitted work and adapt the relevant interfaces; the older Swift patches target a stated historical base and must not be applied blindly. Windows-only credential storage, login-helper and installation mechanisms are not macOS implementations.

The verified firmware reconstruction uses the 18 patches above, skips standalone .239 because .240 includes it, and adds the non-behavioral .250 context repair without changing historical patch identities. All 92 reconstructed files match current .252 after newline normalization. Build and synthetic tests are separate from hardware acceptance: blue flashes, attachment size, portrait screensavers, BLE-only diagnostics, actual one-character/password variants, the observed daily quota discrepancy and complete current-Mac integration remain pending. This handoff performs no push, publication, Mac modification or device write.
