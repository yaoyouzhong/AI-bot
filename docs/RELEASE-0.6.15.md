# Mac 0.6.15 发布准备 / Mac release preparation

**仅 Mac，尚未公开发布。** Windows 保持已发布 0.6.14；ESP8266、TAB5 和图库沿用原正式版本。本次不要求重新刷写设备。

## 授权与默认设置

- 将桥接凭据迁入一个钥匙串项目，正常启动读取一次并复用；拒绝后不由后台轮询反复请求，不生成替代密钥覆盖原资料。
- 默认开启可用的 Codex 会话、直达、发送、电脑控制和音乐控制；保留用户明确关闭的设置。录音和发送仍由用户操作发起，系统权限、账户和解锁密码仍需具备对应条件。
- 按当前签名构建检查辅助功能引导，不依赖 macOS 一定显示系统提示。正常打开应用时展示应用自己的说明；后台启动延后到用户点击菜单栏图标或重新打开应用时展示，右键菜单保留“完成 Codex 直达授权…”入口。同一构建不自动重复，保留明确关闭的选择。系统页面可能叫“辅助功能”或“设备控制与数据访问”。
- 首次统一入口授权等待延长到十分钟，避免用户处理系统提示时提前超时；不会自动重复提交请求。

## 升级与资料迁移

- 正常退出原桥接，将已验收的新应用安装到“应用程序”目录。备份使用 ZIP 或独立目录，不把改名的旧 `.app` 留在“应用程序”中。
- 已有分散钥匙串资料先完整迁移，原项目保留；若旧项目尚未授权，系统可能仍需分别批准，程序先说明并由用户决定是否继续，不能承诺任意旧版本首次迁移都只需一个提示。
- 迁移后的日常读取使用统一项目。选择“始终允许”后，同一版本重启应复用授权；签名改变、系统权限或钥匙串状态变化仍可能需要重新授权。
- 后续凭据修改以新项目为准；旧版无法读取这些修改，降级前应核对资料兼容性，不直接把迁移前记录当作最新资料。
- 本次公开候选沿用 ad-hoc 临时签名，未经公证；本机证书签名候选的结果不能代替实际公开包验收。

## 验证与发布门槛

- 当前实际包从干净提交 `8f4a5ed` 构建，424 项 macOS 测试、29,059 项隔离检查、Release 构建、包完整性和实际明暗界面检查通过；38 项固定设置、5 份配置、29 份加密资料与 3 份资源保持。
- 用户确认统一项目一次授权、应用引导可见并能打开系统设置；重新添加当前应用的辅助功能条目后，同包正常重启无新提示、TAB5 定位成功，新进程后台记录定位 200 与实时 USB 心跳。详情见[实际包验收](development/MAC-0615-PACKAGE-ACCEPTANCE-20261102.md)。
- 用户确认真实 TAB5 音乐播放／暂停正常，没有新的自动化权限提示；本次未登记播放器名称，不外推所有播放器或首次自动化授权弹窗。
- 实际升级需要用户重新关联旧的辅助功能条目，不承诺临时签名升级免授权。公开 0.6.14 首次迁移、全新用户与不同 macOS 尚未验收，Intel Mac 不在本次范围。
- 只有确认实际发布文件与验收文件相同后才能公开。公开前不把下载索引改为 0.6.15，不更新未变更平台的附件。

## English

**Mac-only preparation; not published.** Windows retains 0.6.14; device firmware and galleries retain their existing releases.

- Consolidate bridge credentials into one Keychain item, reuse a single process snapshot and stop background retries after denial. Preserve existing values and encryption keys.
- Enable available Codex, desktop, computer and music features by default while respecting explicit opt-outs. Recording and submission remain user-initiated; system permissions and required accounts/passwords remain prerequisites.
- Provide an app-owned accessibility guide once per untrusted build, with a persistent menu entry and effective permission state. Background launches defer foreground presentation until the user opens the app. Preserve explicit opt-outs and allow ten minutes for initial consolidated-item authorization.
- Legacy items may still require individual system approval during first migration. After migration, use the consolidated item. Retained legacy records are pre-migration rollback data; subsequent credential changes require downgrade compatibility checks.
- The public candidate remains ad-hoc signed and unnotarized. Local certificate-signed acceptance is not acceptance of the public installer.
- The exact package from clean commit `8f4a5ed` passes 424 tests, 29,059 isolated checks, the Release build, native UI previews and preservation of 38 settings, 5 configuration files, 29 encrypted files and 3 resources. The user confirmed one consolidated-item prompt, visible app-owned guidance, normal restart without new prompts and successful TAB5 navigation after re-adding the current app's accessibility entry. The restarted process records navigation 200 and fresh USB heartbeats.
- The user confirms real TAB5 play/pause with no new Automation prompt; the player was not recorded, so this does not establish every player's behavior or first-time Automation approval.
- Ad-hoc upgrades are not authorization-free. Public legacy-first migration, fresh-user environments and other macOS versions remain untested. See the linked package record for exact hashes and remaining gates.
