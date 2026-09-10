# Third-party notices

AI-bot source code is licensed under the MIT License. Dependencies remain under their respective licenses.

| Component | Use | License/source |
| --- | --- | --- |
| .NET and `System.IO.Ports` | Windows bridge runtime and serial transport | MIT, https://github.com/dotnet/runtime |
| Microsoft.Web.WebView2 1.0.4078.44 | Authorization browser control/loader | Package's Microsoft BSD-style LICENSE.txt; browser Runtime installed separately under its Microsoft terms, https://developer.microsoft.com/microsoft-edge/webview2/ |
| Microsoft.Windows.SDK.NET.Ref 10.0.19041.56 | Windows SDK .NET projection and WinRT runtime DLLs | Windows SDK terms, https://aka.ms/WinSDKLicenseURL; both net8.0 DLLs explicitly listed at https://learn.microsoft.com/en-us/legal/windows-sdk/redist#microsoftwindowssdknetref |
| ESP8266 Arduino core | Firmware framework | LGPL-2.1 and component-specific notices, https://github.com/esp8266/Arduino |
| TFT_eSPI | ST7789 display driver | FreeBSD/MIT/BSD component notices, https://github.com/Bodmer/TFT_eSPI |
| ArduinoJson | Firmware JSON parser | MIT, https://github.com/bblanchon/ArduinoJson |
| WiFiManager | ESP8266 captive Wi-Fi configuration portal | MIT, https://github.com/tzapu/WiFiManager |
| Open-Meteo API data | Weather, air quality, and geocoding | CC BY 4.0, https://open-meteo.com/en/license |
| Espressif NONOS SDK | Wi-Fi and ESP8266 runtime in Arduino core | Espressif MIT License with ESP8266-only condition; core tools/sdk/License |
| LittleFS | Device filesystem | BSD-3-Clause; core libraries/LittleFS/lib/littlefs/LICENSE.md |
| lwIP | Device IP stack | BSD notice in core tools/sdk/lwip2/include/lwip/init.h |
| umm_malloc, libb64, eboot, uzlib, BearSSL and other bundled core components | Core utilities; archive includes component sources/notices | Original per-component notices retained; inclusion in source materials does not imply every component is linked |
| GCC 10.3.0 runtime | Linked compiler support code | GPL-3.0 with GCC Runtime Library Exception 3.1; licenses/firmware-toolchain/ |
| newlib 4.0.0 | Embedded C runtime | Collection of permissive notices in licenses/firmware-toolchain/COPYING.NEWLIB |

Release archives must preserve the license texts delivered by package managers when their terms require redistribution with binary forms.

Windows packaging collects notices from actual restored NuGet packages, plus the
targeting pack excluded from ordinary NuGet library lists. The two SDK DLLs must
match the reviewed originals by SHA-256. Complete SDK terms and REDIST evidence
are bundled, not replaced by this table or MIT. See [distribution terms](docs/DISTRIBUTION_TERMS.md).

Firmware packages include application and actual core/library sources for modifying
and rebuilding the LGPL-linked work, component notices, build configuration and
hashes. Original component notices take precedence over this summary. Toolchain
executables are installed separately, not redistributed in the materials archive.
See [firmware materials](docs/FIRMWARE_PACKAGE.md).
