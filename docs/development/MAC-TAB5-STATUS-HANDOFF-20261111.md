# Mac TAB5 状态回包对齐交接草稿

本页保留首次交接基线。后续补测结果与 Mac 接续清单见 [Wi-Fi 触控与升级核验交接](MAC-TAB5-WIFI-TOUCH-HANDOFF-20261111.md)。

## 范围与接续

Windows → macOS。交接分支为 `codex/tab5-status-reliability`，基于 `471249e59ab0f9fc0fc98a4cbd5c9cd75c10d040`。交接源码以包含本文件的提交为准。可先拉取分支审阅，等 Windows 真机剩余两项通过后再开始 Mac 实现。

~~~bash
git fetch origin codex/tab5-status-reliability
git log -1 FETCH_HEAD
~~~

目标是让已配对的 TAB5 在 Mac 状态生成变慢时仍能及时取到最近一帧有效状态。沿用现有 TAB5 HTTP 状态接口、鉴权、加密和固件能力协商。TAB5 待发布版本是 `0.2.320-ui`，内部候选是 320-R1。桥接发布版本待定。

## 对齐清单

| 项目 | Windows / TAB5 现状 | Mac 对齐位置和验收 |
| --- | --- | --- |
| Wi-Fi 状态回包 | **已完成 Windows 实现及并发回归。** `Tab5Service.Respond` 在验签后读取一次发布的完整状态与兼容帧，避开状态发布锁。缓存过期时停止回传旧状态。 | **待验证。** 从 `MacTab5Service.status` 入手。目前它持服务锁调用 `frameLocked` 生成状态，再编码并加密。先用 `HTTPStatusServer` 的回包耗时确认现场基线。若确认阻塞，提前生成状态，缩短读路径的持锁时间。 |
| 总览项目名与断联提示 | **已完成固件实现、离屏预览及 319-R9 启动核验。** 总览项目行去掉分页标记，断联提示按设备最近收到的认证数据判断。总览实屏及 USB 拔线验收待完成。 | **不适用 Mac 源码。** 使用同一 TAB5 固件验证总览与链路，固件改动见 [320-R1 源码补丁](TAB5-320-R1-OVERVIEW-LINK-20261111.patch)。 |

Mac 侧保留设备归属与配对状态检查、请求绑定的加密、固件容量选择、资源回执和传输优先级。缓存需关联配对代次与单调时间，过期、停用或配对变化后应失效。并发回归应覆盖状态生成暂停时已认证请求仍能读取新鲜帧，以及缓存到期后无法再取旧帧。另测错误凭据、设备切换、压缩与兼容帧、资源暂停和恢复。Windows 的并发用例在 `Tab5TelemetrySelfTest.VerifyWifiStatusDuringPublicationAsync`。

## 构建与验收证据

| 对象 | 身份与结果 |
| --- | --- |
| Windows 本地桥接 | 实机使用的候选 DLL SHA-256 为 `aadbfc62cb6706b5e7cf4b899436f7be57e77bac140541301f5608c2a022edbd`。Release 构建、`--status-once`、TAB5 遥测与通用自测通过。候选已在 Windows 安装并启动。 |
| TAB5 319-R9 | BIN SHA-256 为 `edc6b59c9ba78b855eb19b7469833f861c0d6e91c9f332f1af99ccb6ae570e8a`。已走 Wi-Fi OTA，启动核验读回 ELF `88e0c750b404bf402616eefb8c340ac9659451e2bf17ba229ba8a89e6e5b4590`，分区 `ota_0` 为 `VALID`，显示设置升级前后相同。 |
| TAB5 320-R1 | BIN 为 7,052,768 字节，SHA-256 为 `05ae073e732ea33498a411f549c03154e184ff1afee7c90e59aa39ea25283475`。与 319-R9 对应同一轮功能修复，按 320-R1 重新构建。镜像、内部修订、升级说明和离屏总览通过检查。依用户决定，本轮使用已经刷入的 319-R9 验收。320-R1 的真机启动记录待取得。 |
| macOS | 原生实现、构建和真机验收均待执行。 |

旧桥接现场出现过 TCP 连接成功、HTTP 响应头未返回，Wi-Fi 有效数据年龄约 25.6 秒。新桥接安装后，USB 接着时采样 46 次，跨度 2 分 18 秒，Wi-Fi 数据年龄最高约 4.3 秒。这段采样覆盖 Wi-Fi 状态回包，USB 仍是设备优先通道。草稿保留未单独实测。

## 剩余步骤

1. 在 Windows 配对的 319-R9 上，实屏确认总览项目行去掉 `1/2`。保证 TAB5 供电后拔掉电脑 USB 约 45 秒，从诊断确认实际使用 Wi-Fi，观察总览持续更新及断联提示，再恢复 USB。
2. Windows 两项通过后，在 Mac 复现状态生成阻塞的情形并按结果对齐。原生运行 `cd mac-app && swift test && swift build -c release`。用 Mac 配对的 TAB5 验证 Wi-Fi 回包、拔掉 USB 后的更新、短暂断网与恢复，并复查资源传输和认证。
3. 按 [统一开发发布流程](../DESKTOP-DEVELOPMENT-RELEASE.md) 回填实际 Mac 源码提交、候选摘要与实机结果。发布候选继续按各自镜像指纹登记验收。
