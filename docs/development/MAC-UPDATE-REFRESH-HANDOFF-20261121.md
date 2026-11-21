# Mac 软件固件刷新对齐

## 范围和源码身份

- 来源为 Windows [PR #32](https://github.com/yaoyouzhong/AI-bot/pull/32)，主干合入提交为 `6654317650f5408e750d2ef9ef9597a2e794b58d`。Mac 开发分支 `codex/mac-update-refresh-20261121` 基于 `af9b8b196c6092173ff7fafeca370c63ab9109ad`。
- 本次对齐「软件固件」的正式发布目录检查。共同行为覆盖首次打开、缓存时效、后台调度、失败恢复和手动检查。沿用现有发布目录、组件与设备协议、更新资格、安装交接和通知机制。桌面安装、固件和发布版本无改动。

## Mac 界面设计

- 从设备中心或通知打开「软件固件」时，版本表和所选说明立即展示已有数据。初次查询或缓存过期时，沿用页脚的原生次要状态文字显示正在读取。刷新失败时同位置显示错误并保留已有版本、徽标数量和选择。通知指定的项目保持选中。
- 沿用 800 × 640 点窗口、600 × 570 点最小内容区、正文系统字体、10 点区块间距和系统浅色、深色样式。上方原尺寸「检查更新」作为主动刷新入口。请求进行时沿用禁用态和底部「取消检查」按钮，保留原按钮点击区。检查结束恢复手动入口，原组件操作按当前选中项和设备状态启用。

## 对齐清单

| 用户行为 | Windows 来源 | Mac 接收位置 | 状态及证据 |
| --- | --- | --- | --- |
| 打开软件固件页，首次或成功结果满 5 分钟时自动刷新 | `UpdateCenterForm`、`UpdateService` | `MacUpdateWindow.open`、`MacUpdateCheckCadence` | 已完成。原生窗口回归验证首次打开、连续打开时共用正在进行的请求、复用 5 分钟内的结果和保留指定组件。 |
| 后台检查成功后约 1 小时再查；失败后约 15 分钟后重试 | `TrayApplicationContext`、`UpdateService` | `MacUpdateWindow.startAutomaticChecks`、`MacUpdateCheckCadence` | 已完成。时钟模拟测试验证前后台共用上次请求结果及两种间隔。Mac 首次后台检查在启动授权检查结束约 30 秒后进行，受「自动检查并提醒」开关控制。 |
| 请求失败保留最近结果；1 分钟后再次打开可重试；手动直接刷新 | `UpdateService`、`UpdateCenterForm` | `MacUpdateWindow.check` | 已完成。离屏窗口验证断网、结果保留、重试门槛、取消后立即重新打开、读到新版本和缓存仍有效时手动检查。 |
| 已验收的 Windows 刷新、原生安装与通知入口 | Windows PR #32 | Mac 本次代码仅实现上述检查行为 | Windows 已有无需修改。PR 记录原生 Release、状态快照和更新自测均通过，Windows 和工具 CI 均成功。[Windows CI](https://github.com/yaoyouzhong/AI-bot/actions/runs/44881039520/job/136776663775) |

## 构建与验收记录

- 在 macOS 27.3 arm64 上运行 `swift test --package-path mac-app`，544 项，4 项跳过，0 失败。 `swift build -c release --package-path mac-app` 构建通过。
- 带版本信息的隔离 App 通过 `--self-test-tab5-ui` 原生离屏检查。图库测试夹具使用早于当前桥接的模拟发布版本，使图库计数断言不受实际 App 版本影响。目视复核刷新中和离线状态的浅色、深色、默认与最小窗口，表格和说明保留，状态文字与底部按钮完整。代表截图和验证摘要在本机 `artifacts/development/mac-update-refresh-20261121/`。
- 安装包、实际系统通知送达及点击仍待候选验收。变更限于桌面查询，TAB5 和 ESP8266 真机刷写不适用。

## 发布交接

- 本地开发交付，版本与发布负责人待定。无冻结安装包、标签或发布附件。
- Windows 的目录检查策略已合入主干。Mac 后续候选验收应在实际安装包中检查启动后的自动查询，以及收到系统更新通知后能跳到对应项目。
