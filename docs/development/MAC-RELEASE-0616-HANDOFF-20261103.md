# Mac 桥接 0.6.16 发布验收交接

状态：等待 Mac 端对实际候选包验收；这份文档不代表验收或公开发布已经完成。

## 候选身份

- Windows 和 Mac 构建使用同一个源码候选提交：`28ff9cf474ec74d93beb8f5041f4e02d8d7d751d`。后续仅交接文档提交可以位于该候选之后，不能将其误写为二进制的构建提交。
- GitHub Actions macOS 构建：[运行 41578192491](https://github.com/yaoyouzhong/AI-bot/actions/runs/41578192491)，[Mac 产物](https://github.com/yaoyouzhong/AI-bot/actions/runs/41578192491/artifacts/13204168064)。这是临时 Actions 产物，需登录 GitHub 下载，并须在过期前保存。
- 产物内 `AI-bot-bridge-v0.6.16-macos-arm64.zip` SHA-256：`8a0d7b72a6a1d6228bd67a0138f931c4e91686f0db27b854203dc2b14b43afb0`。包内 `SOURCE_SHA.txt` 应为上述提交。保留并验收该确切包，勿用新构建覆盖。
- Actions 归档外层 SHA-256：`c1ff87fcdbeacccea3d6fa68449d58e0dadae10b0bdf8b079257e79cb5302c9e`，与上面的内部 Mac 包摘要不同。
- macOS 15 Apple Silicon CI 的 433 个测试和 Release 构建通过，包解压与签名校验通过。签名为 ad-hoc，未公证；不把 CI 通过视为真实安装、升级或设备通过。

## Mac 端执行

1. 检查并保留 Mac 工作区已有改动，获取候选信息；不要覆盖未提交文件。下载上述 Actions 原包并核对外层及内部摘要。
2. 在适当隔离环境及真实用户会话中验证安装、启动、从已发布旧版升级；核对设置、配对、草稿及展示策略保留，并记录实际系统提示。设备或账号不可用时明确标记未验证，不模拟通过。
3. 按 [Mac 对齐记录](MAC-ALIGNMENT-RESULT-20261103.md) 对可用的 ESP8266 与 TAB5 检查连接、重连、关键展示及 DeepSeek / GLM。保持用户原配置；如须用户观察，只在必要时询问，不自动打开或置顶其前台窗口。除非另有具体授权，不刷机。
4. 将验收对象的文件名、SHA-256、源码身份、运行系统、结果和未验证项记录并反馈给发布负责人。Mac 候选为 ad-hoc 签名且未公证，首次替换可能触发系统授权提示；核实实际行为。
5. Windows 端统一管理最终标签和公开发布。请勿在 Mac 端另行打标签、发布或以重新打包的文件替换已验收候选。

ESP8266 0.5.8 已在 Windows 用户授权下刷入并通过应用区独立回读、启动、心跳和亮屏检查；自动轮播、原页面顺序和 15 秒间隔已恢复。TAB5 0.2.303-ui 的真实生日 30/31 天边界和真实 Codex 异常详情仍待条件；其固件来源和独立重建由 Windows 端继续核查。
