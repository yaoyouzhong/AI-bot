# TAB5 macOS 离屏预览

此辅助工程用于在 macOS 上编译 TAB5 0.2.312 已验证源码中的真实 `tests/ui-preview/preview.c` 和 LVGL 软件渲染器。它不安装桥接、不连接设备，也不构建正式固件。`native_timing.h` 只把 Windows 测时接口映射到单调时钟，`native_pinyin.c` 使用系统 zlib 实现测试用解压接口；显示源码和测试控件仍取自指定工程。

先按 `docs/development/tab5-312-source/README.md` 验证工程及依赖，确保 `firmware/managed_components` 包含清单中的固定依赖，`.tools/esp-idf` 指向对应 SDK。下面的示例在隔离构建和输出目录中运行 Claude 萌宠相关场景：

```sh
cmake -S tools/tab5-preview-macos -B /tmp/aibot-tab5-preview -G Ninja \
  -DTAB5_SOURCE=/absolute/path/to/verified/source -DCMAKE_BUILD_TYPE=Release
cmake --build /tmp/aibot-tab5-preview --target ui-preview --parallel 2
mkdir -p /tmp/aibot-tab5-preview-images
cd /tmp/aibot-tab5-preview-images
TAB5_CLAUDE_COMPANION_ONLY=1 /tmp/aibot-tab5-preview/ui-preview
```

输出是离屏 PPM 和测试日志；需人工检查文字、截断、对齐与留白。它不能替代 Windows 原生测试、固件构建、烧录或真机验收。不要将会话真实名称、设备凭据或其他个人数据作为预览夹具。
