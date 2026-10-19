# macOS TAB5 phase one / 第一阶段本地实现

2026-10-19。**仅本地源码候选；未部署、未操作真实配对、未刷机、未提交、未推送或发布。Mac 编译、原生界面及设备验收尚未完成。** 已发布 Mac 下载包支持范围不变。

**Local source candidate only. No deployment, live pairing, flash, commit, push or publication. Mac compilation, native UI and hardware acceptance remain pending.** Published Mac packages retain their existing support scope.

## 实现范围 / Implementation

- 原生菜单“TAB5…”进入 USB 配对、Wi-Fi 配置和显示设置窗口；保留 ESP8266 原入口与展示偏好。窗口可缩小并滚动，设置编辑器提供八页勾选及上下排序。
- USB 使用 460800、v1 `@AIBOT ` JSON 和固件现有的 2048 字节兼容 RPC 邮箱。与 ESP8266 扫描器共用串口互斥；重连、端口名称变化后先验证已保存设备身份。
- 小屏刷机与 TAB5 窗口操作互斥，刷机暂停/恢复时同时管理两个串口扫描器；退出时取消设置等待并停止新请求。
- 配对身份、端口和 256 位密钥保存在独立 Keychain 条目。先保存密钥再发送配对命令；不确定 ACK 保留原密钥供用户明确重试。无法读取原记录或设备身份不同时拒绝覆盖。
- 现有 HTTP 监听端口（默认 8765）增加 `/tab5/v1/status` 和 `/tab5/v1/rpc`；TAB5 独立 HMAC/AES-GCM 认证，ESP8266 `/status` 继续使用 `X-AIBot-Token`。拒绝 TAB5 浏览器来源请求。
- UDP 18766 响应认证发现，使用连接实际本地 IPv4；回执绑定设备、nonce、地址和 HTTP 端口。密钥不广播、不写日志或普通偏好。
- 日常数据复用 Mac 活动元数据、Claude/Codex 额度、天气、最多 20 个行情、系统指标、音乐文字/进度、公历/星期/农历。保留来源实际值和 stale 标志；国内厂商未知额度不编造。TAB5 额度字段使用 `primary`/`weekly`，Codex 主窗口遵守既有 PLUS 边界。
- 显示设置包含亮度、音量/静音、固定页、轮播、间隔、屏保、提醒和页面启用/排序。命令有效 20 秒，桥接等待 25 秒；写入需设备 revision，匹配加密成功回执后才显示保存。冲突、设备忙、保存失败、畸形回执或超时均不视为成功。USB/Wi-Fi 共用 nonce、session/time 与有界重复回执处理。

English: adds native TAB5 setup/settings, identity-checked USB pairing, authenticated LAN discovery/status/RPC, existing Mac daily data and device-confirmed display settings. Keys stay in Keychain. USB scanners share a lease; uncertain pairing retains its staged key. Display writes require a fresh device revision and matching receipt. ESP8266 preferences and token authentication remain separate.

## 架构及边界 / Architecture and limits

`MacTab5Service` 管理会话、序号、认证、nonce 和设置事务；`MacTab5USB` 串行执行端口 I/O。`MacHTTPRequest` 有界解析头与二进制 body，拒绝重复头、非法长度、Transfer-Encoding 与尾随流水线数据；`HTTPStatusServer` 先分发 TAB5 二进制路由，再保留旧文本接口。

保持现有 v1 协议；AES-256-GCM 包为 `nonce[12] | tag[16] | ciphertext`，32 字符挑战作为 AAD，不使用 CryptoKit 默认 combined 顺序。未修改 Windows 或 TAB5 固件源码。

第一阶段要求设备已安装支持 hello、RPC v1 和显示设置的 AI-bot 固件。保留设备当前连接模式与轮播；需在设备选择自动、USB 或 Wi-Fi。仅蓝牙模式无法使用本候选。Wi-Fi 请求仅证明已认证请求到达，界面不将其称为屏幕已显示；USB 状态 ACK 和设置回执分别核对。

单主机/单 TAB5 配对。迁移同一设备到其他电脑可能替换设备当前密钥，配对窗口明确提示。Wi-Fi 密码在提交时清空输入框，Mac 不持久化密码。蓝牙、封面/桌宠资源同步、图库、电脑控制/解锁、Codex 正文/发送、语音、OTA、首刷恢复及新增厂商采集不在本轮；不通告其能力。音乐文字复用原有可选读取器，未改 Music/Spotify 的 Apple Events 或播放控制行为；未推测节假日/节气等未知数据。

English: retains protocol v1 and device-side transport selection. The compatibility mailbox handles phase-one small requests. Existing firmware is required; BLE-only mode, resource sync, control/unlock, Codex content/send, voice, OTA and first installation/recovery are excluded. One host pairing is supported. Wi-Fi UI reports an authenticated requester, not confirmed rendering. Music uses the existing optional reader.

## 本地验证 / Local validation

| 检查 / Check | 结果 / Result |
| --- | --- |
| Windows 协议参照 Release 构建 | 通过，0 警告/0 错误；没有启动桥接或读取用户配置 |
| Swift 语法解析 | 40 个 Swift 文件无语法错误；仅 tree-sitter，不是类型检查或 SDK 编译 |
| 独立协议向量 | Python AES-GCM 与实际 Windows `Tab5Protocol` 双向互解；HMAC、认证发现一致 |
| 畸形数据 | 实际 Windows 参照拒绝 4 类篡改包、8 类非法显示设置 |
| 固件邮箱 ABI | 编译实际未修改的 `rpc_mailbox.inc`；124/2048/2049/5000/12384 字节五场景通过，核对 ID、偏移拒绝和最终重组字节 |
| 差异检查 | 本轮范围 `git diff --check` 通过；既有行尾转换提示不属于空白错误 |
| Swift XCTest、Mac Release 构建 | **未执行：当前主机无 macOS/Swift 环境** |
| 原生 UI、权限及 USB/Wi-Fi 实机 | **未执行：需 Mac，设备验收/部署另行授权** |

证据：`artifacts/mac-tab5-20261019/checks/results.json`，参照 DLL 位于同目录上级 `windows-reference/`。Python 语法依赖与 C 编译器只存入忽略的本次 artifacts；未全局安装、未修改系统设置。向量密钥是公开合成测试数据，不是用户凭据。

新增 19 个 XCTest 方法，覆盖加密、日常数据映射、HTTP 分包/鉴权隔离及二进制设置回执主链路、设置读写与冲突、会话/时效/重放/重复回执、忙碌/取消、配对保存失败、发现、邮箱和串口互斥。**已编写，未在 Mac 执行，不计为通过。**

English: Windows reference and firmware ABI checks passed; 19 new XCTest methods are prepared but unexecuted. Syntax parsing does not establish Swift compilation. No Mac/device success is claimed.

Windows 本地复核命令（路径对应本机证据）：

```powershell
python scripts/test_macos_tab5_contract.py `
  --reference-dll artifacts/mac-tab5-20261019/windows-reference/AIBotBridge.dll `
  --compiler artifacts/mac-tab5-20261019/tools/tcc/tcc.exe `
  --firmware-main artifacts/release-068-20261016/tab5-saver-source/firmware/main `
  --python-deps artifacts/mac-tab5-20261019/python
```

Mac 构建/测试入口（不部署、不发布）：

```bash
bash scripts/verify_macos_tab5.sh
```

后续先完成 Mac 编译与测试并处理任何失败，再核验窗口可见布局。真实 Keychain/局域网权限拒绝与恢复、USB 配对/拔插/改端口、纯 Wi-Fi 设置回执、本机修改冲突、断连及睡眠唤醒需单独实机验收。本次不得以运行上述脚本为由替换在用程序、重新配对设备或刷机。

Next: run the Mac gate, fix failures and inspect native layouts, then separately authorize physical acceptance for permissions, pairing, reconnect, Wi-Fi-only settings and sleep/wake. Build/test commands do not authorize deployment or live pairing.
