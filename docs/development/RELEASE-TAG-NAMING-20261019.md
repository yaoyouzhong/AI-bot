# 电脑端发布标签统一 / Desktop release tag migration

2026-10-19，按用户授权，将电脑端发布标签从 `bridge-vX.Y.Z` 统一为 `vX.Y.Z`。当前正式发布为 [AI-bot v0.6.10](https://github.com/yaoyouzhong/AI-bot/releases/tag/v0.6.10)。命名修复提交为 `aacde03f0bfc4d523b5c7057481353ca758ee8d8`。

- 共迁移 12 个发布标签：10 个正式版、2 个既有草稿。草稿仍是草稿；保留原发布 ID、发布日期、预发布状态和全部附件。全仓库 295 个 Release 附件的 ID、名称、大小、SHA-256 digest 均与迁移前一致，其中迁移的电脑端记录含 80 个附件。
- 新标签均为带注释标签，12 组新旧标签逐一确认指向相同原提交；不把维护修复提交冒充原二进制源码。旧 `bridge-v` Git 引用仅作为历史别名保留，不再绑定发布记录。
- 同步中英文主页、完整下载中心、安装说明、视频入口、相关历史文档及 22 个 Release 正文中的下载/发布地址；实际已发布附件名称保持不变，包含旧客户端依赖的兼容安装器名。
- `.releaserc.yml`、组件清单及发布工作流统一生成 `vX.Y.Z`。0.5.0 及更早的 `v` 标签保留组合版语义；0.5.1 起只路由电脑端。ESP8266、TAB5 保持各自标签与独立版本。
- 迁移创建标签引发的 12 个重复打包工作流已取消；未重新打包、发布新版本、安装电脑端或写入设备。

## 验证

- 37 项本地回归通过：组件版本/发布范围 6 项、下载目录 13 项、双语发布说明 4 项、打包 12 项、附件命名兼容 2 项。
- [对应提交的 CI](https://github.com/yaoyouzhong/AI-bot/actions/runs/39270042291) 中 Windows、macOS、固件三项全部通过；公开内容检查及 `git diff --check` 通过。
- 129 个公开下载 URL 的 HEAD 均返回 200，长度与发布资产记录一致；逐个核对迁移正式版的公开校验清单与服务器 digest。完整下载中心的在线核验通过，包含 Windows、Mac、ESP8266、TAB5 首刷/升级、标准图库及高清图库分段清单。
- Windows 安装器与 Mac ZIP 的首尾各 1 KiB Range 请求均返回 206，范围和文件头正确。此项是分段抽查，不表示完整下载了 Mac ZIP 或大图库。
- 从原正式 Windows ZIP 提取已发布的 **0.6.10.0** 程序，未重新编译产品。测试宿主调用该程序真实 `UpdateService`，确认 API 返回电脑端 0.6.10、TAB5 0.2.234-ui、ESP8266 0.5.3，并选择新 `v0.6.10` 下载路径。
- 通过该真实更新服务完整下载 28,777,651 字节 Windows 安装器，执行原有 SHA-256、MZ 格式检查和 `VerifyUnchanged`。SHA-256：`de8842efa6bea81b97a8083b533ef312473d827d9422f563b7e3de01ee3042fa`，与原正式包一致。测试未启动安装器，不冒充重新安装或设备验收。

## 地址兼容边界

GitHub 实测不为旧标签的附件路径提供跳转：迁移后的 `/releases/download/bridge-v…/…` 返回 404，新 `/releases/download/v…/…` 正常。维护中的公开链接已修正；外部收藏、复制出去的旧直链需改用新地址。保留旧 Git 引用不能恢复旧附件路由。已安装电脑端通过 GitHub Release API 重新检查更新后获取新地址，原有安装器兼容文件名和校验信息均保留。

本机详细证据保存在主工作目录 `artifacts/release-tag-naming-20261019/`：迁移前后元数据、逐项操作日志、新旧提交对应表、129 项下载检查、真实更新服务报告和测试宿主源码。仅本次创建的导出目录、编译中间目录、解压副本和下载测试缓存已在保存报告后清理；主工作目录既有开发改动保留，命名补丁以定向替换同步。

Desktop releases now use `vX.Y.Z`; all 12 migrated annotations retain their original commits, all release identities/statuses and all 295 repository assets are unchanged. Bilingual public links and future publishing rules are synchronized. Thirty-seven local regressions, all three CI jobs, 129 public download checks and the original published Windows updater's complete installer download/hash validation passed. No installer or device write was performed. GitHub does not redirect old tag-specific asset URLs, so external bookmarks need the new addresses; API-based update discovery and legacy installer aliases remain valid.
