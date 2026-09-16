# 屏保农历 / Lunar screensaver

时间、公历日期/星期、农历日期分三行显示，支持初一、二十、三十和闰月。整组显示保持缓慢移动，最底部不超过 216 像素，避开固件 219 像素处的 PC OFF 提示。

以屏保使用的 UTC 时间和偏移量确定公历日期，然后换算农历。固件本地计算，屏保模式中电脑离线仍可显示；不是缓存上一天的农历文本。普通离线页保持原样。

Windows 使用 .NET `ChineseLunisolarCalendar`；固件采用由同一公共 API 导出的年度日历事实（1901—2100 农历年），与本项目实现的逐月换算算法；macOS 使用系统 Chinese Calendar，按显示的公历日期在中国参考时区换算。日期超出 1901-02-19 至 2101-01-28 时隐藏农历行，不猜测。字段不传入通信协议，也不增加网络请求。

参考：[Microsoft 日历支持范围](https://learn.microsoft.com/en-us/dotnet/api/system.globalization.chineselunisolarcalendar)。`scripts/generate_lunar_calendar.ps1` 可重建固件表，不复制第三方日历源码；`LunarText.h` 是本项目独立编写的简化笔画，不分发系统字体。

验证：`scripts/test_lunar_calendar.ps1` 用本机 Visual C++ 构建固件头文件测试，并逐日对照 .NET API，覆盖 73,028 天及边界；Windows 公共自测覆盖春节、闰月、除夕、时区零点和移动边界。macOS 加入对应 XCTest，须在 Mac 上执行。构建/离线渲染不替代实体屏测试。

本功能包含在 v0.2.0；设备显示需要更新固件。实体设备验收仍待完成。

## English

The screensaver adds a third line for the lunar date, including leap months. Time, civil date/weekday and lunar date move together within the safe area above PC OFF. The displayed civil date comes from the existing epoch and UTC offset. Firmware calculates locally even when the PC disconnects in screensaver mode; the ordinary offline page is unchanged.

Windows uses the public .NET ChineseLunisolarCalendar API. Firmware uses generated calendar facts and an independently written converter; Mac uses the system Chinese calendar for the displayed civil date. Unsupported dates outside 1901-02-19 through 2101-01-28 omit the lunar line. No protocol or network changes are needed. Firmware glyphs are original geometric strokes, not redistributed font files.

Host C++ tests compare all 73,028 supported days with .NET, plus range and layout boundaries. Windows and Mac fixtures cover new year, leap month and local midnight. macOS runtime and hardware acceptance remain separate. Included in v0.2.0; device display requires updated firmware. Physical-device acceptance remains pending.
