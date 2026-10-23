# TAB5 0.2.262-ui source overlay

在已归档的 TAB5 .257 完整工程上，按相同相对路径覆盖本目录的 `firmware/` 与 `tests/`。此处保存 .259–.262 的完整修改文件及新增测试夹具源码，包含照片加密分块、只读显示诊断、带宽保护和照片窗口内跳过纯诊断堆扫描。构建版本参数使用 `-LocalArt -LocalVersion 0.2.262-ui`。原工程及第三方许可保持适用。

`.262` 对应镜像与 ELF 身份见 `SOURCE.json`。OTA 与崩溃记录文件取自 .263 改动前留存的 .262 原件，未混入 .263/.264 后续诊断。用户已在此前确认 .262 添加照片成功且不再闪烁；本次仅归档源码，不重新测试、构建或刷机。

Copy `firmware/` and `tests/` over the previously archived .257 source, preserving paths. This full-file overlay contains the .259-.262 changes and required fixture sources. Build with local version 0.2.262-ui. Existing project and third-party licenses apply. No .263/.264 OTA diagnostic changes are included. Tests, builds and hardware operations were not rerun for this source-only commit.
