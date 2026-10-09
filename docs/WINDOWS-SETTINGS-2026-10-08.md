# Windows 更新、天气与股票设置修订

本次为未发布的 Windows 桥接变更，不修改 ESP8266、TAB5 固件或组件版本号。

- 天气源与位置选择独立：已配置和风的用户沿用和风；无密钥默认免费 Open-Meteo，也可明确选择免费源而不删除和风配置。仅和风模式显示 Host／Key，未保存输入在切换时保留，取消不保存。定位权限提示归入位置区，测试结果／错误在操作按钮上方显示。免费源为数值预报、美标 AQI，个人非商业免密钥；和风支持中文城市／区县及当地 AQI，费用和可用接口依账户额度。两者均接入逐小时及 7 天预报，和风请求失败沿用免费回退、全部失败保留上次数据。源间天气／AQI 数值不能视为相同口径。
- 生日窗口左侧列表、右侧编辑；历法、日期、闰月及提醒分组，移除与保存独立。保存前沿用姓名／日期校验，取消不写入生日文件。
- 更新窗口用四列显示组件、当前版本、可用版本与简短状态；所选组件的升级条件和操作独立显示。版本未知的小屏显示「未上报」，保留手动升级与完整备份要求，不声称检测到新版本。
- 「账号数据 → 自选股票」只管理股票列表。「账号数据 → 天气定位」统一管理电脑自动定位与手动城市／区县；普通用户不需要输入经纬度。天气测试／保存结果固定显示在操作按钮上方。
- 自选股票输入框支持代码、中文名称、全拼和拼音首字母查询。选择结果后点击「加入自选」，也可双击结果。相同代码可能属于不同市场，例如 `000001` 对应沪市上证指数及深市平安银行，因此查询不会自动添加第一项。
- 自选列表显示名称、市场、代码和数量，可移除项目、上移／下移调整展示顺序，或批量粘贴带市场前缀的代码，最多 20 项。保存并重新打开时保留顺序。保存股票不覆盖其他窗口更新的天气或设备设置。搜索请求取消、过时、失败或解析失败时保留已有列表。
- 启动任务不存在时，Windows COM 可抛出 `FileNotFoundException (0x80070002)`；现在与对应 COM 异常一样视为未登记任务，避免设备中心打开失败。其他权限错误不按不存在处理。
- 开机启动使用勾选框，显示当前状态；保存期间暂时禁用并提示正在开启／关闭，回读成功后显示结果。失败时恢复实际勾选并显示原因，设备中心刷新保留进行中的操作。任务写入放在后台，避免窗口看起来没有响应。
- 服务状态按原生字体行高调整默认高度，优先完整显示 8 行数据／AI 额度；设备表按实际行数占用高度，剩余空间用于数据表。最小窗口仍显示多行和底部操作，空异常／通信说明不占位；屏幕工作区不足时沿用窗口适配限制。

搜索读取腾讯的公开建议数据接口 `https://smartbox.gtimg.cn/s3/`，与既有腾讯行情来源配套；不执行返回的 JavaScript，不使用账户凭据，限制输入／响应大小并校验结果格式。该接口依赖网络与外部服务，不承诺返回全部匹配证券。2026-10-08 已实测代码、中文、首字母、全拼、A/H/美股及指数共 10 组查询。返回的美股上市市场后缀会转换为现有行情接口可用代码，保留 `BRK.B` 等股份类别。

验证包括隔离 Release 构建、真实公开搜索、股票搜索／选择／保存回归、缺失启动任务及实际 Windows 开机启动状态只读检查、更新交接回归，以及默认／最小窗口和缩放预览。股票排序还验证到桥接行情快照，服务器返回顺序不同也按保存列表排列。预览使用原生控件与演示数据；不等同于运行中的旧程序已经更新，也未执行安装、刷机或开机启动配置变更。本地证据在 `artifacts/development/update-center-layout-20261008/`；`--status-once` 的桌面运行验收结果另行记录。

## English

These unreleased Windows bridge changes leave ESP8266/TAB5 firmware and component versions unchanged. The update center separates short version/status rows from the selected component's requirements and actions. Unknown legacy firmware remains an explicitly confirmed manual migration.

Stock preferences now contain only the watchlist; automatic location and manual city/district selection belong to Weather & Location, without manual coordinate fields. Search accepts codes, Chinese names, full pinyin and initials. Users explicitly select a named market/code result before adding it, because codes such as `000001` are ambiguous across markets. The list supports removal, saved up/down ordering, prefixed-code batch entry, deduplication and a 20-item limit. Saving stocks preserves newer weather/device settings; failed or superseded searches preserve existing selections.

Suggestions are parsed as bounded data from Tencent's public suggestion endpoint, never executed as scripts. Ten real queries cover code/name/pinyin searches and A/H/US shares and indices. This network service is an external dependency and does not guarantee exhaustive results. US listing suffixes are normalized while share classes such as `BRK.B` are preserved.

An absent Windows startup task can surface as `FileNotFoundException (0x80070002)` rather than COMException. Both now mean an absent task; permission failures remain distinct. Validation covers isolated Release builds, live search, native search/add/remove/save flows, startup-state reads and Device Center reopening, update handoff regressions, and default/minimum/scaled native previews. Saved ordering is also checked in the bridge quote snapshot when the server returns a different order. No installer, firmware write, startup configuration change or public release is performed.

Startup is now a checkbox with immediate pending feedback, verified acknowledgement and rollback on failure. Background task writes keep the UI responsive; Device Center reloads retain the pending action. Service Status uses measured native row heights to target eight complete data/AI quota rows by default, fitting the device table to its row count and retaining footer actions at minimum sizes. Empty error/communication labels no longer reserve space; existing screen work-area limits still apply.

候选程序桌面运行检查已通过，见本地 `VALIDATION.md` 和 `desktop-status-check.json`。为支持没有附加控制台的输出重定向，`--status-once` 直接向标准输出流写 UTF-8 JSON，不更改控制台代码页。最终运行验证明确指向隔离构建的候选 DLL；常驻旧程序未替换。

The candidate's desktop status check passed; see the local `VALIDATION.md` and `desktop-status-check.json`. The status command writes UTF-8 JSON directly to standard output without changing the console code page, supporting redirected launchers without an attached console. Final runtime verification explicitly used the isolated candidate DLL; the resident application was not replaced.

本次勾选框和服务状态增补的验证记录在 `artifacts/development/bridge-ui-feedback-20261008/`。原生勾选交互、延迟保存／重复点击／刷新／失败回退／回读不一致、多设备与默认／最小／附加缩放布局均通过；候选 DLL 的 `--status-once` 经 Windows 桌面启动路径读取成功。只读取实际开机启动状态，没有改变系统启动配置，常驻程序未替换。

Evidence for the checkbox/status additions is under `artifacts/development/bridge-ui-feedback-20261008/`. Native checkbox activation, delayed save/reentry/reload/failure/readback mismatch and multi-device/default/minimum/scaled layouts passed. The candidate DLL's status command also succeeded through the Windows desktop launch path. Actual startup registration was only read, and the resident application was not replaced.
