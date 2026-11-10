#pragma once
typedef int portMUX_TYPE;
#define portMUX_INITIALIZER_UNLOCKED 0
extern unsigned test_lock_depth, test_lock_errors;
#define portENTER_CRITICAL(lock) do { (void)(lock); if(test_lock_depth++) test_lock_errors++; } while(0)
#define portEXIT_CRITICAL(lock) do { (void)(lock); if(test_lock_depth!=1) test_lock_errors++; test_lock_depth--; } while(0)
