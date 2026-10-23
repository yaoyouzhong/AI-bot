# Codex 直达锁屏反馈修复（本地，未部署）

定位失败原先设置通用 `quick_failed`，使底部错误状态显示“重试录音”，即使尚未确认目标会话。现在仅在已确认语音目标的失败状态显示录音重试；定位失败保留 Codex 主按钮用于新点击重试。

Windows `open-recent` 新增 409 / `desktop_not_interactive`。通过 `GetUserObjectInformation(UOI_IO)` 检查桥接线程所属桌面是否接收输入，在桌面不可交互时拒绝启动/定位及 Alt 回退。TAB5 显示“电脑桌面不可操作，请解锁后再点 Codex”。不把锁屏、安全桌面或会话断开精确归因为某一种原因。解锁不会重放旧请求；必须新点击。原草稿不修改。

## 源码与验证

- 修复保留在当前 AI-bot 开发目录及相邻 TAB5 开发目录，并同步到实际 0.6.9 / 217 来源：`C:/Users/yaououzhong/.codex/worktrees/release-067-205/AI-bot` 和 `artifacts/release-068-20261016/tab5-saver-source`。
- 当前来源 Windows Release 构建通过，0 警告、0 错误；`--self-test-tab5-quick-console`、`--self-test-tab5-rpc` 通过。新增回归覆盖锁定桌面不激活/不发送 Alt、不打开/读取草稿、解锁后旧请求仍拒绝、新点击恢复定位。
- 原生只读桌面探针在本次执行时返回 `true`。未主动锁定电脑；真实锁定/解锁路径仍待部署后的实机验收。
- 当前来源 TAB5 `scripts/build.ps1` 通过；沿用源码版本号进行编译验证，不构成新版本交付。
- 开发目录及当前交付来源的原生 LVGL 全套回归、锁屏提示字形和可见预览均通过，输出 `LVGL_PREVIEW_OK`。当前来源使用独立 CMake 构建目录，避免共享工具目录中的旧缓存指向另一份源码。`preview-current.log` 保存全套结果，`quick-console-desktop-locked.png` 已目视核验：提示完整、无录音重试按钮、原 Codex 主按钮保留。
- 默认 Windows 输出目录被运行中的桥接占用，因此改在独立目录构建；未停止桥接。`--status-once` 在当前启动环境下被既有数据目录保护拦截，返回 2；通过桌面 Shell 启动的复查仍受相同拦截。保留保护逻辑，不将本项记为通过。
- `git diff --check` 在本次改动文件上通过。未提交、推送、发布、安装桥接或刷机。

证据：`artifacts/development/codex-lock-20261017/`。其中 `bridge-current` 是当前交付来源的本地测试构建；当前来源固件构建 SHA-256 为 `6771de0da9616fe5e166d74ab6fb74edf71ef835b6c2280796f9218f62455aed`。正式选择路径 `artifacts/firmware/tab5/latest/aibot_tab5.bin` 仍为原 217，SHA-256 `bb3a94f9e1862c200a81d5176852966663b0d456559f8f59e8b1eed0714d3e96`，未替换。

接口依据：[Microsoft GetUserObjectInformation / UOI_IO](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getuserobjectinformationa)。

用户后续询问应用控制页解锁能力，并明确选择“先了解可行性，暂不开发”。本次没有新增解锁功能、保存凭据或安装 Windows 登录认证组件。
