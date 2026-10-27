# 第三方来源

本工程自有代码适用根目录 `LICENSE` 的 MIT 条款；下列第三方代码、字体、词库与服务标识不被重新许可，保留原版权和条款。Own code is MIT licensed; third-party content retains its original terms.

`firmware/components/m5stack_tab5`、`esp_lcd_st7121`、`esp_lvgl_port` 来自 M5Stack 官方 M5Tab5-UserDemo：
https://github.com/m5stack/M5Tab5-UserDemo

固定来源 commit：`b4e356bc491ca070d54004718dad789c07d5fc93`。
保留组件内的 SPDX、Apache-2.0 等许可标识及官方根目录 MIT 许可 `firmware/components/M5Stack-LICENSE`。
本项目移除了 ST7121 CMake 对示例专用 `package_manager` 的版本生成调用，其余驱动来源与后续修改应在此记录。

2026-09-24：`esp_lvgl_port/src/lvgl9/esp_lvgl_port_disp.c` 的横屏旋转改用 LVGL CPU 软件旋转，移除该显示路径的 PPA 客户端及阻塞式旋转。现场 JTAG 读取确认界面任务停在 PPA 完成信号量等待；保留原显示坐标转换和传输完成回调。此修复需要重新构建、刷入并验证长时间运行，CPU 占用与帧率也需真机评估。

摄像头预览新增的 `esp_video` 0.7.0、`esp_cam_sensor` 0.7.1、`esp_sccb_intf` 0.0.4 与 `esp_ipa` 0.1.0 也取自上述 M5Stack 官方示例快照。它们的组件目录保留各自的 `LICENSE` 和来源元数据；`esp_ipa` 包含 ESP32-P4 预编译库，适用许可详见其目录。设备代码仅启用示例中的 SC202CS/MIPI CSI 配置，硬件模组批次仍待真机核对。
摄像头时钟 BSP 调用改为返回 LEDC 实际初始化错误，以便设备在摄像头不可用时显示失败，而不是继续取帧。

2026-10-02：本地修改 `esp_video/src/esp_video.c` 与 `esp_video_ioctl.c`，支持停止状态下的 `VIDIOC_REQBUFS(count=0)`：清空队列、释放帧缓冲及信号量，不再创建替代缓冲。用于 ISP 持有设备引用时显式回收相机池；保留正在采集时拒绝释放的保护及原许可。相机调用方在显示解绑、停止采集和解除映射后请求释放。

BSP 音频配置的四处旧 `ES7120_SEL_MIC*` 名称改为 esp_codec_dev 1.6.2 的 `ES7210_SEL_MIC*`，修复依赖升级后的编译错误。首阶段尚未启用或验收音频功能。

ESP-IDF v5.4.2（Apache-2.0）、LVGL 9.2.2（MIT）、ESP-Hosted 1.4.0、esp_wifi_remote 0.8.5 等构建依赖由官方组件管理器获取，具体版本以 `firmware/dependencies.lock` 为准。发布时收集全部实际依赖许可，不能只分发裸二进制。

2026-10-16：`scripts/apply-nvs-blob-safety.py` 对固定 ESP-IDF 5.4.2（源码 commit `f5c3654a1c2d2a01f7f67def7a0dc48e691f63c0`）的 NVS 做两处本地修正：`nvs_page.cpp` 最后一项重复检测按 `chunkIndex` 区分同键的不同分块；`nvs_storage.cpp` 写入失败时只清理此次版本的分块，并在垃圾回收后重新定位页面。后者参考 Espressif release/v5.4 的版本范围清理思路，但保持本项目固定 SDK 的接口。原 Apache-2.0 许可及存储格式不变。真实备份的 SDK 重放覆盖连续失败、垃圾回收、全部其他键保留及逐 Flash 操作断电，不等于真机断电验收。Official source: https://github.com/espressif/esp-idf/tree/release/v5.4/components/nvs_flash/src . The local patch preserves chunk identity and re-locates failed-version chunks after GC; it retains the upstream license and NVS format.

没有复制旧 ESP8266 项目的第三方宠物素材或图标。内置文字使用 LVGL 提供的字体。
# Noto Sans CJK font

`firmware/main/tab5_font_24.c` is a 24 px, 2 bpp subset of Noto Sans CJK SC Regular.
Copyright (c) 2014-2021 Adobe (http://www.adobe.com/). Licensed under SIL OFL 1.1;
the complete license is in `firmware/main/NotoSans-OFL.txt`.
Source: https://github.com/notofonts/noto-cjk/blob/main/Sans/OTF/SimplifiedChinese/NotoSansCJKsc-Regular.otf
Source SHA-256: `2C76254F6FC379FDDFCE0A7E84FB5385BB135D3E399294F6EEB6680D0365B74B`.
Generated with official `lv_font_conv` 1.5.3; see `scripts/generate-font.py`.

The reading subset additionally contains rare ideographs including (U+276C4, U+28811,
U+2C64E) from Plangothic P1 Regular V2.9.5795, Copyright (c) 2024 Fitzgerald P.
Köeingsegg, under SIL OFL 1.1. Upstream release:
https://github.com/Losketch/Plangothic-Project/releases/tag/V2.9.5795
Source `PlangothicP1-Regular.ttf` SHA-256:
`322a96735fd2ee13bb58337e338d891eda3c3a8231f866222e684c9a4b1b5833`.
The license is retained at `firmware/main/Plangothic-OFL.txt`. The generated
modified subset is named `tab5_font_24`; no original font name is used for it.

The subset also uses Noto Sans Regular for U+202F (narrow no-break space) and
Noto Sans Math Regular for U+21A4 (leftwards arrow from bar), Copyright
2015-2021 Google LLC (Noto Sans) and 2018 Google LLC (Noto Sans Math), under
SIL OFL 1.1. License:
`firmware/main/NotoSupplement-OFL.txt`. Sources are pinned to
notofonts/noto-fonts commit `ffebf8c1ee449e544955a7e813c54f9b73848eac`:

- https://github.com/notofonts/noto-fonts/blob/ffebf8c1ee449e544955a7e813c54f9b73848eac/hinted/ttf/NotoSans/NotoSans-Regular.ttf
  SHA-256: `b85c38ecea8a7cfb39c24e395a4007474fa5a4fc864f6ee33309eb4948d232d5`.
- https://github.com/notofonts/noto-fonts/blob/ffebf8c1ee449e544955a7e813c54f9b73848eac/hinted/ttf/NotoSansMath/NotoSansMath-Regular.ttf
  SHA-256: `80b61fd613d3519197e64fff6f7e71fdc7f3e6526440ea4115b554ef7fd59af7`.


30 px 中文字库 tab5_font_30.c 同样来自上述 Noto 字体，2 bpp；生成脚本为 scripts/generate-workbench-fonts.py。

# Montserrat digit fonts

## Annual saver Nunito subset

`tab5_annual_digits_144.c` uses Nunito at 144 px, 4 bpp: digits/space/hyphen at weight 900 and percent at weight 500. `tab5_annual_text_26.c` uses weight 800 for Latin glyphs at 26 px. Copyright 2014 The Nunito Project Authors (https://github.com/googlefonts/nunito), SIL OFL 1.1; full license: `firmware/main/Nunito-OFL.txt`.

Source: https://github.com/google/fonts/blob/8b0a1d0f5983c89bc2b93f1b5fb55f9e252744b5/ofl/nunito/Nunito%5Bwght%5D.ttf

Source SHA-256: `bb55a5ca5c2042335b3991af27c4d0705d0ef41cac6164ac737fd8f2a1e85207`. Rebuild with `scripts/generate-annual-font.py` using FontTools 4.62.0 and lv_font_conv 1.5.3. The font is rasterized at its display size, not enlarged from the clock glyphs.

The Chinese glyphs in `tab5_annual_text_26.c` use Resource Han Rounded CN Bold (weight 700), 26 px, 4 bpp. Source: https://github.com/CyanoHao/Resource-Han-Rounded/releases/tag/v0.990, `RHR-CN-0.990.7z`, `ResourceHanRoundedCN-Bold.ttf`. Source SHA-256: `f713907a21a10701cd68a7ce3e345ccdce46c789e1809d65ace54e095d7107c3`. Font copyright: 2018–2019 Cyano Hao; portions 2014, 2015, 2018 Adobe. Licensed under SIL OFL 1.1; upstream license retained at `firmware/main/ResourceHanRounded-OFL.txt`. The same generator creates this glyph subset, named for the TAB5 annual UI.

## Existing clock digits

tab5_digits_72.c / tab5_digits_96.c 为 LVGL 9.2.2 scripts/built_in_font/Montserrat-Medium.ttf 的数字子集，4 bpp。源 SHA-256：421F26B23E2BE6B98373D32ACD3CB2897B154D4BF0A77D26534CE476E4CBED53。
字体内嵌版权为 Copyright 2011 The Montserrat Project Authors (https://github.com/JulietaUla/Montserrat)，SIL OFL 1.1；完整许可见 firmware/main/Montserrat-OFL.txt。

SDK 显示诊断：scripts/apply-display-diagnostics.py 对 ESP-IDF 5.4.2 esp_lcd_panel_dpi.c 的 underrun 分支加入只读计数回调；保留原 Apache-2.0 许可和错误处理。

# 离线拼音词库与服务标识

`firmware/third_party/rime-pinyin-simp` 保存 Rime/Android 简体拼音词库、作者名单和 Apache-2.0 许可。固定来源版本、SHA-256 及生成规则见该目录的 `SOURCE.md`；生成的 `firmware/main/tab5_pinyin_dict.inc` 随源码保存。

服务标识的来源和使用边界见 `firmware/assets/brands/README.md`。这些标识属于各自权利人。

## Monthly calendar subset

The classical left cover uses LXGW WenKai Regular v1.522 under SIL OFL 1.1.
Copyright 2021–2026 LXGW and 2020 The Klee Project Authors. Upstream:
https://github.com/lxgw/LxgwWenKai/releases/tag/v1.522 (`LXGWWenKai-Regular.ttf`).
Source SHA-256: `39ad71264b588165b469e35e6afb162a378dacd1f95348160240ba9038ac3009`.
The TAB5 source retains the complete license at `firmware/main/CalendarWenKai-OFL.txt`
and the subset generator at `scripts/generate-calendar-font.py`.
The custom subsets are named `tab5_calendar_month_48`, `tab5_calendar_verse_24`
and `tab5_calendar_meta_18` (48/24/18 px, 2 bpp, losslessly compressed bitmaps).
Calendar glyphs and the 72/96/144 px numeral subsets use lossless bitmap compression.
Numeral outlines, grayscale and metrics are unchanged; general Chinese UI fonts remain uncompressed.
The poem body selects among three distinct OFL calligraphic faces by solar term:
LXGW WenKai, Ma Shan Zheng and Zhi Mang Xing. Each poem uses one face throughout;
only its required glyphs are included. Font/source coverage is checked before generation.
Ma Shan Zheng: Copyright 2018 The Ma Shan Zheng Project Authors;
Google Fonts commit `406197b91ff39a93061c2c2eeaee67ddf2ae1f0d`, font SHA-256
`6d2546bb189c732a8ca29af9e22457b152387d158aa459e4ac2ce1e51788b7fb`.
Zhi Mang Xing: Copyright 2018 The Zhi Mang Xing Project Authors;
Google Fonts commit `b12c22f97f4769802373d8de6a0f4115eabb9a24`, font SHA-256
`644e0cae9b40f0b10ab729a01bd32032e3973bac22be3dccae01bf6ae7fde969`.
The TAB5 source retains exact download URLs and hashes in
`firmware/assets/calendar-calligraphy-sources.json`, with complete upstream licenses
in `firmware/main/mashanzheng-OFL.txt` and `firmware/main/zhimangxing-OFL.txt`.
The 24 classical poem texts are public-domain originals; source links and variant notes
are recorded in the TAB5 source at `firmware/assets/calendar-poems.json`.
No modern translations or commentaries are embedded. Seasonal pairings are editorial
selections and do not imply that all poems were written for the named solar term.

`firmware/main/tab5_calendar_text_20.c` uses the existing pinned Noto Sans CJK SC Regular source at 20 px / 2 bpp. SHA-256: `2c76254f6fc379fddfce0a7e84fb5385bb135d3e399294f6eeb6680d0365b74b`; same Adobe copyright and SIL OFL 1.1 (`firmware/main/NotoSans-OFL.txt`). Rebuild with `scripts/generate-calendar-font.py`. The .124 calendar flower PNGs were generated for this project without external image references; original transparent sources, provenance and the LVGL encoder are retained in `firmware/assets/calendar-flowers/` and `scripts/generate-calendar-flowers.py`. Lunar conversion and official schedules reuse the existing calendar implementation.

## Weekly countdown medium subset

`firmware/main/tab5_countdown_24.c` uses Noto Sans CJK SC Medium for `0123456789dhm`, at 24 px / 2 bpp. Source file: `NotoSansCJKsc-Medium.otf`, SHA-256 `ca094f6b0001fb048ca39ddd797a0cdb0179e1e55c6561e111c49c3e6a61d7b7`; upstream Noto CJK Sans/OTF/SimplifiedChinese distribution. Same Adobe copyright and SIL OFL 1.1 as above (`firmware/main/NotoSans-OFL.txt`). Generated by `scripts/generate-countdown-font.py`; regular glyph advances, line height and baseline are retained. Chinese and spacing fall back to the existing regular font.

## Bosch BMI270 orientation sensor

TAB5 portrait art mode uses Bosch Sensortec BMI270 SensorAPI under BSD-3-Clause. Copyright (c) 2023 Bosch Sensortec GmbH. The unmodified driver subset and official maximum-FIFO configuration come from M5Stack M5Tab5-UserDemo commit b4e356bc491ca070d54004718dad789c07d5fc93, platforms/tab5/components/sensor_bmi270. The TAB5 firmware retains full license text and source provenance in firmware/components/bosch_bmi270/LICENSE and SOURCE.md. Only accelerometer measurements are enabled; no copied third-party application UI is included.

TAB5艺术屏保的方向检测使用Bosch官方BMI270驱动（BSD-3-Clause），完整许可与固定来源保留于独立TAB5工程上述路径。


## Date countdown scalable serif subset

`firmware/main/tab5_date_serif_data.c` is a renamed OpenType/CFF subset of
Noto Serif CJK SC Bold (SIL OFL 1.1). The complete upstream copyright and
license remain in `firmware/main/LICENSE-NotoSerifCJK.txt` and the font metadata.
Source: https://github.com/notofonts/noto-cjk/blob/main/Serif/OTF/SimplifiedChinese/NotoSerifCJKsc-Bold.otf
Source SHA-256: `8af07d4b6c2e82bcc72a30e066eaf295f11b9424f4aad2eaa9fe0e9c3b38fc73`.

Rebuild with `scripts/generate-date-saver-font.py` (fontTools subsetter).
The derivative is named Tab5 Date Serif. It contains 1,000 frequent characters
selected using the vendored Apache-2.0 Rime dictionary weights, event-name
vocabulary, ASCII and countdown labels. Other titles use the existing full UI
font as a whole line. The former bitmap subsets are superseded.

Rendering uses the existing LVGL 9.2.2 Tiny TTF module and its bundled
stb_truetype/stb_rect_pack sources (MIT or public-domain dual license; license
texts are retained in those upstream headers). No third-party library source
was copied or altered. File font loading is disabled; only the pinned embedded
font bytes are passed to the renderer.


## Camera software JPEG / 相机软件编码

`firmware/components/tab5_jpeg/vendor/jpge.cpp` 与 `jpge.h` 原样取自 Espressif
`esp32-camera` commit `f37e65608f709bd94846dee01d947f02ae07c0b0`：
https://github.com/espressif/esp32-camera/tree/f37e65608f709bd94846dee01d947f02ae07c0b0/conversions
文件保留 Rich Geldreich 的 Public domain 声明；同时附带上游根目录 Apache-2.0
`LICENSE`。`SOURCE.json` 登记固定 URL 与 SHA-256。本项目的 `tab5_jpeg.cpp`
仅提供有界输出、RGB565 逐行转换和让出执行，不复制其他相机驱动。

The two vendor files are unchanged from the pinned official Espressif source.
Original public-domain notices and the upstream Apache-2.0 root license are retained.
Source URLs and hashes are recorded in `vendor/SOURCE.json`. The local wrapper owns
bounded output and scanline RGB565 conversion; no upstream camera driver is imported.
