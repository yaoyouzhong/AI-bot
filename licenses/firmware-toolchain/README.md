# Compiler runtime notices

The reviewed PlatformIO `toolchain-xtensa/2.100300.220621` uses GCC 10.3.0 and
newlib 4.0.0 (the installed `_newlib_version.h` confirms 4.0.0). The host toolchain
is not bundled. Runtime code linked into the firmware retains these upstream terms:

- GCC license: https://raw.githubusercontent.com/gcc-mirror/gcc/releases/gcc-10.3.0/COPYING3
- GCC runtime exception: https://raw.githubusercontent.com/gcc-mirror/gcc/releases/gcc-10.3.0/COPYING.RUNTIME
- newlib notices: https://raw.githubusercontent.com/mirror/newlib-cygwin/newlib-4.0.0/COPYING.NEWLIB
- Toolchain source: https://github.com/earlephilhower/esp-quick-toolchain/tree/3.1.0-gcc10.3
  (tag object `8903880f46a97e6b755fa98a4ae6c34483129078`).

These files are retained verbatim and hashed in `licenses/materials.json`.
They are not the license of the entire firmware or the host compiler executable.
