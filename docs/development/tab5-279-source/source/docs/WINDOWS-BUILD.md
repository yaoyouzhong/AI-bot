# Windows firmware builds

Use `scripts/build.ps1`. The process-local resource scope uses at most eight distinct physical cores, including at most two performance cores on hybrid CPUs, with BelowNormal priority. It respects inherited affinity and restores the caller on success or failure. On the i7-12700H this selects two performance and six efficiency cores, leaving four performance and two efficiency cores for other work. Firmware compiler/linker jobs share a Ninja pool sized to this budget; the native preview uses the same parallel-job limit.

## Compiler command length

ESP-IDF can add enough include directories to make a compiler command exceed the Windows process command-line limit. Previously, manually moving arguments in generated `firmware/build/CMakeFiles/rules.ninja` only repaired that build directory: CMake regeneration discarded the repair.

The firmware now includes `cmake/WindowsNinjaResponseFiles.cmake` before ESP-IDF project configuration. On Windows Ninja generators it enables `CMAKE_NINJA_FORCE_RESPONSE_FILE`, so CMake itself generates compiler response files containing definitions, includes and flags. This applies to fresh build directories and subsequent regeneration, including direct IDF configuration; Linux and macOS retain their existing behavior. No global environment, toolchain installation, firmware feature or optimization setting changes.

Do not patch `rules.ninja` or shorten/remove include paths to recover this failure. The policy is tracked source and ships in the candidate source archive. CMake behavior is verified against the pinned [3.30.2 generator implementation](https://github.com/Kitware/CMake/blob/v3.30.2/Source/cmNinjaTargetGenerator.cxx).

## Regression coverage

`tests/windows-response-files-test.py` uses the same production policy with the pinned CMake, Ninja and RISC-V C/C++ compilers. It creates an explicitly new output directory, compiles commands longer than 32,767 characters, checks macros and a header in a path containing spaces, then regenerates CMake from a fresh cache and compiles both targets again. Run it inside the `Enter-Tab5BuildResourceScope` / `Exit-Tab5BuildResourceScope` scope, passing `--cmake`, `--ninja`, `--cc`, `--cxx` and `--output` (see `--help`).

The firmware must also pass `scripts/build.ps1` after configuration regenerates its own rules. A generated-file workaround is not accepted as completion evidence.

## 中文说明

Windows 的长编译命令由受版本控制的 CMake 配置自动转入响应文件，完整保留宏、头文件路径和编译选项。新目录构建和重新配置均生效，无需手工修改生成的 Ninja 文件。按用户授权放宽为最多八个独立物理核心，混合架构最多使用两个性能核心，并行编译数与核心预算一致；保留低优先级，不改系统配置或全局环境。回归测试覆盖超过 32,767 字符的真实 C/C++ 编译、含空格路径、宏参数和重新生成后的再次编译，并另做实际固件构建。`tests/build-resources-test.ps1` 另验证核心选择、继承限制、子进程及失败后的恢复。

Changing compiler command generation triggers a one-time rebuild of affected objects. Ordinary subsequent source edits continue to use Ninja's incremental dependency checks; do not delete the build directory between builds.
