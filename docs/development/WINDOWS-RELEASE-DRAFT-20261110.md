# 0.6.18 / TAB5 0.2.318-ui 双语发布说明草稿

本文件供 Windows 发布负责人审校，并非公开说明。目标版本、范围、权限、候选素材和实际日期须在发布前确认；与当前 `Unreleased / 未发布` 及目标版本既有条目合并后，以最终 CHANGELOG 和镜像 sidecar 为准。涉及提示音的条目仅在素材具备公开再分发依据且选定包实际包含它时保留。未知验收项不写成已通过。

## 桥接 / Desktop bridge

### 中文草稿

#### 对话与项目

- 在 TAB5 对话页使用图标切换 Codex 与 Claude；目录短暂不可用时保留最近可用的入口和会话，恢复后继续更新。发送仍会核对真实目标和权限。
- 将同一 Git 仓库主目录及经核验的 worktree 归为一个 Codex 项目，不同的同名项目保留区分；新固件支持在同一项目内滚动查看更多会话，旧固件继续分页。
- 已观察到的 Codex 回复在电脑端读过后，下次有效同步即可清除对应未读；新轮次和其他会话继续受保护。

#### Claude 原生会话

- 在支持的 Windows 和 Mac 原生终端中按真实进程与会话识别 Claude Code，不再依赖固定的终端版本名单；发送前仍检查目标、输入区和权限。
- 支持 Mac Terminal 已有 Claude 会话的文字、图片、追加和指定轮次中断，保留原会话公开回复与送达核对；发送结果不确定时保留草稿，不自动重发。
- 改善原生终端状态恢复与回复刷新；Mac 保留已有草稿并在无法确认输入目标时阻止发送。

#### 语音与升级

- 两端豆包自动检测若已指向所选大疆物理麦克风，则直接复用；临时切换时继续核对实际端点、恢复原选择并尊重手动改选。Windows 语音提示按窗口宽度换行。
- Windows 允许麦克风权限后，等待授权窗口过渡完成并继续同一次录音；安装新版桥接使用一次管理员授权，常驻桥接可直接发起 TAB5 升级。
- 保留现有配对和草稿；设备固件、ESP8266 和图库按各自版本单独更新。

### English draft

#### Dialogue and projects

- Switch between Codex and Claude with icons on TAB5. Briefly unavailable catalogs retain the last usable entries and sessions; sending still checks the actual target and permissions.
- Group the main checkout and verified worktrees of one Git repository while distinguishing different projects with the same name. New firmware scrolls through more sessions in one project; older firmware continues to show pages.
- Clear a previously observed Codex unread indicator on the next valid sync after the reply is read on the computer. New turns and other sessions remain protected.

#### Native Claude sessions

- Discover supported Windows and Mac Claude Code terminals by live process and session identity without a fixed terminal version list; verify the target, composer and permissions before sending.
- Support text, images, follow-ups and exact-turn interruption for existing Mac Terminal Claude sessions, preserving public replies and delivery checks. Keep drafts and do not automatically resend uncertain submissions.
- Improve native terminal readiness and reply refresh. On Mac, preserve existing drafts and block input when the target cannot be confirmed.

#### Voice and upgrades

- Reuse Doubao automatic detection when it already resolves to the selected DJI physical microphone on either desktop platform. Temporary selection still validates the endpoint, restores the previous choice and respects manual changes; Windows voice guidance wraps to the window.
- On Windows, continue the same recording after microphone approval and the permission-window transition. Install the new bridge with one administrator approval; the resident bridge can initiate TAB5 upgrades directly.
- Keep existing pairing and drafts. Device firmware, ESP8266 and galleries follow their own version lines.

## TAB5 固件 / TAB5 firmware

### 中文草稿

#### 对话与会话

- Codex 和 Claude 使用图标入口；按可用空间排列，入口较多时可横向滚动，未读数字为 14 px。厂商目录短暂缺失时保留当前入口和会话。
- 将可验证属于同一项目的会话分页合并为一个入口，通过滚动列表查看更早的会话；不同项目保持独立。
- 当前项目的未读会话优先显示；有新回复时等触控操作结束再带入视野，不切换当前项目或选中会话。
- 保留 Codex 原完成提示，并使用匹配音色的 Claude 完成提示。

#### 触控与显示

- 切离对话页时关闭项目菜单；为长项目名保留箭头空间。相机与录音按钮采用等高双圆，发送、追加和取消使用胶囊按钮。
- 修复录音结束后按下态残留、长按清空后的文字残影，并保留取消后草稿与照片恢复保护。
- 保持独立版本和现有配对、展示设置；现有设备使用升级包，新设备使用首刷包。

### English draft

#### Dialogue and sessions

- Use provider icons for Codex and Claude, arrange them to fit the available width and allow horizontal scrolling when needed. Unread numbers use 14 px; briefly missing catalogs retain the current entries and session.
- Merge verified pages belonging to one project into one entry and scroll to older sessions; keep different projects separate.
- Show unread sessions first in the current project. Reveal a new reply after touch interaction ends without changing the current project or selected session.
- Retain the existing Codex completion prompt and use a matching voice for the Claude completion prompt.

#### Touch and display

- Close the project menu when leaving dialogue and reserve arrow space for long project names. Use aligned circular camera and microphone controls and capsule send, append and cancel buttons.
- Fix the lingering microphone pressed state and stale text after long-press clear, while retaining cancelled-draft and photo recovery protections.
- Keep the independent firmware version, existing pairing and display settings. Existing devices use the upgrade package; new devices use the first-install package.
