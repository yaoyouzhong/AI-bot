# AI-bot v0.6.8 候选 / Candidate

## 范围 / Scope

Windows 桥接：串口断开恢复、故障来源与建议、安装器／固件下载续传、设备中心窗口交互统一，以及新旧构建物命名兼容。当前公开版仍为 0.6.7；本文件和本地版本号不代表已经发布。Mac 元数据同步版本线，但 Windows 的窗口和下载功能不属于 Mac 功能声明。

Windows bridge: serial-disconnect recovery, attributed service faults and advice, resumable installer/firmware downloads, consistent modeless Device Center panels, and artifact naming compatibility. The public release remains 0.6.7 until separately published. Mac metadata shares the bridge version line; Windows UI/download changes are not claimed as Mac features.

## 更新与兼容 / Updates and compatibility

安装器为 `AI-bot-bridge-v0.6.8-windows-x64-setup.exe`，发布准备需生成旧客户端识别的同字节兼容附件。保留原配对、额度历史、图库与展示设置。下载校验成功后仍由用户确认安装或设备升级。ESP8266 0.5.3、TAB5 0.2.205-ui 及现有图库不因桥接升级而重新编号。

The canonical Windows installer keeps a byte-identical legacy alias for existing clients. Preserve pairing, quota history, galleries and display preferences. Installation/flashing remains explicit after download validation. Existing ESP8266, TAB5 and gallery releases retain independent versions.

## 验证边界 / Validation boundaries

记录源码回归、隔离原生窗口验证、安装器自检、完整包解压校验和真实运行验收。测试成功不代表已部署。0.6.7 串口热修复已在本机恢复运行并由用户确认 TAB5 报警消失；0.6.8 已在固定 Release 目录完成安装与真实运行验收，安装器和部署文件哈希已核对；未公开发布。TAB5 209 的写入速度与重启电量已获用户确认；210 已完成独立构建、刷入及日程／内存／写入／重启电量验收，结果见 [210 收尾记录](development/TAB5-210-ACCEPTANCE-20261017.md)，不包含在 Windows 安装器中。

Record source regressions, isolated native UI verification, installer self-tests, extracted-package checks and real runtime acceptance separately. The previous serial hotfix has user-confirmed TAB5 alarm recovery; 0.6.8 installation and real runtime acceptance are complete at the fixed Release path, with installer and deployed-file hashes verified; it remains unpublished. TAB5 209 writing speed and reboot battery display are user-confirmed. TAB5 210 build, deployment, agenda layout, memory, writing speed and reboot battery display passed local acceptance; see its separate record. It is not installed by the Windows package.
