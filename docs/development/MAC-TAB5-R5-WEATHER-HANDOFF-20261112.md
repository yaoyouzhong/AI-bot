# Mac 交接：320-R5 总览日历、显示与天气定位

## 范围与接续

- Windows → macOS，交接分支 `origin/codex/tab5-status-reliability`。以包含本文及 R5 补丁的提交为交接点；共同基线为 `3f3dacf977f7cd2c8a1867eb1b909517beeb4895`。
- 本轮授权：把确认的农历布局刷入本机 TAB5，整理本轮固件与天气修复，提交、推送。没有正式发布、创建标签或替 Mac 声称原生验收完成。
- 已读取远端 Mac 分支 `codex/mac-tab5-status-reliability-20261111` 的 `af26b5045d8e105c3dca8afb4c2a3ee062d1df21`。保留该分支已有的状态回包修复，先检查本地改动再合并本交接分支，不覆盖 Mac 工作树。
- TAB5 公共协议版本为 `0.2.320-ui`，内部候选 `R5`；桥接正式版本本轮不变。天气字段没有新增，R4 的显示诊断只追加可选后缀。

```sh
git status --short
git fetch origin codex/tab5-status-reliability
git log -1 --oneline FETCH_HEAD
```

确认本地工作已保留后，在当前 Mac 开发分支整合交接提交；不要直接重置分支。只补实际缺口，遵循[统一流程](../DESKTOP-DEVELOPMENT-RELEASE.md)。

## 对齐清单

| 项目 | Windows / 共同固件结果 | Mac 接续 |
| --- | --- | --- |
| 居中农历 | 普通日显示农历；假期、当天节气与农历整体居中，颜色沿用；生日、倒计时、补班提醒独立一行。长文字省略但不隐藏提醒，详情保留完整内容。跨日先更新布局宽度再更新文本，防止较长农历被旧宽度提前截断。 | **已有无需修改桥接。** 使用同一固件；核对 Mac 提供的农历、假期、节气与生日字段即可。 |
| 总览屏闪 | R4 限制 framebuffer-copy DMA2D 读写突发为 32 字节，新增在途诊断；R5 沿用。没有再调低屏幕时钟或更换扫描时序。R4 连续 612.6 秒总览采样欠载与显示错误均为零，用户反馈暂未看到闪屏。 | **已有无需修改桥接。** 不能把有限观察窗口写成永久根治；Mac 实机记录通道、页面、欠载变化与肉眼结果。 |
| 和风坐标 | `QWeatherCoordinates.cs` 只在服务边界将中国大陆自动定位的 WGS-84 转为 GCJ-02；手动地区、海外及港澳台不转换，OS 原坐标与 Open-Meteo 保持 WGS-84。一次明确授权的真实查询返回正确区县。 | **待验证 / 待对齐。** 当前 `MacQWeatherService.fetch` 仍直接格式化传入坐标为两位小数；核对 CoreLocation 来源与手动城市坐标，避免漏转或重复转换，检查区县边界精度。空气质量接口单独按其契约核对，不机械复用转换。 |
| 天气刷新 | `WeatherRefreshSchedule` 独立记录上次成功请求的地区、坐标、供应商与主机；位置/设置变化、权限恢复立即刷新。静止正常天气保持 15 分钟周期，失败保留缓存。 | **待验证 / 待对齐。** 在原生定位与采集调度中实现相同行为；尤其覆盖“设置窗口先保存位置”和“同坐标权限恢复”。 |
| 权限与资料 | Windows UI 线程请求权限；未获授权时不保存自动定位设置，成对保存坐标；提示不展示真实坐标。该候选尚未覆盖安装。 | **待验证。** 使用 macOS 原生权限生命周期；拒绝、取消、恢复时保留设置和最近天气，不照搬 Windows 权限代码。 |
| 既有 Wi-Fi 触控和自动核验 | Windows 已验证；本轮保持兼容。 | **核对已有结果，只补缺口。** 参见[前一份 Wi-Fi 交接](MAC-TAB5-WIFI-TOUCH-HANDOFF-20261111.md)，不要因接收本次布局又重做全部无关测试。 |

天气问题的证据、实现文件与边界见[天气定位交接](2026-11-12-weather-location-followup.md)。

## 固件源码与复现

共同固件仍使用独立工程；源码增量在本仓库交付，构建原包不加入 Git 历史。

1. 已有 320-R3 源码时，依次应用 [R4 补丁](TAB5-320-R4-CALENDAR-SCANOUT-20261112.patch) 和 [R5 补丁](TAB5-320-R5-CENTERED-LUNAR-20261112.patch)。
2. 若从已发布 0.2.319-ui R8 源码开始，先按前一份交接应用 R1、R2、R3，再接 R4、R5；不要在已经应用的目录重复打补丁。
3. 每份补丁先执行 `git apply --check <补丁路径>`；Windows 已在独立 R3 基线上验证两份补丁可顺序应用，结果逐文件匹配最终构建源码。
4. R4 的 `scripts/apply-display-diagnostics.py` 包含 SDK 修订；须应用于对应 ESP-IDF 5.4.2 源码，不能只复制 UI 文件。私有 SDK 构建与 ISR 检查见 [R4 记录](TAB5-320-R4-CALENDAR-SCANOUT-20261112.md)。
5. 不以 `0.2.320-ui` 字样判断原包身份；核对 BIN SHA-256、嵌入 R5 标记、运行 ELF 指纹和分区 `VALID`。

## 验证与边界

- R5：原生 ESP32-P4 构建；完整 LVGL 界面回归；普通日、假期、节气重合、长文字与生日同时显示；真实 sidecar 更新说明；显示 ISR / flash 链 IRAM 检查。实际渲染已查看，示例日期与天气不代表本机实时数据。
- Windows 天气候选：Release 构建零警告、零错误；`--self-test-weather-location`、`--self-test-weather-forecast`、`--status-once` 通过。天气设置渲染、关闭与取消的既有结果绑定同一 DLL SHA-256 `87fa6c22c4365728b33bcfb4c467f98a835a493b4902b129e732a7f472f9819d`，未改变原包时复用。
- 本轮只刷 TAB5 固件，**未安装天气桥接候选**。不得把固件启动核验写成天气设置、Windows 覆盖安装或 Mac 原生验收通过。
- Mac 尚须 `cd mac-app && swift test && swift build -c release`，并完成受影响的真实定位刷新、资料保留和设备展示检查；移动测试使用实际授权位置，不把坐标、密钥或原始运行状态提交到 Git。
- 完整证据在 Windows 本机 `artifacts/t5-320-r5-centered/`；Git 只保留必要摘要、源码补丁与交接。未做正式发布，仍待 Mac 对齐及相应验收后选择发布候选。

## 设备与原包记录

- R5 BIN：7,056,512 字节；SHA-256 `14f8add8efef345124e404137c174f3db3e8abd76fe068f5d9b80b22bb474c4b`。
- 嵌入 ELF 指纹：`a4ac591457bf72832f456e765611c476a50d66cb2df675f992415b0e8dc6901e`。
- R5 增量补丁 SHA-256：`163db166c2932027a2e3f8362603df7fbbf225d67e7d6e78fc9317f0d14d433a`。
- 原包归档：`artifacts/firmware/tab5/versions/0.2.320-ui-r5/14f8add8efef-1352be9a/`；固定交付入口 `artifacts/firmware/tab5/latest/aibot_tab5.bin`。镜像与 `.bin.notes.json` 成对使用，不混入中途构建的 R5。
- 本地构建、实际渲染、完整回归与补丁链校验已通过。用户补充明确批准这份 R5 后，2026-11-12 13:07–13:08（北京时间）已通过原常驻桥接完成 Wi-Fi 升级；实际压缩传输 5,123,801 字节，传输记录耗时 9,562 毫秒。
- 13:08:38 自动执行的启动核验通过：运行 ELF 指纹与上述一致，`ota_0` 为 `VALID`，`previousError=0`；升级前后展示设置读回一致。未重装桥接、未请求用户手动点核验。
- 原始升级记录与启动读回保存在本机 `artifacts/t5-320-r5-centered/device-upgrade-result.json` 和 `device-boot-verified.json`。设备显示短时采样及用户实屏观感另行记录，启动成功本身不等于屏闪长期根治。

- R5 总览实机观察：连续 180.9 秒、13 个采样均处于总览且未进屏保；运行指纹持续匹配，LCD 欠载 0→0、显示错误为 0，亮度与背光设置次数保持不变。用户确认「布局正常，暂未看到闪动」。摘要见本机 `device-acceptance.json`，此结果仅覆盖本轮有限观察窗口。
