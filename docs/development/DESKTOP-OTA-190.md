# Desktop TAB5 upgrade and transfer tests

The Windows firmware page offers Start Upgrade for TAB5 0.2.190-ui or later. Older devices require one installation initiated on the device. An authenticated request first saves drafts and checks device activity, then commits the selected offer and SHA-256. A start receipt and a reported version are separate from exact USB boot verification.

The boot verification action shares a row with its status. A successful result is bound to the paired device, version and ELF fingerprint; selecting a different image does not inherit the previous confirmation. Three live identity/uptime replies and the valid OTA partition check remain required.

The transfer tab offers Gallery/Card and Communication through one selector and Start/Stop row. Gallery results include validation and TF writes; communication results do not. Summary rates use successful samples only. Detailed phase timings remain available without adding firmware preflight or radio isolation controls to the page.

The two installation-guide PNGs were rendered from the project's actual Windows Forms using an isolated offline profile. They contain fixture connection/version state and no private pairing, network credentials or account data. They document layout, not hardware installation or performance acceptance. Their reviewed bytes are recorded in `licenses/materials.json`.

Local checks cover default/minimum windows at 100/150/200 percent scaling, actual encrypted HTTP/RPC and same-user IPC, rejected/expired/mismatched requests, device busy and draft persistence failures, native worker ownership, and the production OTA task. Physical desktop triggering and new transfer performance remain pending the first device installation.

# 电脑升级与传输测速

Windows 桥接可为 TAB5 0.2.190-ui 及以后版本发起升级。旧设备需先在设备端完成一次安装。配对鉴权请求先保存草稿、检查设备操作，再按本次固件和 SHA-256 确认开始。开始回执、版本回报与精确 USB 启动核验分别显示。

核验按钮与状态放在同一行。成功结果绑定设备、版本及 ELF；切换镜像不继承旧确认。保留三次身份／递增运行时间应答和有效 OTA 分区检查。

测速页只保留“图库写卡／通信传输”选择与开始／停止操作。前者包含校验和写卡，后者不写卡；速度汇总只使用成功样本。详细耗时可展开，该页不再显示固件预检和无线隔离操作。

安装说明中的两张 PNG 由本项目真实 Windows Forms 在隔离离线资料目录中生成。连接及版本为示例，不含私有配对、网络凭据或账户资料；用于说明布局，不作为安装或速度验收证据。对应文件的已审阅哈希登记在 `licenses/materials.json`。

本地验证覆盖常规／最小窗口、三种缩放、真实加密请求与本机通信、过期／不匹配请求、忙碌和草稿保存失败、原生任务所有权及实际 OTA 流程。电脑发起升级与新传输速度仍待首次设备安装后真机验收。
