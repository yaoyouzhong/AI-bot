# Windows 豆包自动音源与 TAB5 验收

## 当前范围

基线 `1348a0453af6f1ce5ba6135cce0bc2b26a3025f6`，TAB5 继续使用已验收的 `.314` 原始 BIN。正式 Windows/Mac 0.6.17 原包、配对与协议不变；本轮代码生成独立修复候选，不覆盖已发布文件或套用原包验收。

用户要求：有可用大疆时优先大疆，没有时使用 TAB5；豆包“自动检测”已经正确路由到大疆时直接使用，不因模式名称拒绝。确需临时改选时结束后恢复原设置，用户期间手动改选优先。

用户随后要求 Mac 同步优化。Mac 已有临时选择和恢复；本次仅在已知豆包自动模式与当前 CoreAudio 默认物理端点一致时跳过不必要的指定。不同、未知或虚拟输入仍沿用原已验证租用路径。新增四组纯决策测试；Mac 原生构建、实际自动模式 ID 和真机结果由 Mac 本机补齐，不把 Windows 检查当作 Mac 通过。

现场读取：Windows 默认音源为 `麦克风 (Wireless Mic Rx)`；豆包选择为 `ime_auto_detect_microphone_id`，设备列表唯一默认项为同一大疆。用户正常电脑口述后，该设备上可读到 `ImeService` 的非活动录音会话。旧代码仅比较选择字符串与物理设备 ID，因此在录音前拒绝。该会话信息不等同本次已采到声音或 TAB5 识别成功。

## 实现前设计

- 音源：保留现有大疆优先、无大疆时 TAB5 的选择器，以及其他明确设备选择；只补豆包选择模式和物理端点匹配。
- 自动检测：仅在豆包列表有唯一默认物理端点，且它与本次选择的大疆一致时原样复用。不依赖上一次录音会话猜测设备。
- 临时切换：目标必须确实存在于设备列表中；先落盘恢复记录，再改选并读回。结束恢复原“自动检测”或其他选择；用户手动改动优先。未知、忙碌、保存或恢复失败沿用现有保护。
- 小窗：保留标题、两个信息区和 `440×190` 基准尺寸；提示在上、识别文本在下。动态依据可用宽度换行，不新增按钮或改变 TAB5 布局。
- 字体：沿用 `Microsoft YaHei UI`，提示 9.5pt、识别文本 14pt。颜色沿用 Windows 界面的白底与 `SettingsWindow.Ink`（`#202B3B`），间距按 DPI 缩放。
- 渲染：离屏检查正常提示与长错误在标准及较窄窗口、100%/150%/200% 等效缩放下的可读性、换行、留白和输入区。这是原生 WinForms 布局检查，不替代三个实际显示器 DPI 的验收。人工发起实际录音时复核真实小窗与识别回填。

## 验证计划

1. 扩展现有麦克风租用回归：自动检测匹配默认大疆零写入、不匹配时临时选择并恢复、缺失/歧义默认项、没有大疆的 TAB5/CABLE 路径、手动改选及现有异常恢复。
2. Windows 独立候选构建及必要原生语音回归；不覆盖常驻目录。静态和离屏验证与实际录音分别记录。
3. TAB5 原始 `.314` 与不受影响的 Mac 验收按原范围复用；Windows 当前自动检测场景须补真实录音、停止、回填和恢复检查。

## 当前开发验证

- Windows 独立 Release 候选构建通过，零警告、零错误；正常桌面上下文的 `--status-once` 通过。
- 麦克风租用、语音核心与原生 STA 回归通过。测试在独立不可见桌面运行，没有切换用户输入桌面。
- 12 张原生窗口渲染检查通过，覆盖 440/240 基准宽度、正常/长错误提示和三种等效缩放。已查看窄屏长错误与放大渲染，提示完整换行，识别区保持独立。布局由单列两行表格约束提示宽度，无固定 420 像素上限。
- 原语音 UI 测试假设默认设置是“未配置”，但当前默认已启用语音；正式 0.6.17 原包也出现相同超时。用例改为显式无效识别提供方，验证真实 STA 拒绝配置错误且不打开麦克风；未放宽超时或跳过断言。
- Windows 实际 TAB5 `.314` + 本轮候选已由用户点按录音、停止，设备截图显示“测试豆包语音。”与“文字已返回，请检查后发送”。后台为 `source=dji`、`state=review`、`sound=True`、`takeMs=3750`、`finalWaitMs=1438`、`restoreFailures=0`；录音后豆包仍为自动模式，唯一默认输入为大疆，无待恢复记录。未点击发送。候选 DLL SHA-256：`49305e1c3f83e8bc002224fc0fec4d9d1a38901ba7c724b0cba138032e76276d`。
- 首次切换两侧均有同一短语，因之前电脑口述也使用过该短语，不能据此认定串入。以当前 Codex 原文字为基线，用户在同一 Claude 验收会话第二次口述；截图显示 Claude 新增“第二次隔离验证。”，Codex 原文字未变，切回 Claude 两句均保留。此次独立语句的客户端隔离检查通过；没有发送文字，也不推断首句历史来源。
- 第六次临时诊断结束时，工具最后的显示设置读回遇到现有“设备忙”保护，未取得 `settingsPreserved` 结果，不记作该项通过。原常驻已恢复，配置文件哈希不变，候选及固定常驻 DLL 均未被替换；后续独立会话补查收尾，不重做已完成录音。
- 第七次设置读回也遇到忙保护。诊断入口增加最长 30 秒的空闲等待，涵盖检查后新 RPC 插入的竞争；取消和实际读回错误仍失败，原有前后设置逐项比较保持。四类定向回归（空闲等待、竞争、取消、I/O 失败）与语音回归通过，新开发候选另存于 `candidate-with-diagnostic-wait`，前一份录音候选未覆盖。此项不改变日常录音或设备协议。
- 新候选 DLL SHA-256：`7389b42066444bebec713e3f65b7e33a1d02716134d248ec8c95deeb94f0f36f`。第八、九次诊断均正常结束，`settingsPreserved=true`，配置文件哈希保持。第八次用户实际拍照并使用，Claude 缩略图和两句文字在切换后保留，Codex 未带入照片；设备诊断记录 Wi-Fi 上传 87,646 字节、577 ms、状态 200，控制仍走 USB，没有发送到模型。
- 已备份原固定目录的 7 个变化文件，再部署 45 项已核验运行文件中的变化项；部署后所有 45 项摘要与候选一致，正常桌面 `--status-once` 和常驻启动通过，配对、设置及语音偏好摘要保持。正式发布 ZIP 未修改。本地常驻现为上述开发修复构建，不能把它写成正式 0.6.17 原包的二进制验收。
- 用户拔掉 USB 后，三个新鲜设备确认样本通过 Wi-Fi 更新（12:30:49、12:30:54、12:31:01）；BLE 同时存在，此项是优先级接续，未声明禁用 BLE 的纯 Wi-Fi 隔离。插回后 COM9 重新认证，USB 数据年龄为 19/133/61 ms，确认时间持续前进，USB 优先恢复通过。

## Mac 交接范围

按[跨平台交接模板](CROSS-PLATFORM-HANDOFF-TEMPLATE.md)登记：来源 Windows，接收 macOS；任务分支 `codex/windows-dynamic-microphone-20261105`，基线见上文。本轮本地提交及远端到达情况以 Git 记录为准，不能把基线 SHA 当作已经收到本次修复。当前属于开发与验收，没有正式发布。目标为两端音源行为对齐，未升号；最终发布负责人仍为 Windows。协议、配对格式和最低固件版本无变化。

只需同步 `MacDoubaoMicrophone.swift` 和 `MacDoubaoMicrophoneSelectionTests.swift`。已知自动模式 ID 沿用已有 `MacDoubaoBackgroundValidation` 中的 `ime_auto_detect_microphone_id`；默认端点每次从 CoreAudio 现读，不使用上次录音缓存。明确选择已匹配、自动模式匹配默认物理端点时直接沿用；其余情况走原有临时选择、验证和恢复。TAB5 虚拟音源仍需原有明确选择。

Mac 本机执行 `swift test --package-path mac-app`、`swift build -c release --package-path mac-app`，再补三条受影响实测：自动检测已指向大疆时不改设置并完成识别；自动检测指向其他输入时临时选大疆并恢复；期间手动改选后保留手动选择。缺少大疆时仍验证 TAB5 原有音源路径。复用其余不受影响验收，不重编 TAB5、不替换现有 0.6.17 正式附件，不把 Windows 静态检查登记为 Mac 通过。

| 对齐项 | 状态 | 证据与剩余事项 |
| --- | --- | --- |
| Windows 自动音源与真实录音 | 已完成 | 限开发候选的构建、自测、原生渲染及上述大疆真机录音；未生成正式安装包。日志在本机 `artifacts/windows-dynamic-microphone-20261105/` 与 `artifacts/windows-tab5-314-acceptance-20261105/usb-session-06-candidate/`，不公开个人运行数据。 |
| Mac 自动模式复用实现 | 已完成 | 本地源码及四组决策回归用例已编写；不是 Mac 构建或实机通过。 |
| Mac 原生构建和受影响录音 | 待验证 | 须在 Mac 本机执行以上命令与步骤，影响本次 Mac 修复交付。 |
| 已发布附件、签名与固件 | 已有无需修改 | 现有原包不变；本轮未构建 Mac 包，不新增签名、公证、安装或刷机结论。 |

接收端第一步：取得包含本轮两份 Mac 源文件的实际提交后，在 macOS 运行定向测试与 Release 构建，再记录新 Mac 包的源码 SHA、文件大小、SHA-256 和实测结果。当前无新标签、草稿附件或公开发布结果。

## 既有验收与未完成项

本轮自动验收已覆盖真实 Windows USB 采图/点按/滑动、Codex 总览直达与返回、Claude 真正供应商回复阅读、年度/全量图库切换与桥接重连后进度保留，以及上文的录音、照片、客户端隔离和拔线接续。未通过项和现场原始记录位于本机 `artifacts/windows-tab5-314-acceptance-20261105/`。

Claude 的一条隔离测试输入首次显示送达未确认，未重复发送。随后只读核对持久回执与真实供应商 UUID 一致，结果为 `accepted` / 202、供应商侧仅一条测试输入；TAB5 已实际阅读到 `ok` 回复。历史会话的“状态未知”不能据此解释为送达失败。

独立 `.315` 已按用户对精确 BIN 的批准完成升级、精确启动核验、两客户端菜单切页与手指操作验收。原照片只提交一次，持久回执 `accepted` / 202，之后用户仅发送不同的继续指令；从正常 Windows 托盘核对并允许一次真实 `Read`，供应商读取成功并回复“照片已收到”，用户在 TAB5 确认显示该回复和“已完成”。本轮 Windows 受影响路径验收通过，详见[定向记录](TAB5-PROJECT-DROPDOWN-CLOSE-20261105.md)。Mac 新麦克风修改的本机构建和受影响录音验证仍待接收端完成。正式发布与刷机状态单独记录，不以代码提交代替验收。

## English

The Windows candidate preserves the accepted .314 firmware and released desktop archives. Reuse Doubao automatic selection only when its unique default physical endpoint matches the selected DJI. Otherwise temporarily select a present endpoint through the existing durable, verified recovery lease; retain a user's intervening manual choice. The no-DJI path keeps the existing TAB5/CABLE behavior. Improve status wrapping in the existing voice draft window and validate native rendering separately from physical recording. macOS uses its existing native microphone route; no shared protocol change is introduced. Publication and successful delivery are not claimed by these development checks.
