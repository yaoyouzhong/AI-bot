#ifndef TAB5_NATIVE_TIMING_H
#define TAB5_NATIVE_TIMING_H
#include <time.h>
typedef long long LONGLONG;
typedef struct { LONGLONG QuadPart; } LARGE_INTEGER;
static inline int QueryPerformanceFrequency(LARGE_INTEGER *value) { value->QuadPart=1000000000LL; return 1; }
static inline int QueryPerformanceCounter(LARGE_INTEGER *value) { struct timespec now; if(clock_gettime(CLOCK_MONOTONIC,&now))return 0; value->QuadPart=(LONGLONG)now.tv_sec*1000000000LL+now.tv_nsec; return 1; }
#endif
