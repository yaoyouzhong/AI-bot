# DeepSeek 网页账户的设备显示交接（2026-11-02）

- 范围：Windows 桥接将 DeepSeek 设备数据切换为同一次网页账户响应中的余额和累计消费；API Key 和 API 余额仍独立保存。ESP8266 与 TAB5 使用既有协议字段，无固件修改或版本要求变化。
- Windows：`MigratedDomesticBridge.DeepSeekWebDisplay` 只投影网页数据；没有网页余额时保持未知，消费缺失、观测时间不一致或超过 24 小时时不显示消费。网页刷新失败单独标记缓存，不把 API 账户的余额或消费与网页账户混用。
- 验证分级：Windows 已本地部署到原启动目录。新进程真实网页刷新出现 DeepSeek `usage-saved`、GLM `spend-saved`；两项 `usedCost` 非空且 `stale=false`。用户明确确认小屏和 TAB5 的四项“已使用／USED”均有金额。自动化状态核对与用户实体屏确认分别记录，不把离线样例当作账户金额。
- Mac：Windows 这次工作当时未修改 Mac 源码。后续 Mac 已独立实现并验收部分实际链路，详情及剩余项见 [Mac 交接记录](MAC-ACCOUNT-SPEND-HANDOFF-20261102.md)；Windows 结果不能代表 Mac 验收。
- 以下记录对应当时的额度修复部署；源码随后与当日语音改动一起按用户要求本地提交并合并主干，见 [当日整合记录](WINDOWS-DAILY-INTEGRATION-20261102.md)，不推送或发布。额度构建含 DeepSeek、GLM 和自动退出修复；主程序来自 `windows-app/AIBotBridge/bin/Release/net8.0-windows10.0.19041.0`，部署时 PID 10124、健康守护 48348。DLL SHA-256 为 `7EE81EAF5214E8A8A4B52A1F37A1588C8D0B9EBBC61A6913D411730546803758`。只替换 DLL/PDB，先核对依赖契约一致并备份原文件；备份和无金额证据见 `artifacts/quotas-final-20261102/rollback`、`deployment-result.json`。
- 部署复核：小屏 USB 在线，TAB5 USB/Wi-Fi 在线，`pairMatchesDisk=True`、`test=False`。最终运行状态为 auto、轮播开启、原六页顺序及 15 秒间隔。Release 构建、DeepSeek、GLM、国产额度及 profile guard 回归通过。独立 `--status-once` 在 Codex 命令环境仍被正常的目录保护拒绝；通过真实桌面进程及其只读 `/status` 验证了实际运行数据，没有放宽账户目录检查。
- 账号页文字控件的离屏预览已检查；一次尝试完整窗口离屏渲染时前台焦点保护检查未通过，立即停止并撤回该测试方式。完整窗口实际渲染尚待以不影响用户前台的方式核验。

## 自动化失败原因与修正

- Codex 启动的 CLI、Shell.Application 和原生直接启动均可能继承 MSIX 文件重定向；KnownFolder 显示正常路径也不代表文件句柄指向真实桌面资料。失败后应先检查启动上下文，不重复同一路径，也不弱化 AppProfileGuard。
- 原 `--exit` 位于账户目录检查之后，虽然只发送当前用户会话的退出信号，却被不相关的目录检查拦住。本次将该无账户文件访问的命令提前处理；已实测退出码 0、临时候选正常结束，普通启动的目录保护保持不变。
- 本次采用可核验的分段操作：候选独立构建和测试、旧目录未运行检查、原文件备份、两文件更新与哈希核对、正常桌面启动、真实数据刷新、设备连接及用户目视验收。此前自动审批拒绝的是自制部署辅助程序，不能据此推断一切部署方式均不可行，也不能规避拒绝后运行等价自制程序。
- Windows 资源管理器直接双击 EXE 已验证可从正常桌面上下文启动；通用直接启动及本次快捷方式步骤没有作为成功依据。Computer Use 操作由用户指定的 Terra/high 执行。优先固定采用已验证入口，启动后以进程路径、payload 哈希、HTTP 状态和新进程真实刷新判断成功。
