#pragma once
// The CDC application and TinyUSB must not execute endpoint/FIFO operations
// concurrently. TinyUSB clears OUT busy before copying its DMA buffer to the
// FIFO; a reader on another core can rearm that buffer during the copy.
// The higher-priority stack finishes its nonblocking RX callback first.
enum {TAB5_USB_IO_CORE=1,TAB5_USB_STACK_PRIORITY=8,TAB5_USB_RECEIVER_PRIORITY=6};
