#pragma once
typedef struct {unsigned connection;} ble_keyboard_state;
void ble_keyboard_get(ble_keyboard_state *state);
