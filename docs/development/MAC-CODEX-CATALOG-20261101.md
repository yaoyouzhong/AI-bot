# Codex 新会话已切换但定位未核实

## 原因与证据

用户报告 Codex 直达已实际切换到最新会话，TAB5 却提示会话尚未核实。后台定位返回 `409 / desktop_foreground_unconfirmed`，导航树已有一个当前会话按钮，但 `unique=0`，未取得可验证的会话地址。

仅在后台读取当前导航元数据，不激活、置顶、切换窗口或读取输入／消息正文。实际选中会话的完整名称在当前 `sqlite/codex.db` 中唯一存在，在旧 `sqlite/codex-dev.db` 中不存在。Mac 标题显示及定位身份校验仍硬编码读取旧目录，因而系统打开会话成功后，核实步骤失败。旧目录中的已有会话仍可能正常，不能把本次归因于所有 AX 权限失效或设备断链。

## 修复边界

- 两处读取统一使用当前 `codex.db`；仅当该文件不存在时，兼容旧版 `codex-dev.db`。
- 当前目录存在但缺目标、锁定、损坏或结构不兼容时，不通过旧目录的过期标题放行；读取仍只读、有时限、不创建目录／数据库。
- 全目录标题唯一性、跨主机同名冲突、精确地址、当前行及输入框的独立校验保持。没有把“系统接受打开请求”改成身份已确认。
- 不修改固件、协议、布局或提示文案；沿用定位成功／未确认的既有交互，本次纠正数据来源和状态判定。TAB5 保持 .301。

## 验证

使用实际导航标题测试的断言适配器运行 10 个方法、6,477 个断言：旧代码在新增场景出现 10 个失败，修复后全部通过；覆盖新旧目录并存、只在新目录的会话、改名、当前目录缺目标／错误结构、远端同名、完整／紧凑标题以及既有旧版目录兼容。此适配执行不冒充 XCTest；本机 CLT 缺 XCTest，已尝试 `swift test`，完整测试交由 CI。

Release 构建、完整签名校验、28,815 项隔离自检、追加／音乐采样回归及钥匙串 helper 自检通过。R48 已在原固定目录后台部署，保留原 helper 与备份；首次后台启动返回系统 -600，重试启动后确认唯一进程及文件哈希一致。用户重新定位原失败会话，确认“定位正常，不再提示未核实”；后台对应定位请求为成功。所有现场资料留在忽略的本地 artifacts，不提交真实会话名称、标识、数据库或消息内容。

## Windows 对齐

Windows `Tab5CodexCatalog.ReadCatalog` 同样硬编码旧目录。该端应将最近列表和身份确认目录一起切到当前文件、仅在缺文件时兼容旧目录，保持确认目录不受最近 512 项截断及项目过滤影响。不能只改设备显示名称而让确认继续读取旧目录；Windows 代码及实机验收仍由该端完成。

## English

The selected navigation row exists uniquely in the current `sqlite/codex.db` but not in the legacy `sqlite/codex-dev.db`. Both Mac title display and navigation identity previously read only the legacy database, so the app could open a new chat while verification returned 409. Both readers now prefer the current file and use legacy only if the current file is absent. A missing target, damaged schema or read failure must not fall back to stale identity. Read-only access, deadlines, full-catalog uniqueness, cross-host collisions and independent composer checks remain.

Ten actual navigation test methods execute 6,477 assertions through a local assertion adapter: the old implementation fails ten new-scenario assertions, the fixed implementation passes. Native XCTest remains unavailable in the installed CLT and must pass CI. Firmware .301, protocol, visual layout and existing messages are unchanged. Windows has the same legacy-only path and needs a corresponding port and separate native acceptance.

R48 Release/signature, 28,815 isolated checks, append/music sampling and keychain-helper checks pass. Deployed at the fixed path with backup and unchanged helper. The first background launch returned OS -600; retry starts the single verified process. The user repeats the failing new-chat locate and confirms success without the unverified warning; the background locate receipt is successful.
