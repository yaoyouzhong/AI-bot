#pragma once
#include <stdbool.h>
typedef unsigned portMUX_TYPE;
#define portMUX_INITIALIZER_UNLOCKED 0u
void probe_test_enter(portMUX_TYPE *,bool);
void probe_test_exit(portMUX_TYPE *,bool);
#define portENTER_CRITICAL(lock) probe_test_enter(lock,false)
#define portEXIT_CRITICAL(lock) probe_test_exit(lock,false)
#define portENTER_CRITICAL_ISR(lock) probe_test_enter(lock,true)
#define portEXIT_CRITICAL_ISR(lock) probe_test_exit(lock,true)
