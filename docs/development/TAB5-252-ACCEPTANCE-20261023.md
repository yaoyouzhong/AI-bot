# TAB5 .252 统一验收（2026-10-23）

用户要求先完成剩余验收、汇总结果，再集中修复。本轮先记录现有版本的行为，不修改程序、系统凭据或刷写固件；此前的提交/推送授权不延伸到后续新改动。

## 当前基线

- 开发分支 `codex/windows-tab5-multihost-20261021`，最新已推送提交 `79ffe7fb6b41819322283c2e151d52ca0b4f8204`。
- 设备运行 `0.2.252-ui`；对应已验镜像 SHA-256 `ddc7183ffb1d977f4bc08f34617a661681becf54bbf01f6296a3f0fad00a4246`，本轮不再次刷写。
- Windows 固定运行目录中的 DLL SHA-256 `9526a26192278de1936904cc51bbeb9144da873da1239ff015f2c17e81bc773c`，与设备中心已安装候选一致。
- 本轮实际运行诊断、额度边界及后续测试证据保存在忽略目录 `artifacts/development/acceptance-252-20261023/`，不读取照片内容或凭据。

## 已得到的结果

| 项目 | 结论 | 证据及界限 |
| --- | --- | --- |
| 名画、书法横竖屏 | 通过 | 用户确认恢复正常。 |
| 相机与屏保反复切换 | 通过 | 用户确认功能正常。 |
| 相机首次打开 | 通过 | 用户确认首次也没有报错；不额外要求重启。 |
| 添加照片蓝闪 | 未通过 | 用户在 .252 确认仍存在。新鲜 USB 诊断虽显示旧的显示错误/欠载计数为零、照片处理及上传成功，也不能推翻可见故障。无需为确认已知失败再次拍照。 |
| 照片添加后的“可与文字一起发送”提示 | 待修改 | 用户明确要求去掉。先记入集中修复清单。 |
| 附件查看页“返回后可继续编辑或发送”提示 | 待修改 | 用户明确要求去掉。先记入集中修复清单。 |
| 10 月 21 日的额度累计 | 未通过 | 当前桌面桥接诊断仍为 4 个百分点、partial=true、carryOver=0；第一读数 61、末次 65，前一有效读数 35 位于 10 月 20 日 09:42。后续通过正常桌面复制原始历史，核实同一账户/套餐及两个未来重置时间变化；用户确认该断档未用卡。新算法只读重算为 30（含跨日补记 26），仍标部分记录和周期待核实；尚未安装候选。 |
| 新额度采样及设备传输 | 通过（限链路） | 运行诊断有新采样、storageError=null；daily 与 transmitted 数值一致。只证明采集/传输在工作，不证明上述历史累计正确。 |

此前三张附件增删、日程 32 字截断回填及布局、Mac/Wi-Fi Codex 直达、备注和状态提示、Windows USB/Wi-Fi 使用现有 PIN 解锁的通过记录继续保留；不把旧测试扩大为新组合均已通过。

## 剩余本轮测试

| 项目 | 当前状态 | 验收方式 |
| --- | --- | --- |
| 附件横图、竖图的查看大小 | 显示大小已反馈，布局偏好待确认 | 用户反馈“占大半个屏幕，不是全屏”。当前布局保留标题和返回栏，按可用区域等比例显示；不直接把未铺满整屏判定为缩放失败，也未据此宣称用户已认可。另询问是否希望接近全屏、仅保留返回按钮。 |
| Windows 设备中心最终安装界面 | 通过 | 用户确认“设备中心功能显示完整”，补齐已安装版本的实窗验收。 |
| 仅蓝牙的无线诊断及同设备回退 | BLE 实时诊断通过 | 用户确认按“仅蓝牙”条件就绪。两次正式诊断均为 USB 未连接、BLE 实时采样；设备运行毫秒由 44313148 增至 44345963。Wi-Fi 仍联网并与桥接通信，但诊断实际经 BLE 返回。每次首尝试的内部时序未单独输出，不夸大为全部超时路径均完成真机覆盖。证据见 `ble-diagnostics-accepted.json`、`tab5-ble-first.txt`、`tab5-ble-second.txt`。 |
| BLE 现有凭据完整解锁 | 壁纸页通过；指纹页首次失败 | 用户反馈壁纸页一次成功；指纹页第一次失败、第二次成功。设备保存 attempt=1/2/3 均为 BLE，结果 success/failure/success，耗时 8184/1273/6023 ms；中间失败为 `credential_transport_failed`，`hidSubmitted=False`，不属于已发送凭据后的解锁确认失败。最近一次辅助组件仅证明最后一次 PIN 准备成功，不能反推前一次状态。没有证据将 07:06:40 的旧 GATT 中断归因于本轮。根因待验收汇总后定位，现不要求重试。见 `ble-unlock-acceptance.json`。 |
| Mac 当前开发版的 USB 接管/无线跟随组合 | 通过（用户已测结果） | 用户确认 Mac 已适配最新绑定逻辑且能连接，并确认“已绑定 USB 自动接管、拔 USB 保持所选电脑、通道不可用不跳到另一台”三项均正常。不要求重测；没有将 Windows 单侧诊断当作 Mac 端记录。 |
| 一位真实密码、字母/符号凭据 | 条件性延期 | 一位密码此前由用户明确推迟；不为验收改 Windows 系统密码。仅当已有相应凭据且用户准备测试时实测，聊天中不收集内容。 |

仅为已有安装组件重复安装、同版固件重刷或改用假凭据不能补齐真实验收。全部可执行项已汇总，开始处理已确认故障；条件尚不具备的真实凭据变体保留延期。附件预览未收到新的全屏偏好，沿用现有布局，仅移除用户明确要求删除的提示。

## English

The user requested acceptance of the remaining current-build behavior before further fixes. Painting/calligraphy orientation, repeated camera/screensaver transitions and first camera opening are accepted by the user. Blue flashes still fail; two redundant photo hints are queued for removal. Current desktop diagnostics still report October 21 as four points with no carry-over, so that issue remains open despite functioning sampling/transport. The user has now accepted the final device-center window and confirmed the forced-BLE setup. Two live BLE diagnostic samples advanced from uptime 44313148 to 44345963 ms with USB disconnected and Wi-Fi still available; this establishes fresh BLE diagnostics without claiming every internal timeout path is observable. The user then reported wallpaper unlock succeeding once and fingerprint-page unlock failing once before a successful second attempt. Device attempts 1/2/3 confirm BLE success/failure/success; the failed credential request had no HID input submitted. It remains a failed first-attempt reliability case with no established root cause. The attachment viewer occupies most of the screen under its current fit-with-toolbar layout; a near-fullscreen preference is awaiting confirmation. The user confirmed the current Mac bridge is adapted and connects, and explicitly accepted trusted-USB takeover, selected-host retention after unplugging, and no cross-host wireless fallback. No repeat was required. Real credential variants remain conditional. The acceptance phase itself made no code changes or device writes. Follow-up work is recorded separately in TAB5-FOLLOWUP-253.md; it does not turn the failed .252 blue-flash result into a pass.
