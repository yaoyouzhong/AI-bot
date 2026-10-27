#pragma once

// Timing counters and sanitized RTOS task names only; no application payload.
// Values are observational snapshots, not a lock-consistent
// trace. Microsecond timestamps wrap after ~71 minutes; compare unsigned deltas.
void display_diagnostics_format(char *out, unsigned size);
