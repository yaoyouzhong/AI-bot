# TAB5 取消后恢复草稿：设计与交接

## 范围与设计

修复真实 TAB5 上已确认 202 的追加消息取消后，桥接已恢复草稿、设备输入框却仍为空的同步遗漏。基线桥接源码 `854f679c7a2d06e23c234f0166cd35108045cc25`，TAB5 .313 原应用 `e0c0981beae8525e74fc1aeb3880904aeb95df909d63f5b9a39162ec72deb587`。只修改 TAB5 的既有中断成功后草稿刷新，不变更 RPC 字段、桥接安装包或 ESP8266。

继续使用现有“对话”页的项目/会话列表、输入框和照片标签；1280×720 布局、字号、颜色、间距、按钮层级与触控范围均不变。中断成功后，只有无待核对请求、未被编辑且文字与全部照片均为空的本机草稿才重新加载。加载成功通过既有输入框、照片标签及“已恢复草稿”提示展示；返回明细/输入页的路径不变，不新增弹窗。

服务端已在返回中断成功之前记录 CLI 的取消确认并恢复草稿。设备复用 `draft-load` 和现有请求 ID、会话及 revision 校验。用户已有新文字/照片时保留；刷新期间产生的新输入通过 revision/dirty 检查拒绝迟到结果。待核对发送保持原草稿/回执恢复路径，不被重载覆盖。Codex 与 Claude 共用同一刷新规则。

## 验证计划

先用真实 LVGL 离屏回归复现失败，再验证取消→草稿加载→文字/照片渲染、新草稿保护及刷新期间编辑保护。编译目标 TAB5 .314，预览实际 sidecar 后部署一次；核验精确镜像与启动状态、保留展示设置。真实 TAB5 使用隔离合成会话重复原失败场景与新草稿场景，保存截图及回执。沿用不受影响的既有验收，不扩大为全功能重测。Windows/Mac 使用相同设备固件；Windows USB 实机结果只由 Windows 记录。

This fixes a TAB5 cache invalidation omission after a confirmed interrupt. Empty, unchanged local composers reload through the existing draft RPC; newer text/images and edits during reload keep priority. Layout and wire fields are unchanged. Native bridge packages retain their existing identity. Build, actual rendering and physical evidence are recorded separately below after verification.

## 结果与交接

来源平台为 Mac，接收端为 Windows 发布负责人；基线主干已同步至 `56eefba624a5c9789c13923444fcd9f445f6a748`。修复保存在任务分支 `codex/tab5-claude-dialogue`，交接提交由 Git 记录。用户本次授权修复、自动验收及更新 GitHub 交接，不操作主干推送、正式标签或公开发布。

| ID | 内容 | 状态 | 证据与边界 |
| --- | --- | --- | --- |
| C1 | 中断成功后刷新未编辑的空草稿 | 已完成 | .313 离屏测试先失败；.314 的真实 LVGL 对 Codex/Claude 都通过。真实 TAB5 的 Claude 202 追加取消后恢复 `queued A` 与一张合成照片。 |
| C2 | 保护新草稿 | 已完成 | 两客户端离屏测试覆盖新文字、新照片、刷新期间编辑；真机在取消前输入 `new`，取消后仍保留，旧追加回执转为最终 409。 |
| C3 | 既有待核对/拒绝/切换行为 | 已完成 | `TAB5_RECOVERY_ONLY`、`TAB5_CODEX_REJECTION_ONLY`、`TAB5_WORKSPACE_PARITY_ONLY` 通过；未重开无关全功能验收。 |
| C4 | Windows / Mac 桥接、ESP8266、协议字段 | 已有无需修改 | 本次只改 TAB5 现有 draft-load 触发，继续兼容 0.6.17。没有新的电脑端安装包或 ESP8266 刷机要求。Windows USB 本次未在 Mac 代验。 |

### 设计与实际渲染

使用既有输入框与照片标签恢复内容。实际 LVGL 检查恢复文字/照片与保留新草稿的两客户端画面，可读性、间距、按钮及截断正常。使用实际 `.bin.notes.json` 检查更新说明顶部与滚动末尾，分类标题和列表均正常。未新增控件或更改字号、颜色、尺寸。

### 构建与真机

- TAB5 `0.2.314-ui`，6,924,000 字节；应用 SHA-256 `f3f98f2019c46ac9c49be3b60fe7f550b1b03ee0c5732e044796c53bbc21dd99`。
- ELF SHA-256 `eace7cc46df3bef73f9dc27d49f4c3df04a5d4ceb169293c66984d4abe27b3a1`。
- 原生 ESP-IDF 5.4.2 构建通过；SDK 配置值相对 .313 无变化，276 个 main 输入只修改 `ui.c`。通过正式准备脚本生成 `artifacts/firmware/tab5/latest/aibot_tab5.bin` 及 sidecar、摘要与历史归档。
- USB 一次部署后，精确 ELF、VALID 分区和三个实时 UI 样本核验通过，展示设置保留。真实设备为 1280×720、rotation=1。
- 两条已确认 202 的追加消息都在取消后变为 `409 message_cancelled` / `deliveryFinal=true`。场景 A 的设备输入框恢复文字及照片；场景 B 的设备与桥接都保留 `new`。
- 桥接使用原 `854f679` 生产处理逻辑及隔离的确定性合成 CLI 注入/测试检查程序，仍走正式加密 USB、真实设备触控与截图。该诊断程序不是正式 Mac ZIP；没有真实供应商网络、本轮 Windows USB 或物理触摸手感的新验收声明。原正式 Mac ZIP 与 465 项原生测试（1 skip）、29,068 parity 及不受影响验收按原范围复用。

### 源码与接收方式

基于仓库中的完整 .313 源码 ZIP，应用 [`TAB5-CANCELLED-DRAFT-314-20261105.patch`](TAB5-CANCELLED-DRAFT-314-20261105.patch)，按 [`TAB5-CANCELLED-DRAFT-314-SOURCE.json`](TAB5-CANCELLED-DRAFT-314-SOURCE.json) 复核。这三项文件是 `firmware/main/ui.c`、预览入口及新增定向测试；补丁已在独立目录逐字节应用验证。构建版本参数 `TAB5_LOCAL_VERSION=0.2.314-ui`；原依赖锁定、SDK 补丁及许可沿用完整 .313 源码。

Windows 接收本分支和 GitHub 交接附件后使用这一份已验收 .314 BIN；不要重新构建并沿用本次真机结论。0.6.17 电脑端程序不变：Mac 原包 SHA-256 `fe6a9bdd23869435fbe7fc52c5be04e7fe123631c30a8c643518fba774ab6996`；Windows 沿用既有对应原包。不需要重编桥接或凑新版本。

本地完整证据位于 `artifacts/cancel-draft-fix-20261105/`；GitHub 草稿附原始 OTA ZIP、SHA-256、源码补丁和脱敏真机证据。保留 .313 原包及旧失败记录；本次通过只绑定上述 .314 镜像，不倒签 .313 验收。

The .314 firmware passed targeted native LVGL cases for both clients and two real-TAB5 Claude cancellation cases using an isolated deterministic CLI peer over production authenticated USB. It restores text/photo into an unchanged empty composer and preserves a newer composer. Exact image/ELF, VALID boot and three live samples were verified. The desktop 0.6.17 archives and wire fields are unchanged. Existing evidence retains its original scope; no Windows USB or real provider-network test is newly claimed. The source delta applies byte-for-byte to the complete .313 archive. Use the accepted BIN directly rather than rebuilding it for handoff.


### 收尾

两个合成会话的测试文字和照片已通过设备既有控件清空并确认桥接读回为空；输入模式恢复为进入时的中文，返回总览。自动化 stop 返回 `settingsPreserved=true`。临时合成资料随程序退出删除；官方最终 Mac 原包恢复后台运行，监听及 USB 打开均另行核对，不请求电脑前台窗口。
