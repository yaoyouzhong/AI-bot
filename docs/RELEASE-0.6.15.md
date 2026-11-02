# Mac 0.6.15 发布准备 / Mac release preparation

**仅 Mac，尚未公开发布。** Windows 保持已发布 0.6.14；ESP8266、TAB5 和图库沿用原正式版本。本次不要求重新刷写设备。

## 授权与默认设置

- 将桥接凭据迁入一个钥匙串项目，正常启动读取一次并复用；拒绝后不由后台轮询反复请求，不生成替代密钥覆盖原资料。
- 默认开启可用的 Codex 会话、直达、发送、电脑控制和音乐控制；保留用户明确关闭的设置。录音和发送仍由用户操作发起，系统权限、账户和解锁密码仍需具备对应条件。
- 启动时按当前签名构建检查辅助功能引导：新构建权限未生效时请求一次，同一构建不重复请求；保留明确关闭的选择。设置页显示实际授权状态并提供系统设置入口。部分 macOS 页面名称为“设备控制与数据访问”。
- 首次统一入口授权等待延长到十分钟，避免用户处理系统提示时提前超时；不会自动重复提交请求。

## 升级与资料迁移

- 正常退出原桥接，将已验收的新应用安装到“应用程序”目录。备份使用 ZIP 或独立目录，不把改名的旧 `.app` 留在“应用程序”中。
- 已有分散钥匙串资料先完整迁移，原项目保留；若旧项目尚未授权，系统可能仍需分别批准，程序先说明并由用户决定是否继续，不能承诺任意旧版本首次迁移都只需一个提示。
- 迁移后的日常读取使用统一项目。选择“始终允许”后，同一版本重启应复用授权；签名改变、系统权限或钥匙串状态变化仍可能需要重新授权。
- 后续凭据修改以新项目为准；旧版无法读取这些修改，降级前应核对资料兼容性，不直接把迁移前记录当作最新资料。
- 本次公开候选沿用 ad-hoc 临时签名，未经公证；本机证书签名候选的结果不能代替实际公开包验收。

## 验证与发布门槛

- 本地开发候选已通过 423 项 macOS 测试、29,059 项隔离检查、Release 构建及实际明暗界面检查；已验证原值迁移、38 项固定设置与 29 份加密资料保留。
- 用户确认本机统一项目一次授权、正常重启无再次提示、TAB5 USB 心跳及 Codex 直达定位。详情见[统一入口记录](development/MAC-KEYCHAIN-VAULT-20261102.md)。
- 0.6.15 正式包的源码 SHA、摘要、安装与正常重启、从公开 0.6.14 的实际升级路径，以及必要的音乐／设备操作按最终包单独记录。全新用户与不同 macOS 尚未验收；Intel Mac 不在本次范围。
- 只有确认实际发布文件与验收文件相同后才能公开。公开前不把下载索引改为 0.6.15，不更新未变更平台的附件。

## English

**Mac-only preparation; not published.** Windows retains 0.6.14; device firmware and galleries retain their existing releases.

- Consolidate bridge credentials into one Keychain item, reuse a single process snapshot and stop background retries after denial. Preserve existing values and encryption keys.
- Enable available Codex, desktop, computer and music features by default while respecting explicit opt-outs. Recording and submission remain user-initiated; system permissions and required accounts/passwords remain prerequisites.
- Guide accessibility authorization once per untrusted build, preserving explicit opt-outs and avoiding repeated requests for the same build; show its effective state and allow ten minutes for the initial consolidated-item authorization.
- Legacy items may still require individual system approval during first migration. After migration, use the consolidated item. Retained legacy records are pre-migration rollback data; subsequent credential changes require downgrade compatibility checks.
- The public candidate remains ad-hoc signed and unnotarized. Local certificate-signed acceptance is not acceptance of the public installer.
- Local development acceptance passed 423 tests, 29,059 isolated checks, native UI checks, legacy-value verification and preservation of 38 fixed preferences/29 encrypted files. The user confirmed one consolidated-item prompt, no prompt after normal restart, fresh TAB5 USB heartbeats and Codex navigation. Record final-package identity, installation, upgrade and remaining hardware checks separately before publication.
