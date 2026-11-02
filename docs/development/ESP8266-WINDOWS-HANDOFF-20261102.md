# ESP8266 股票字形与切页交接（2026-11-02）

## 范围与源码

Mac 工作区到 Windows 工作区的开发交接；基于 `657f22e76f624b3969e5fc2ffc8b95c0c2111525` 的未提交工作区，未推送。Windows 本地未提交内容不可读取，接收方应先检查并保留其现有修改。DeepSeek、GLM 与天气间距已由 Windows 对齐，不属于本次交接。

## 功能和兼容性

- ESP8266 切页由 `firmware/src/PageTransition.h` 与 `main.cpp` 实现非阻塞的轻微调光：90 ms 降至用户亮度的 70%，完成绘制后 110 ms 恢复；股票列表内部翻页也使用同一过渡。用户确认股票列表翻页及其他切页的实机效果可接受。
- 股票代码使用 `StockCodeFont.h`、`StockCodeGlyphs.h` 的可核验来源 Roboto Mono 子集；常见最多 7 字符采用 8×16 像素格，较长代码压入原 56 像素区域并截断。生成器、版权和 OFL 见 `tools/generate_stock_code_font.py`、`licenses/roboto-mono/OFL.txt` 及 `THIRD_PARTY_NOTICES.md`。用户否定了先前偏窄版本，已确认加宽候选实机观感可接受。
- 资源去重和背光初始化见 `ResourceDedup.h`、`main.cpp`；桥接传输不得反复改写相同资源或亮度。沿用 `@AIBOT` 协议和现有字段，不要求 Windows 重复模拟设备端过渡，不改 TAB5 设置。

## 验证和剩余事项

PlatformIO 构建、字形／过渡／资源去重原生回归及背光所有权检查通过。2026-11-02 Mac 写入 ESP8266 应用镜像 `410f2d04531a205117f01aa3dbdbb0dd011f8a92c9c51abd60e54323a9f96c82`，独立验证通过；恢复自动循环，原六页及 15 秒间隔保留，桥接重新连接。用户确认加宽字形和切页效果；最新候选的 USB 重插观感尚未单独复测。Windows 需独立执行其 Release 构建、`--status-once`、桥接链路和真实设备验收，不能用 Mac 测试代替。交接不授权推送、发布或额外刷机。
