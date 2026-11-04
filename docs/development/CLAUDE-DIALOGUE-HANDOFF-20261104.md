# Claude / Codex 对话对齐交接（2026-11-04）

## 范围与源码身份

- 来源平台：Apple Silicon macOS；接收端：Windows 10/11。独立工作区 `tab5-companion-312-alignment`，保留原工作区未提交修改。
- 基线源码：`0dc59134626e110f25e427bc6d7c4f61e64a8a85`。本地诊断候选构建时以 `artifacts/claude-session-validation/dialogue-final-source-files.json` 的逐文件摘要和独立 TAB5 来源清单绑定；后续本地提交固定同一份运行源码，不重建原包、不声明发布标签。
- 本地授权：实现、后台自动验收、临时切换桥接、TAB5 升级及用户随后要求的本地提交。未执行合并、推送、标签、GitHub 上传或公开发布。Windows 接收端按[本次变更清单](WINDOWS-CLAUDE-DIALOGUE-ALIGNMENT-20261104.md)继续，不重开未修改功能的历史验收。
- 电脑桥接 VERSION 保持 0.6.17；TAB5 独立候选 0.2.313-ui；ESP8266 未修改、未刷机。不能用本地新增能力代表已经发布的 0.6.17 附件。
- 协议 version=1 不变；可选 `claudeTasks`、`aiDialogueVersion=1`、RPC `client=codex|claude` 与旧桥接兼容。缺省 client 仍是 Codex。原始 Claude UUID、路径、用户提示、推理与工具内容不进入设备状态。

## 对齐结果

| 用户行为 | Mac / Windows 实现 | 状态与证据 | 剩余事项 |
| --- | --- | --- | --- |
| 项目、历史会话与公开回复 | ClaudeCatalog / ClaudeHistory / ClaudeWorkspace 两端独立实现；共用设备布局 | 已完成：原生 Mac、真实 CLI 隔离测试、C# 离线与交叉编译；历史只传助手公开正文 | Windows 原生及实际账户待验证 |
| 草稿、图片、送达与去重 | Mac 私密存储；Windows 每设备 DPAPI 日志；客户端/设备/会话分别绑定 | 已完成：Mac 草稿隔离与真机手动键盘发送；真实 CLI PNG、追加、取消、中断通过 | Windows 原生核对新增客户端路由与 DPAPI 草稿/回执；原相机功能复用已有结果 |
| 语音 | 两端共用既有语音链路并固定客户端 | 已完成：真机录音/结束到草稿、显式发送的路径采用合成识别结果验证；原生相关回归通过 | Windows 只补目标归属与切换保护；识别后端及准确性未修改，不新增重验任务 |
| 总览直达明细与返回 | 固件统一精确客户端/会话导航，未读定位精确轮次；底部“对话”保留项目/历史列表 | 已完成：Claude 与 Codex 原会话明细、返回总览和底部列表均经最终镜像真机检查通过 | 不以最近条目替代指定会话 |
| 完成与已读状态 | 受控 CLI 精确结果投影到同一总览会话；固件回执携带渲染时 client | 已完成：Claude 发送后总览已完成，实际阅读后未读清除、总览待命；新外部提示/其他等待会话不会被旧结果覆盖 | 非受控外部会话仍不从 Stop/静默猜成功 |
| 目录暂缺 | 两端声明统一对话能力；现代桥接不回退旧阅读页 | 已完成：实际 TAB5 目录停用后显示禁用的“对话待同步”，点击留在总览 | 旧桥接仍保留旧只读兼容页 |
| Claude 待命形象 | 原始已存在素材固定帧，3.6 秒约 1.1% 呼吸；总览及屏保同一表现 | 已完成：实际 LVGL 总览/屏保、呼吸端点与循环测试；TAB5 待命截图与连续样本像素变化已核验 | Windows 复用同镜像结果，不重验共用触摸与动画 |
| 工具审批 | 两端原生手工打开确认窗口；拒绝默认，允许本次，原参数只读 | 已完成：Mac 合成参数离屏检查与审批 wire 测试；Windows 编译通过 | 用户主动打开的真实审批及 Windows 原生外观待验证 |

外部仍运行的 Claude 会话只读，不能与其并发写入或声称能够中断；只对本桥接持有的 CLI 实例提供追加与中断。保留模型、设置与审批策略，不自动批准工具操作。这是本次实现的能力边界，不扩大为其他功能的重新开发或验收任务；Windows 原生受影响路径仍须如实记录。

## 构建物

| 组件 | 文件 | 摘要与验证 |
| --- | --- | --- |
| Mac 本地开发签名候选 | `artifacts/claude-session-validation/mac-dialogue-details-final/AIBotBridge.app` | 主程序 SHA-256 `e3cf1d4b25b23902af7125fa60ac6acdd9b690d51bd65a1d33458464359d34a6`；本地 AI-bot 开发证书，codesign strict 通过，未经公证 |
| Mac 本地验收包 | `artifacts/claude-session-validation/local-dialogue-package.zip` | 附 LICENSE、THIRD_PARTY_NOTICES.md、主程序摘要及包摘要文件；非公开发布包 |
| TAB5 | `artifacts/firmware/tab5/latest/aibot_tab5.bin`，6,923,744 字节 | SHA-256 `e0c0981beae8525e74fc1aeb3880904aeb95df909d63f5b9a39162ec72deb587`；精确 ELF `e5f52c3aff72315dec130342b4a16705b3205ba60da3cb41bc367ba4ffeb8ef7` |
| Windows | 本地 Release 交叉构建目录 | 0 warning / 0 error；没有 Windows 原生可验收发布包 |

TAB5 包经准备脚本生成、保留同级 versions 历史，使用实际 sidecar 离屏检查分类与列表。升级结果 `tab5-details-idle-upgrade-report.json` 确认精确 ELF、VALID 分区、三个实时 UI 样本及 settingsPreserved=true。未将旧镜像的验收结果套用于新镜像。

## 验证与证据

证据统一位于 `artifacts/claude-session-validation/`，均为本地合成测试，无真实凭据或对话正文：

- `mac-dialogue-capability-tests.log`：原生 Mac 462 tests，1 条环境门控跳过，0 failures；29,068 条隔离 parity 检查通过。门控的真实 CLI 路径另外在 `mac-overview-native-cli-retry.log` 通过文字、PNG、精确 resume、排队追加及中断，7 次 loopback API 请求。首次并发构建时曾因 SwiftPM 锁超时，已在构建结束后单独重跑通过。
- `mac-dialogue-details-final-app-build.log`：原生 Release app 构建与本地签名核验通过；`windows-dialogue-capability-build.log`：Windows 交叉编译通过；`windows-overview-portable.log`：身份、公开阅读、历史、目录和自动化 C# 离线回归通过。
- `dialogue-details-idle-render/`：实际 LVGL 总览/屏保待命、明细标题、旧桥接兼容、现代目录暂缺等预览。工作区定向测试检查普通与未读入口、精确旧会话、忙时导航、返回及已读时机。
- `details-idle-regression/`：Codex 萌宠、语音起止、照片切换、相机就绪、恢复与拒绝场景通过；`claude-idle-companion.log`：全部 378 帧解码/边界、完成保持与呼吸端点/循环通过。
- `tab5-dialogue-details-final-live/`：最终镜像实际 TAB5 USB 截图。02=Claude 从总览直达 A 明细，03=返回总览，04=底部进入项目列表，06=受控完成，07=精确未读轮次，08/09=正确 client 回执后待命，10/11=现代目录暂缺不进入旧页，13–16=Codex 总览直达明细、返回及单客户端项目列表。00/01 的操作期间已注入 UserPromptSubmit，属于工作状态，不能作为待命证据。
- 前一候选 `tab5-overview-consistency-live/12-claude-voice-finished.png` 和 `13-voice-text-explicit-send.png` 验证真机语音路径与显式发送；其语音实现随后未修改，识别器是合成测试回调。该候选暴露的 Claude read-ack 路由错误已在最终镜像修复并真机复验；旧错误截图不记为通过。

最终自动化退出、模型偏好恢复与普通后台桥接恢复结果登记于本记录末尾。Windows 原生执行仓库要求的 dotnet build、--status-once 及本次受影响路径验证；跨编译与 Mac 结果不能替代。未修改的语音识别、相机、账户与设备场景复用已有结果，不重新要求用户验收。无公开发布授权，未生成发布标签或更新线上下载入口。


## 本地收尾

最终 USB 自动化显式 stop 返回 settingsPreserved=true；总览模型的全部 11 个复选框与操作前截图逐一比较一致。没有运行 ESP8266 的恢复自动/轮播命令。临时 CLI HOME 和合成 API 已随测试程序退出清理。普通桥接已用最终候选 --background 恢复并验证监听与 USB 打开，未请求电脑前台窗口。用户随后要求本地提交源码，依然不推送或发布。

最终状态与构建物/证据摘要见 `artifacts/claude-session-validation/DIALOGUE-WORKSPACE-ACCEPTANCE.json`；完整变更补丁保存在该目录的 `combined-dialogue-final-source.patch`，基于本记录的基线提交，使用独立 Git 索引核验可应用，未修改真实索引。
