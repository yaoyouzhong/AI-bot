# 候选包验收 / Candidate acceptance

本表用于确定候选的验收记录，空项表示未验证。不要把旧程序或旧固件的成功记录填入新候选。

记录：提交 SHA、各 ZIP SHA-256、Windows/Mac 版本、设备型号、测试日期。修改源码后重新生成候选，说明需要复测的范围。

| 项目 | 操作与通过标准 | 结果 |
| --- | --- | --- |
| 成品完整性 | 附件和逐文件哈希一致，许可、对应源码、重建材料齐全 | 待验 |
| Windows 新安装 | 空白用户安装依赖、解压启动、托盘与镜像可用，无私人数据 | 待验 |
| 默认桌宠 | 无导入资源时默认动画出现；导入自选后重启保留选择 | 待验 |
| 真实授权 | 自行授权后额度可读；网络失败保留最近成功值；日志无凭据 | 待验 |
| 升级与回退 | 备份后升级保留设置；退出新版后可恢复旧版与备份 | 待验 |
| Mac | 按 MAC_PACKAGE.md 检查首次启动、权限、菜单、音乐、设备与睡眠唤醒 | 待验 |
| 实体显示 | 每页检查文字、数值、音乐封面、桌宠与屏保；保留无私人信息的照片 | 待验 |
| 双通道 | USB 失联后约 8 秒恢复 LAN 状态和图片；USB 恢复后重新同步 | 待验 |
| 持续运行 | 建议连续 24 小时，覆盖锁屏、睡眠、断网与重连，记录异常 | 待验 |
| 收尾 | 恢复自动模式及原循环页面、顺序、间隔，保留用户设置 | 待验 |

Mac 或实体设备不可用时如实记录，不用模拟截图代替。

## English

Record the exact source commit, package hashes, OS/device versions and date. Verify fresh installation, defaults, real authorization, upgrade/rollback, physical pages, USB-to-LAN resource fallback, recovery and sustained runtime. Restore the user's automatic cycling settings afterwards. Unavailable hardware and interactive Mac checks remain pending; synthetic tests are separate evidence.
