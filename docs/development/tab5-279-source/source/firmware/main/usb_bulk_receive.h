#pragma once
#include <stdbool.h>
#define USB_BULK_RECEIVE_BYTES 4096u
void usb_bulk_receive_select(bool enabled);
void usb_bulk_receive_close(void);
bool usb_bulk_receive_discard(void);
