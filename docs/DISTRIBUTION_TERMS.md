# Distribution terms / 分发条款

AI-bot 自有源码及其原创资源适用根目录 `LICENSE` 的 MIT 条款，版权属于
姚有忠。第三方组件并未被重新许可为 MIT，完整条款位于包内 `licenses/`。

Windows 包中的 `Microsoft.Windows.SDK.NET.dll`、`WinRT.Runtime.dll` 原样来自
Microsoft.Windows.SDK.NET.Ref，适用随包的微软 Windows SDK 条款。使用或再次
分发这些组件须遵守该条款，包括其第 2 节的分发要求和限制。请先阅读
`licenses/Microsoft.Windows.SDK.NET.Ref-10.0.19041.56/sdk_license.rtf`；如果不能
遵守，不要使用或再次分发这些组件。下游分发者也须向接收者提供并要求遵守
相同的第三方条款。AI-bot 不授予超出组件原始许可的权利。

这些 DLL 仅随调用 WinRT 的 Windows 应用分发，保持字节及原有版权声明不变。
它们不适用 AI-bot 自有源码的 MIT 授权。其余 Windows 依赖适用各自随包条款。
.NET Desktop Runtime 和 WebView2 浏览器 Runtime 由用户从微软安装，不随此包提供。

固件中的第三方库适用各自许可，包括 ESP8266 Arduino core 的 LGPL-2.1-or-later、
各组件的 BSD/MIT 等声明，以及 Espressif NONOS SDK 的 ESP8266 硬件使用限制。
分发固件时须同时提供固件材料包中的许可、对应源码/构建材料和重建说明，
不得只转发 `.bin` 而丢弃这些材料。允许用户修改开源库、重建固件，并为调试
这类修改进行相关许可所允许的分析；本说明不缩减 LGPL 授予的权利。

个人导入的桌宠、页面标志、账户数据和配对信息不属于本分发包。

## English

AI-bot's own code and original assets are MIT-licensed, copyright 姚有忠. Third-party
components retain their own licenses; the package's `licenses/` directory contains
their terms. Using or redistributing the unmodified Windows SDK projection/runtime
DLLs requires compliance with the accompanying Windows SDK terms, including section
2. Read the original SDK RTF before use or redistribution; do not use or redistribute
those components if you cannot comply. Downstream distributors must provide and
require compliance with the same third-party terms. AI-bot grants no additional
rights to Microsoft components. They accompany this Windows application for WinRT
API access and are not relicensed under MIT. The .NET Desktop and WebView2 browser
runtimes are installed separately from Microsoft.

Firmware redistribution must retain its notices, corresponding source/build
materials and rebuilding instructions. The Arduino core remains LGPL-2.1-or-later;
other components retain their notices, including the Espressif SDK's ESP8266-only
condition. Users may modify open-source libraries and rebuild the firmware, including
analysis permitted by the applicable licenses for debugging such modifications.
Do not distribute the BIN alone. Private imported artwork and user data are excluded.
