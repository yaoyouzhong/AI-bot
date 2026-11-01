# Windows 本地主干整合记录（2026-11-01）

## 提交与范围

- `bb198f0`：Windows W29/W31 实现、自测、TAB5 补丁资料、Mac 交接和跨平台开发发布流程。
- `5cffd17`：合入远端 `f4a509d`（PR #20，Mac USB OTA），保留两端更新日志。
- `bdbce43`：合入原本地 main `12e7732`，保留其七个独有提交及图库、开发记录；main 已快进到整合结果。
- 已确认原本地 main、最新远端 main 和 PR #15 合并提交 `572a7ee` 均为整合结果祖先。相对远端，CI/CD 工作流未新增修改，Mac 仅有共享版本元数据变化，没有新增 Mac 功能修改。

冲突集中在文档和媒体。保留最新远端的正式版本说明、Mac 导航验收记录、视频网页播放版与原版下载及匹配封面哈希；本地主干的功能代码自动整合。原有常青项目介绍和内嵌播放形式保留，没有回退到旧发布摘要。图库内容与原本地 main 一致。

## 验证

- 整合源码 Release 构建通过：0 警告、0 错误。
- 在普通桌面用户上下文运行源码 `dotnet run --project windows-app/AIBotBridge/AIBotBridge.csproj -- --status-once` 通过；未绕过配置隔离保护。
- `scripts/check_version.py`：桥接 0.6.14、ESP8266 0.5.7、TAB5 0.2.302-ui 元数据通过。
- 从 Program.cs 核对参数后，`--self-test-tab5-quick-console`、`--self-test-tab5-voice-core`、`--self-test-domestic-quota`、`--self-test-codex-tasks`、`--self-test-tab5-rpc`、`--self-test-tab5-remote-ota` 全部通过。
- 公开内容保护自测通过；扫描 2767 个当前文件，0 个发现（有限模式检查，不宣称完整安全审计）。新提交新增行凭据模式检查通过。
- 文档本地链接检查及 `git diff --check` 通过。
- 整合后的完整公共套件通过：`PUBLIC_SELF_TEST_OK`、`PUBLIC_SELF_TEST_DESKTOP_OK`；包括 Wi-Fi 解锁 300 项、PIN 266 项和无线启动模拟 63 项。日志为本地证据目录中的 `merged-public-isolated.log`，不代表真实设备验收。

公共自测重复运行曾因旧固定夹具 `artifacts/public-wifi-unlock` 残留出现“Wi-Fi defaults off”。未删除用户配置或修改断言；最终结果由全新进程工作目录、不可见测试桌面重新运行确认。PowerShell 的 `Set-Location` 不能代替该启动器使用的进程工作目录隔离。

## 保留现场与边界

原 main 的 `docs/development/TAB5-WIFI-PHOTO-20261023.md` 未提交修改、五张 software-photo 测试 JPG 和 `startup-overview.ppm` 全部保留；备份及七项 SHA-256 复核一致，没有混入本次提交。整合没有替换当前运行的桥接程序或更改用户设置。

本地证据目录：`C:\Users\yaououzhong\Work\AI-bot\artifacts\windows-submit-20261101`。任务工作区 artifacts 已复制到其中 `worktree-evidence` 并逐文件核对哈希；构建自测日志另存 `build-test-logs`。这些本地证据不作为公开附件上传。

此次只提交和合并本地 main，不推送、不打标签、不发布、不重新刷机。Mac 构建与 R35 真机验收、长时间闲置、大疆麦克风及缺少账户的额度验证仍按[Mac 交接](MAC-FOLLOWUP-FROM-WINDOWS-20261101.md)和现有清单保留。最终安装包必须按[统一流程](../DESKTOP-DEVELOPMENT-RELEASE.md)单独构建和验收，不能用本次源码构建代替。
