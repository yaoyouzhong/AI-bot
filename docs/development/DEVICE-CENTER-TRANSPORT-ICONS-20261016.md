# 设备中心连接图标与布局 — 2026-10-16

用户要求电脑端 USB、Wi-Fi、蓝牙与 TAB5 三个图标一致。读取独立 TAB5 工程 firmware/main/ui.c 的 link_icons / links_render，确认 LV_SYMBOL_USB、LV_SYMBOL_WIFI、LV_SYMBOL_BLUETOOTH 来自 LVGL 的 FontAwesome5-Solid+Brands+Regular.woff。

通过 scripts/extract_tab5_transport_icons.py 从该字体提取三个原始轮廓，生成 Tab5TransportGlyphs.cs（记录源 SHA-256）。不使用近似手绘图标。深色小图标底与 TAB5 背景一致，正常绿、等待/桥接未通黄、断开灰；旁边保留简短状态，不单靠颜色；鼠标悬停和无障碍名称保留完整通道与状态文本。OFL 和 Font Awesome 许可声明包含于 licenses/font-awesome 并嵌入桥接程序集。

连接区等宽三列、紧凑单行。功能入口仍为两列，间距调整使常用窗口完整显示八个入口。ESP8266 保留原有 COM / 状态文本行为。设备行为和固件未改变。

Release 0 警告 0 错误。生产 DeviceCenterStyleSelfTest 对 9 个页面状态（含 USB/Wi-Fi/蓝牙正常与离线、桥接未通、等待回报、ESP8266 中断/未找到 USB）在普通/最小窗口及 100/150/175/200% 缩放下验证；通过。实际原生窗口截图位于 artifacts/device-center-layout-20261016/tab5-icon-preview。

工具环境 status-once 被现有 AppProfileGuard 阻止，未绕过；候选尚未部署。安装入口 install-device-center-from-desktop.cmd 已准备，保留原高清索引和自动轮播设置，不刷新固件。
