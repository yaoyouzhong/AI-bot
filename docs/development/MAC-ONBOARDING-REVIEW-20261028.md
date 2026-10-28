# Mac Codex 检查提示词

在 Mac 的 AI-bot 项目 Codex 会话中使用下面提示词。本次通过 GitHub 分支 `codex/first-device-onboarding-20261028` 交接，不再需要复制 ZIP 或应用旧补丁。此分支以 `origin/main` 的 `9dd5b99` 为基线整合原本地提交 `12e7732`，保留最新发布记录和下载入口。

---

请检查 AI-bot 的 Mac 首次接入与安装界面文案改动。用户要求备注／提示尽量简短，保留必要操作、备份、替换确认和错误原因。Windows 端已完成对应开发；此次交接只包含 Mac 的四个 Swift 文件，尚未在 macOS 编译或运行。

先读取仓库及 `mac-app/AGENTS.md`，记录当前 Git 分支、HEAD 和未提交修改，确认远端是 `https://github.com/yaoyouzhong/AI-bot.git`。获取检查分支：

```bash
git fetch origin codex/first-device-onboarding-20261028
```

优先在已有干净、合适的检查工作区检出远端分支；若本地同名分支已存在，仅允许快进更新。有其他任务或未提交修改时，保留原工作区，使用独立检查工作区，不强制切换、reset、rebase、覆盖文件或丢弃本地修复。合入远端 `main` 后，也可获取 `main` 并在合适工作区快进更新，但须确认包含本轮改动（实现提交 `2d1c0b8`）。记录最终验收 HEAD，并阅读 `docs/development/FIRST-RUN-20261028.md`。

你此前发现“新增 TAB5 后默认打开 Wi-Fi，仍需手动切到 USB 配对”，以及三类既有编译警告。请保留 Mac 本地针对这些问题的修复／记录，核对本分支是否仍存在该导航问题；检查结论应区分本轮新增问题和既有警告，不能仅凭成功构建省略它们。Windows 的对应路径已核对：验证并添加时完成 USB 配对，添加后返回设备中心；手动打开连接页默认 Wi-Fi，维护入口完成首刷后会切回 USB 配对。

本次改动范围：

- `mac-app/Sources/AIBotBridge/MacTab5Window.swift`：空设备中心与添加型号提示，说明已有 AI-bot 和出厂设备的下一步。
- `mac-app/Sources/AIBotBridge/MacTab5ToolsWindow.swift`：USB 配对提示与出厂设备首装入口名称。
- `mac-app/Sources/AIBotBridge/MacTab5InstallWindow.swift`：精简下载模式、备份、完成与失败提示；无端口时显示具体下一步。
- `mac-app/Sources/AIBotBridge/FirmwareFlashWindow.swift`：精简 ESP8266 固件窗口说明。

Mac 仍是“先添加型号，再连接／配对或首次安装”。Windows 是“验证并添加”。本次没有把 Windows 的 COM 端口身份恢复逻辑移植到 Mac，也没有改协议、刷写过程、配对凭据、备份校验或组件版本。请按实际 Mac 流程核对文案，不能仅因措辞相似就宣称两端逻辑完全一致。

请完成以下检查并保存结果：

1. 确认四个文件实际包含本次改动；核对调用方、窗口入口、按钮文字与后续动作是否一致，尤其是出厂 TAB5 首刷后回到 USB 配对的提示。确认备份恢复不会被误导成刷入 AI-bot 后的配对流程。
2. 在 macOS 运行必需的测试和 Release 构建：

```bash
swift test --package-path mac-app
swift build -c release --package-path mac-app
git diff --check
```

3. 使用现有的原生 UI 隔离验证入口，创建一个尚不存在的输出目录并保存截图与日志：

```bash
mkdir -p artifacts
review_root="$(mktemp -d "$PWD/artifacts/mac-onboarding-review.XXXXXX")"
review_bin_dir="$(swift build -c release --package-path mac-app --show-bin-path)"
"$review_bin_dir/AIBotBridge" --self-test-tab5-ui "$review_root/native-ui" > "$review_root/native-ui.log" 2>&1
```

该入口使用隔离数据；请先确认当前实现仍不启动真实设备服务或访问凭据。检查返回码并实际查看截图，不能只凭生成文件或通过标记判定界面正常。重点看 TAB5 首次安装、USB 配对、ESP8266 固件窗口，以及默认／最小尺寸下的换行、滚动、操作按钮和状态区。

4. 空设备中心和“添加设备”表单也需要原生界面复核。先检查现有隔离验证是否覆盖；没有覆盖时可使用临时隔离夹具补看，不能清空用户设备列表来制造空态。若无法隔离验证，明确列为未验收。不要启动会连接现有设备的常驻桥接来替代隔离核验。
5. 不为短文案添加逐字断言测试。若既有测试因真实行为问题失败，记录触发、文件位置和证据；不要跳过或削弱断言。发现问题先报告，本轮检查不扩展功能或重做界面。

边界：没有备用出厂设备，必须保留当前 TAB5／ESP8266 的配置、配对、展示模式和图库；不刷机、重新配对、恢复备份、真实写设备、修改钥匙串、系统权限或安装设置。无需打包、替换正在使用的应用、提交、推送、标签或发布。窗口核验完成后关闭测试窗口，不反复打开常驻应用。

最终用中文报告：检查分支与 HEAD、改动同步状态、Mac 编译和测试结果、实际查看过的窗口及截图路径、发现的问题、未验收项。构建成功、隔离界面、真实设备与发布必须分别说明；真实首刷／配对／显示仍未验收。
