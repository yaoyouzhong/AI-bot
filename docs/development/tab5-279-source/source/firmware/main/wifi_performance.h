#pragma once
#include <stdbool.h>
#include <stddef.h>
void wifi_performance_init(void);
void wifi_performance_bulk(bool active);
void wifi_performance_idle(void);
void wifi_performance_diagnostics(char *out,size_t size);
