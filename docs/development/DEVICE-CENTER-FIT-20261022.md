# 设备中心窗口适配

用户截图中固定两列的九个入口占据五行，连接升级位于窗口下方，电脑控制说明被省略。当前修复沿用白底、蓝色与 Microsoft YaHei UI，默认客户区改为 1180×660；屏幕边界仍由既有 FitScreen 约束。

入口根据实际文字宽度切换三列或两列，保留原顺序、操作目标和键盘顺序。四个入口的页面保留两列。设备列表按名称与字体测量分配宽度，卡片缩小留白但不缩字体。缩放和收窄窗口后补一次布局，重新计算 FlowLayout 的滚动范围；原先遗留的大宽度会产生无用横向滚动条并导致底部滚动范围错误。

证据目录：`artifacts/development/device-center-fit-20261022/`。保存原源码、旧运行文件、设计说明、构建、布局报告和新旧摘要。

- Release 构建无警告、无错误。
- 七个相关页面/状态、56 个普通和最小窗口布局检查通过；包括受限窗口、全部九个入口、完整说明、顺序及最后入口滚动可达检查。最后调整侧栏宽度后，使用真实默认设备名称的三个页面、24 个布局检查通过。
- 测试使用本机 168 DPI（175%）及不同几何缩放，未改系统 DPI；不能将其称为在不同物理 DPI 显示器上均已验收。中间版本的实际窗口已目视核对；最终文件替换后，用户按 Esc 停止桌面控制，最终重启后的窗口核验尚未完成。
- 额外 BridgeUiSelfTest 的启动设置流程通过，但后续服务状态最小窗口只显示两行的断言失败；原安装 DLL 同样复现该失败。该窗口不在本次改动内，未修改或弱化断言。
- 工具启动的 `--status-once` 被原有资料目录保护拒绝，未绕过。中间版本经正常桌面入口启动，USB、Wi-Fi 和蓝牙连接诊断正常；最终版本不以此前连接结果代替重启验收。
- 最终五个运行文件已备份替换，并与候选逐项核对哈希。最终 DLL 的 SHA-256 为 `9526a26192278de1936904cc51bbeb9144da873da1239ff015f2c17e81bc773c`。

不修改固件、Mac、配对、凭据、额度历史或用户设置。用户随后授权本地提交本次界面优化；其他工作区改动保持原样，不推送或发布。

## English

The fixed two-column grid hid the ninth action below the default viewport. The Windows device center now opens wider, measures descriptions to choose two or three columns, preserves action routing/order, and compacts spacing without reducing font size. A post-resize layout refresh corrects stale FlowLayout scroll extents. Four-action pages retain two columns.

Release build and scoped layout checks passed: 56 captures across seven page states, followed by 24 captures across three states using the default device names after the final sidebar adjustment. Tests ran at the current 168 DPI with geometric scaling and constrained windows, not on multiple physical-DPI monitors. The broader service-status minimum-row assertion fails identically in the previous installed build and remains recorded. Tool-launched status-once was blocked by the existing profile protection. An intermediate build was visually checked after desktop startup with healthy USB, Wi-Fi and Bluetooth diagnostics. The final five runtime files were installed and hash-verified, but the user stopped desktop automation before the final restart check. Firmware, Mac, settings and user data are unchanged. The user subsequently authorized a local commit of this UI change; other worktree changes are excluded, with no push or publication.
