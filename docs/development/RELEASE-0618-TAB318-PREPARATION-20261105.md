# 0.6.18 / TAB5 .318 发布准备记录

后续范围变更：用户要求 Claude Code 接入原终端／VS Code 会话，并沿用原界面的权限确认。下文是 `778e433` 原范围的打包证据，该桥接候选当前不推进发布；需完成 `CLAUDE-CODE-NATIVE-ALIGNMENT-20261105.md` 所述修正后冻结新桥接候选，不套用旧后端验收。TAB5 .318 原包和独立验收继续保留。

冻结构建源码：778e4332ac99a609439690a5892db4f4f1881e98；已按用户授权推送至 origin/codex/windows-dynamic-microphone-20261105 并核对远端。后续仅记录验收的文档提交不改变该构建身份。未合并主干、未创建正式标签、未公开发布。

Windows 0.6.18 已完成隔离原生构建、原生回归、实际安装器自测及环境检测、最终 ZIP 的 111 项文件摘要／全量离屏回归和包内刷机工具自测。最终发布 DLL SHA-256 为 911cd26e394c2f92e4c274e70d13afa880963661af8ec8b411448d2d31026ad4，其正常桌面上下文只读启动返回 0；设置、配对与原常驻文件摘要保持，未安装或替换当前桥接。安装器、登录 helper 和迁移输入相对已发布 .617 未变，沿用已登记的安装保留证据。

| Windows 原包 | 字节 | SHA-256 |
| --- | ---: | --- |
| AI-bot-bridge-v0.6.18-windows-x64-setup.exe | 31702628 | e71a7bd99e6dee37426915f69383b23e1a04b7e78930f71e9c01ece27cf16a7b |
| AI-bot-bridge-v0.6.18-windows-x64.zip | 33183065 | f1219eb453a26bff641151c7019d5c42fea9de0169852777bf44080831afbca5 |
| AI-bot-bridge-v0.6.18-source.zip | 160241756 | 63e1d922de0b0c141c451b4df2a4fa8fa0021f254fcbde893d8eba452f16d035 |

TAB5 0.2.318-ui 首刷、升级和完整源码已校验并冻结，候选 ID 为 de25d0860433a470964bcbe292d56b8737cb9f7d9a99cf01d35ae39b083a246c。应用继续使用已验收的 ad7d04c5585f222951aa7ac13deb01e8664a2069679d32547fa9f058563e2be，没有重新构建或刷机；应用实际增量源码身份为 6cc8528608c7e7ceb88fe59aadc171e07a24b33f。发布冻结提交另行汇总完整工程及元数据，验收复用明确绑定原应用。正式 sidecar 仅更新为本次分类说明，实际渲染及生产加载校验通过。

Mac 仍需从同一冻结 SHA 生成正式 0.6.18 ZIP，核验原包身份、签名和后台启动并回传摘要／记录；已有 .617 开发候选功能实测按未变更范围复用，不能改名替代新包。用户已取得一段可转发的交接指令；本机线程工具未识别 Mac 会话，没有将本地 Windows 会话当成 Mac 或虚称已发送通知。详见 [Mac 交接](MAC-0618-RELEASE-HANDOFF-20261105.md)。

Windows 作为唯一发布负责人汇合原包和验收，之后按正式发布授权统一合并主干、打标签、上传和公开。Mac 不自行发布。当前计划保留两端范围，不因 Mac 未到包而静默改成单 Windows 发布。ESP8266 和图库不重建、不升号。

打包阶段的路径限制、官方依赖下载超时、PowerShell 模块环境、启动资料保护拒绝及旧合成测试目录复用失败均保留原日志；最终通过使用短路径、固定摘要的既有正式依赖、正常桌面只读启动及全新测试目录，没有修改产品代码、断言或跳过测试。TightVNC 仍执行原脚本的固定哈希与官方签名核验。

准备产物与日志位于 rtifacts/release-0618-tab318-20261105/，包含 Windows 原包／来源 stamp、TAB5 原包与已绑定验收、统一晋级计划和两端复用范围。临时隔离克隆仅保留作当前发布构建缓存，待跨端原包汇合后收尾；它不属于交付源码或 Git 源码历史。本地未跟踪图片未进入隔离源码与发布包。

## English

Windows 0.6.18 and accepted TAB5 .318 packages are prepared from the frozen source above. Windows native and exact-archive regressions, installer self-tests, manifest checks, bundled flash-tool checks and read-only desktop-profile startup pass; the running bridge and protected configuration are unchanged. Installer/migration evidence is reused only for unchanged inputs. TAB5 retains its physically accepted application bytes, with separately checked public notes and complete corresponding source. The final Mac 0.6.18 archive remains pending from the same source; its prior functional evidence can be reused without relabeling the .617 development ZIP. Windows remains the single publisher. No main merge, release tag or public publication has occurred.
