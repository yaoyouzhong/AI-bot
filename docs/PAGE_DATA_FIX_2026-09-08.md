# 页面数据遗漏修复

用户在切换天气时看到 Waiting for data，并反馈其他页面也不正常。此前 USB 测试只检查资源 ACK、模式和心跳，未验证渲染状态，不足以证明页面功能通过。

## 原因与修复

`updateStatus(JsonObjectConst data)` 对只读成员使用 `is<JsonObject>()` / `is<JsonArray>()`。本地 ArduinoJson 7.4.3 的 `JsonVariantConst.hpp` 对不支持的可变类型返回 false，导致所有复杂页面数据被跳过；独立时钟和活动状态仍能更新。改为 Const 类型判断，不改变既有状态数据格式、屏保布局或配网配置。

设备信息增加 `page_data` 诊断。合成数据验收检查天气、股票、全部额度、系统和音乐的可用性，并核对温度、股票数量、CPU 值。缺少诊断字段或状态错误必须失败。新增 `--test-live-device-data` 使用真实 BridgeRuntime 和资源发送器，核对天气温度、股票数量及系统可用性。两者均不代表光学/视觉验收。

## 现场结果

- 修复前固件运行增强测试退出 1：Page data was not decoded into firmware render state。旧固件缺少诊断，因此此项证明新测试拒绝旧验证标准，不是旧固件逐字段读回。
- 修复版编译、COM7 刷写与写入校验通过。固件 SHA-256：`81811126B4796E5E999FE55DF2FC2B885926B6FD578261CB668A3AF2692C1599`。
- 增强 `--test-device-pages` 退出 0：四类资源、九模式逐次页面数据检查、亮度、USB 超时与恢复、原模式/亮度恢复通过。
- 清除合成测试的内存状态（重启设备，不清配网）后，纯真实数据检查退出 0：weather=true、temperature=25.4、stock_count=6、codex=true、system=true、cpu_percent=21.06084，USB 状态计数 2。
- 此次真实数据中 Claude、四家国产额度和音乐均不可用。不可把合成测试可用当作真实授权/会话可用，相关功能仍需继续核验。
- Windows Release 构建零警告/错误，USB 管理自测、菜单分组/路由自测通过。托盘按旧版七组恢复，左键切换镜像；自定义轮播尚未对齐，明确标注。Logo 等待用户确认素材来源，尚未更换。
- Wi-Fi 回退未重测，原有网络隔离限制仍存在；macOS 未验证。实体屏和实际托盘交互待用户确认。未推送或发布。
