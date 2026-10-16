# AI-bot 0.6.7 / TAB5 0.2.205-ui

2026-10-16。本次发布 Windows／Apple Silicon Mac 桥接 0.6.7、TAB5 0.2.205-ui、ESP8266 0.5.3、标准书法 2026.10.15.1 和名画／书法高清完整图库 2026.10.16。标准名画沿用现有 2026.10.14.1，不重复上传；不集成浏览器高清音乐封面扩展。

## 变化与使用

- Windows 增加日程待办和离线事项同步，支持提前提醒、重复、语音输入及已结束事项；更新设备中心的管理菜单和 Wi-Fi 状态，保留系统监控报警清除规则。
- TAB5 205 调整提醒框大小和按钮比例，保留 204 的到期保留及满 16 条新增腾位、离线删除回执，203 的紧凑持久化／Wi-Fi 恢复，201 的有界旋转绘制及完整字库。
- ESP8266 0.5.3 通过固定设备身份恢复 USB 换口连接，并保留小屏使用的有界心跳数据。电脑端版本与两种固件分别编号，不因发布而要求用户全部重刷。
- 电脑图库提供按需高清解码、键盘／拼音浏览、循环翻页、全屏、缩放和独立阅读栏目。两套高清完整包各 366 件，包含对应标准展示图、资料和逐件来源。安装高清包无需先安装标准包，TAB5 仍使用适配设备的展示图。[高清包下载与安装](GALLERY-HD.md)
- 标准书法 2026.10.15.1 修正全部 1,901 张横屏页的作品名、作者和年代，补齐 32 件年代资料；1,901 张竖屏图片字节保留。只使用设备展示图的用户可单独更新标准书法。

## 发布附件

桥接提供 Windows 安装程序、Apple Silicon Mac ZIP 及 SHA-256；TAB5 提供首刷 ZIP、升级 ZIP 和对应源码 ZIP；ESP8266 提供固件及源码材料 ZIP。图库独立提供，不嵌入安装器或固件。标准名画保留原下载。

高清名画与书法统一以 128 MiB 分段提供，配套下载／断点续传、逐段与整包 SHA-256 校验及合并工具。上传恢复按段进行，已完成段不重传；下载支持 HTTP Range。合并后导入完整 ZIP，不能直接选择分段。[普通用户操作说明](GALLERY-HD.md)

## 验证与边界

Windows 和 Mac 发布工作流、ESP8266 构建及 TAB5 元数据校验通过；附件按服务器大小与 SHA-256 核验后公开。Windows 最终包完整自检、实际离线刷机工具调用（未接硬件）、两套完整高清包实际导入、全分页读取与原生浏览通过。51 个 TAB5／图库附件逐项登记；标准名画沿用旧包。分段恢复、HTTP 中断续传及完整真实包合并分别有验证记录。

205 的原生提醒预览、持久化／交互回归和构建不等于 205 已刷入实机。已有 201 四方向显示、USB 断接恢复，以及 204 到期保留和离线删除的用户确认继续保留；满额腾位、BLE 整包 OTA 和长期运行仍有独立实机边界。本次发布不操作现用设备。

Mac 为 Apple Silicon、临时签名、未公证，功能范围与 Windows 不同；实际 Mac 交互和长期运行不由 CI 编译替代。已安装的配对、用户资料和独立图库保留，下载或安装电脑软件不会自动刷写设备。

## English

This delivery publishes bridge 0.6.7, TAB5 0.2.205-ui, ESP8266 0.5.3, standard calligraphy 2026.10.15.1 and both optional complete HD collections 2026.10.16. Keep existing standard painting 2026.10.14.1. Do not integrate the browser music-artwork extension. Each HD package contains 366 works, matching device previews, guides and source records; a separate standard installation is unnecessary. Desktop HD originals are not sent to TAB5.

Both HD archives use 128 MiB parts with resumable upload boundaries and a Range-capable download/merge helper, verifying part and complete SHA-256 values. Import the reconstructed ZIP. Keep factory/upgrade TAB5 packages distinct and bind them to the same application and source snapshot. macOS is arm64, ad-hoc signed and not notarized. Native previews/builds are not 205 physical acceptance; retain the scoped 201/204 hardware evidence and open full-capacity/BLE/long-run boundaries. Preparation does not flash devices. Published attachment identity, actual complete imports and full-page native reads are verified before the download index advances.
