#pragma once
#include <stddef.h>
#define MALLOC_CAP_SPIRAM 1
#define MALLOC_CAP_8BIT 2
extern "C" void *heap_caps_malloc(size_t,int);
extern "C" void *software_test_alloc(size_t);
extern "C" void software_test_free(void *);
#ifdef TAB5_JPGE_TEST_ALLOCATOR
#define malloc software_test_alloc
#define free software_test_free
#endif
