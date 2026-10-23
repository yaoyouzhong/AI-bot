# 普通权限纯 Wi-Fi 提交链路

## 范围

用户批准先验证普通权限提交，查清官方认证路线；不保存 PIN、不改成账户密码。此前 SYSTEM 解锁程序启动被工具策略拒绝，该路径保持停止，不通过替换管道服务或放宽 `Tab5Unlock.TrustedServer` 绕过。

新增独立 `wifi-submit-test` RPC，原解锁入口及其权限检查不变。设备自动生成公开合成数字 `4826`，没有人工 PIN 输入。测试不会调用认证、登录、UI Automation、键盘注入、凭据读取或 SYSTEM 服务。

普通用户桥接通过现有同用户本地 IPC 的 `wifi-test-start` 显式开始一次 120 秒测试。先检查非 SYSTEM、未提升、交互会话；在既有配对加密状态中发布随机 run。设备仅在 USB 与 BLE 均未连接、Wi-Fi 状态有效时响应；相同 run 只启动一次。

新 RPC 仍经过原设备身份、HMAC、AES-GCM、时戳、会话及 nonce 校验。每个 case 独立随机 32 字节 AES 密钥和 15 秒 offer；内层格式为 `nonce[12] || tag[16] || ciphertext[4]`，AAD 为 offer ASCII。桥接只接受固定公开 fixture，响应始终 `testOnly=true`、`unlocked=false`。成功仅说明合成数据传输及校验。

真机 case：一次正常提交、重放拒绝、密文篡改拒绝、取消后提交拒绝、等待 16 秒后过期拒绝。设备自己检查响应，再汇报 finish；桥接独立核对观察到的各项计数，不能只相信客户端 passed。任一链路变化或请求失败不自动重试。测试结束/120 秒到期清理会话密钥，状态只保留 run 与计数，不保留请求/明文。

协议实现：维护工作树 `windows-app/AIBotBridge/Tab5WifiSubmissionTest.cs`；TAB5 源 `firmware/main/wifi_submission_test.c`。`tools/wifi-submission-test` 通过 `-p:BridgeSource=<维护工作树/windows-app/AIBotBridge>` 编译同一桥接协议实现，在普通权限下验证 fixture、传输限制及失败场景。

## 官方认证接口结论

截至 2026-10-17，公开官方接口中未找到“由 TAB5 经 Wi-Fi 提交现有 Windows Hello PIN，直接解锁本机控制台”的受支持接口。此为所核查文档范围内的结论，不宣称技术上绝对不可能。

| 方案 | 官方能力 | 是否满足现有要求 |
| --- | --- | --- |
| UserConsentVerifier | 请求 Windows 本机验证用户，返回验证结果；方法参数是提示文字，不是 PIN | 可用于应用内身份确认，不是远程 PIN/控制台解锁接口 |
| KeyCredentialManager / RequestSignAsync | 创建应用自己的密钥或请求签名，Windows Hello 负责本机用户确认；应用不获取私钥 | 不能把应用验证成功当作桌面已解锁 |
| Credential Provider | 收集、序列化并提交凭据；实际鉴权由 LSA/认证包完成 | 不会自动拥有验证现有 Hello PIN 的能力；不能承诺只写 provider 即可复用 |
| Windows Hello for Business RDP | 受支持部署中通过证书/智能卡兼容路径完成远程会话认证 | 不是把目标电脑现有 PIN 从任意 TAB5 传入的通用接口，也未证明能解锁原控制台 |
| Trusted signal unlock | Wi-Fi/Bluetooth 可作为额外信任因子，仍需支持的首要身份验证 | Wi-Fi 本身不能替代本机 PIN/生物验证 |
| Companion Device Framework | 历史伴随设备认证框架 | Windows 10 2004 起弃用，不作为新实现路线 |

可支持“继续使用现有 PIN”的应用方案要求用户在电脑上的 Windows Hello 提示里完成确认；这改变了“PIN 输入在 TAB5”的交互位置，不可默认为用户接受。基于独立设备密钥/证书的新认证方案同样改变凭据模型，当前不实施。账户密码方案、重置 PIN、读取 Hello 密钥及包装系统 PIN provider 均未采用。

来源：

- [Windows Hello 应用开发](https://learn.microsoft.com/en-us/windows/apps/develop/security/windows-hello)：应用专属密钥、用户手势与签名边界。
- [UserConsentVerifier.RequestVerificationAsync](https://learn.microsoft.com/en-us/uwp/api/windows.security.credentials.ui.userconsentverifier.requestverificationasync?view=winrt-26100)：方法参数和结果。
- [Credential Providers](https://learn.microsoft.com/windows/win32/secauthn/credential-providers-in-windows)：凭据收集与认证的区别、保留系统登录方式、不建议包装系统 provider。
- [GetSerialization](https://learn.microsoft.com/en-us/windows/win32/api/credentialprovider/nf-credentialprovider-icredentialprovidercredential-getserialization)：序列化接口。
- [RDP 与 Windows Hello for Business](https://learn.microsoft.com/en-us/windows/security/identity-protection/hello-for-business/rdp-sign-in)：部署前提与证书路径。
- [Trusted signal unlock](https://learn.microsoft.com/en-us/windows/security/identity-protection/hello-for-business/trusted-signal-unlock)：Wi-Fi 是第二因子。
- [Companion Device Framework](https://learn.microsoft.com/en-us/windows-hardware/design/device-experiences/windows-hello-companion-device-framework)：弃用状态。

## 验证结果（2026-10-17）

- 桥接 Release 构建通过，0 警告、0 错误；部署前后在普通桌面运行 `--status-once` 均成功。
- 同一桥接协议实现的本机自测通过，覆盖合成数据、重复提交、篡改、取消、过期，以及 USB/BLE 拒绝。TAB5 构建通过；实际更新说明 sidecar 的 LVGL 预览通过并已目视核对。
- TAB5 已通过 Wi-Fi 安装 `0.2.217-wifi-test`，用户确认 USB 拔出、电脑蓝牙关闭、Wi-Fi 桥接正常。启动前设备诊断独立确认相同条件。
- 23:14:38 开始真机测试，run 为 `bce09b8293eaacd75f063384ef461d79`。最终桥接状态为 `passed`，`accepted=1`、`replays=1`、`tampered=1`、`cancelled=1`、`expired=1`，`ordinaryProcess=true`、`realUnlockTested=false`。
- 通过表示正常提交成功，其余四种提交按预期拒绝；设备检查响应，桥接独立核对计数。测试前后诊断均为 USB 未连接、Wi-Fi 桥接正常、电脑蓝牙关闭；设备实现对每次请求及过期等待检查纯 Wi-Fi 条件，不回退其他传输。
- 未带设备认证的独立 HTTP 请求返回 401。测试完成后桥接清理会话密钥和 offer，停止发布测试命令；保留的结果只有运行标识、状态和计数。未使用真实 PIN，未执行 Windows 登录或解锁，未修改账户密码或系统认证配置。
- 本次没有接 USB 进行 ELF/分区 VALID 启动核验；已验证的范围是设备报告版本及该设备实际完成的纯 Wi-Fi 提交。此次短时通过也不能证明此前间歇性 Wi-Fi 故障已彻底解决。

证据目录：`artifacts/development/wifi-unlock-20261017/`。主要记录为 `submission-device-before.json`、`submission-start.json`、`submission-result.json`、`submission-device-after.json`、`submission-auth-rejection.json`、`submission-protocol-test.log`、`submission-bridge-build.log`、`submission-firmware-build-final.log`、`submission-preview.log`、`submission-review-manifest.json`。

交付固件：`artifacts/firmware/tab5/latest/aibot_tab5.bin`，7,097,536 字节，SHA-256 `77e8774ceb3354b47e027c59037f94f32c3fc23889bd99641c85f7f7b1f17c7e`。部署桥接 DLL SHA-256 `f6e0f97f1f3466d206d0940e0c5fb125d30526dd3cb379f0f052f34b7141e541`。保留部署前备份，未提交、推送或公开发布。

上述结果证明普通权限下 TAB5 的纯 Wi-Fi 合成提交链路；不等同于真实 PIN 提交或 Windows 解锁成功。
