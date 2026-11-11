cmake_minimum_required(VERSION 3.16)
include("${CMAKE_CURRENT_LIST_DIR}/../cmake/WindowsNinjaResponseFiles.cmake")
set(PROJECT_VER "0.2.319-ui")
option(TAB5_LOCAL_ART "Build daily art support; collections are installed separately" ON)
if(TAB5_LOCAL_VERSION)
    set(PROJECT_VER "${TAB5_LOCAL_VERSION}")
endif()
include($ENV{IDF_PATH}/tools/cmake/project.cmake)
project(aibot_tab5 VERSION 0.2.47)
# The pinned ESP-Hosted 1.4 constructor starts SDIO before app_main can
# configure Tab5's I/O-expander power rail. The application owns this startup:
# initialize the unmodified SDK once, after board power is ready. Its SDIO
# deinit is a TODO, so runtime deinit/reinit must not be used as a workaround.
idf_component_get_property(tab5_hosted_lib espressif__esp_hosted COMPONENT_LIB)
get_target_property(tab5_hosted_sources ${tab5_hosted_lib} SOURCES)
set(tab5_hosted_manual_sources ${tab5_hosted_sources})
list(FILTER tab5_hosted_manual_sources EXCLUDE REGEX "(^|/)esp_hosted_host_init\\.c$")
list(LENGTH tab5_hosted_sources tab5_hosted_before)
list(LENGTH tab5_hosted_manual_sources tab5_hosted_after)
math(EXPR tab5_hosted_removed "${tab5_hosted_before}-${tab5_hosted_after}")
if(NOT tab5_hosted_removed EQUAL 1)
    message(FATAL_ERROR "ESP-Hosted startup layout changed; review manual Tab5 radio initialization")
endif()
set_property(TARGET ${tab5_hosted_lib} PROPERTY SOURCES ${tab5_hosted_manual_sources})
# Use ESP32-P4 ROM memory/string operations for RGB565 row copies and clears.
# The CLIB memory backend is routed to PSRAM by main/ui_memory.c linker wrappers.
# Select the supported string backend without editing managed LVGL sources.
idf_component_get_property(tab5_lvgl_lib lvgl__lvgl COMPONENT_LIB)
target_compile_definitions(${tab5_lvgl_lib} PUBLIC LV_USE_STDLIB_STRING=LV_STDLIB_CLIB LV_USE_SNAPSHOT=1)
# Tab5 layers use RGB565/ARGB8888 and A8 masks. Indexed artwork is decoded
# to ARGB8888 by lv_bin_decoder; retain that decoder and all color backends.
# Override only these unused software formats, for LVGL and its consumers.
target_compile_definitions(${tab5_lvgl_lib} PUBLIC
    LV_DRAW_SW_SUPPORT_L8=0 LV_DRAW_SW_SUPPORT_AL88=0 LV_DRAW_SW_SUPPORT_I1=0)
