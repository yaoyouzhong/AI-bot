set(tab5_sources "main.c" "rpc_link.c" "link_state.c" "device.c" "usb_link.c" "wifi_link.c" "ble_link.c" "codex_remote.c" "voice_remote.c" "ota_update.c" "camera_preview.c" "tab5_keyboard.c" "ui.c" "tab5_font_24.c" "tab5_font_30.c" "tab5_digits_72.c" "tab5_digits_96.c" "tab5_timer_digits_192.c" "assets.c" "asset_codec.c" "ui_prefs.c" "ui_reading.c")
list(APPEND tab5_sources "tab5_ime.c")
list(APPEND tab5_sources "tab5_annual_digits_144.c")
list(APPEND tab5_sources "tab5_annual_text_26.c")
list(APPEND tab5_sources "tab5_calendar_text_20.c")
list(APPEND tab5_sources "tab5_calendar_month_48.c")
list(APPEND tab5_sources "tab5_calendar_verse_24.c")
list(APPEND tab5_sources "tab5_calendar_meta_18.c")
list(APPEND tab5_sources "tab5_calendar_brush_24.c" "tab5_calendar_running_28.c")
list(APPEND tab5_sources "tab5_countdown_24.c")
list(APPEND tab5_sources "tab5_date_serif_data.c")
list(APPEND tab5_sources "voice_recovery.c")
list(APPEND tab5_sources "desktop_remote.c" "computer_remote.c" "unlock_remote.c")
list(APPEND tab5_sources "wifi_submission_test.c")
list(APPEND tab5_sources "reply_document.c")
list(APPEND tab5_sources "codex_read_ack.c")
list(APPEND tab5_sources "wifi_profiles.c")
list(APPEND tab5_sources "wireless_boot.c")
list(APPEND tab5_sources "photo_attachment.c" "sd_storage.c" "photo_transfer_crypto.c" "photo_display_probe.c")
# ESP-IDF discovers dependencies before loading sdkconfig. Validate the configured phase.
if(NOT CMAKE_BUILD_EARLY_EXPANSION AND NOT CONFIG_FATFS_LFN_HEAP AND NOT CONFIG_FATFS_LFN_STACK)
 message(FATAL_ERROR "TF storage requires FAT long filename support; set CONFIG_FATFS_LFN_HEAP=y in sdkconfig")
endif()
if(TAB5_LOCAL_ART)
 list(APPEND tab5_sources "gallery_remote.c" "gallery_buffer.c" "gallery_jpeg_cache.c" "gallery_store.c")
endif()
list(APPEND tab5_sources "art_orientation.c")
list(APPEND tab5_sources "display_diagnostics.c" "display_bandwidth.c")
list(APPEND tab5_sources "runtime_diagnostics.c")
list(APPEND tab5_sources "usb_bulk_receive.c")
list(APPEND tab5_sources "draw_diagnostics.c" "camera_compositor.c" "camera_viewport.c")
list(APPEND tab5_sources "draw_fast.c")
list(APPEND tab5_sources "draw_orthogonal.c")
list(APPEND tab5_sources "draw_ppa.c")
list(APPEND tab5_sources "draw_cache.c")
list(APPEND tab5_sources "ui_memory.c")
list(APPEND tab5_sources "crash_record.c")
list(APPEND tab5_sources "executable_guard.c")
list(APPEND tab5_sources "battery_status.c" "agenda.c" "agenda_core.c" "agenda_storage.c" "gallery_clock.c")
list(APPEND tab5_sources "display_settings.c" "ota_remote.c")
list(APPEND tab5_sources "transport_benchmark.c" "wifi_performance.c" "http_low_latency.c")
list(APPEND tab5_sources "wifi_probe.c")
list(APPEND tab5_sources "flash_benchmark.c")
option(TAB5_USB_HID "Enable temporary USB HID voice shortcut" ON)
if(TAB5_USB_HID)
 list(APPEND tab5_sources "usb_hid_probe.c")
endif()
idf_component_register(SRCS ${tab5_sources}
    EMBED_FILES "assets/reply_notice.pcm" "assets/health_notice.pcm"
    INCLUDE_DIRS "." REQUIRES tab5_jpeg m5stack_tab5 bosch_bmi270 esp_video esp_driver_jpeg esp_driver_ppa nvs_flash app_update esp_http_client esp-tls esp_timer esp_driver_i2c esp_driver_usb_serial_jtag json esp_partition spi_flash bootloader_support bt esp_hosted esp_wifi esp_netif mbedtls)
target_compile_definitions(${COMPONENT_LIB} PRIVATE TAB5_LOCAL_ART=$<BOOL:${TAB5_LOCAL_ART}>)
# Agenda is a low-frequency scheduler; keep it small without changing rendering/transport optimization.
set_source_files_properties(agenda.c agenda_core.c agenda_storage.c PROPERTIES COMPILE_OPTIONS "-Os")
# The agenda is the only roller user. Keep this new widget compact in the
# existing OTA slots; leave the common draw/transport paths at their current flags.
if(NOT CMAKE_BUILD_EARLY_EXPANSION)
 idf_component_get_property(agenda_lvgl_dir lvgl__lvgl COMPONENT_DIR)
 idf_component_get_property(agenda_lvgl_target lvgl__lvgl COMPONENT_LIB)
 set_property(SOURCE "${agenda_lvgl_dir}/src/widgets/roller/lv_roller.c"
     TARGET_DIRECTORY ${agenda_lvgl_target} APPEND PROPERTY COMPILE_OPTIONS "-Os")
endif()
target_link_options(${COMPONENT_LIB} INTERFACE
    "-Wl,--wrap=tu_edpt_stream_read_xfer" "-Wl,--wrap=tu_edpt_stream_read" "-Wl,--wrap=usbd_edpt_xfer"
    "-Wl,--undefined=__wrap_esp_ptr_executable" "-Wl,--wrap=esp_ptr_executable"
    "-Wl,--undefined=__wrap_esp_tls_plain_tcp_connect" "-Wl,--wrap=esp_tls_plain_tcp_connect"
    "-Wl,--undefined=__wrap_lv_malloc_core" "-Wl,--undefined=__wrap_lv_realloc_core" "-Wl,--undefined=__wrap_lv_free_core"
    "-Wl,--wrap=lv_malloc_core" "-Wl,--wrap=lv_realloc_core" "-Wl,--wrap=lv_free_core"
    "-Wl,--wrap=esp_panic_handler"
    "-Wl,--wrap=lv_draw_sw_image" "-Wl,--wrap=lv_draw_sw_fill"
    "-Wl,--wrap=lv_draw_sw_label" "-Wl,--wrap=lv_draw_sw_blend"
    "-Wl,--wrap=lv_draw_sw_transform"
    "-Wl,--wrap=lv_font_get_bitmap_fmt_txt" "-Wl,--wrap=lv_draw_sw_blend_color_to_rgb565"
    "-Wl,--wrap=lv_draw_sw_blend_image_to_rgb565")


if(TAB5_USB_HID)
 target_compile_definitions(${COMPONENT_LIB} PRIVATE TAB5_USB_HID=1)
 idf_component_optional_requires(PRIVATE espressif__esp_tinyusb)
endif()
if(TAB5_HID_PROBE)
 target_compile_definitions(${COMPONENT_LIB} PRIVATE TAB5_HID_PROBE=1)
endif()
