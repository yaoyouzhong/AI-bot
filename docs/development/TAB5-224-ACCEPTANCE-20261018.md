# TAB5 0.2.224-ui 实机验收

2026-10-18：用户确认 USB 与蓝牙一键解锁成功，成功后面板自动关闭，TAB5 解锁布局正常。蓝牙首次尝试提示“电脑连接未就绪”，后续成功；该首次失败保留为未定位问题，不能宣称稳定性全部通过。

USB 启动核验已确认 ELF `7cb8ef83958a0f641dd65e7c76110c155f26f016d1162e3a7515dd3689575a0f`、`ota_0 VALID`。事后 BLE `hidState=0FA000DF`：加密、认证、绑定、订阅、桥接新鲜度和 HID 就绪均为真，无挂起，解锁面板已关闭。失败/取消位亦由成功清理调用设置，不能据此判断之前失败原因。面板关闭会清空阶段位，未保留首次失败现场。

最近 BLE 中断时间为 21:51:45（升级期间 GATT frame ACK 超时），早于本轮验收；没有证据将它与首次解锁失败关联。USB 中断包含启动核验替换串口连接和后续拔线时的取消，不能直接视作解锁故障。

未收集真实 PIN，未安装 Wi-Fi 解锁组件。后续 Windows 设置与 TAB5 音量布局优化独立于本次功能验收。

## English

User confirmed USB/BLE one-click unlock, success auto-dismiss and TAB5 unlock layout. The first BLE attempt displayed connection-not-ready; later attempts succeeded. Root cause remains unverified. Exact USB boot validation passed with the ELF above and ota_0 VALID. Post-event BLE state is ready/authenticated; modal state is reset on close and the shared cancellation flag also occurs during successful cleanup. No real PIN was collected and no Wi-Fi unlock component was installed.
