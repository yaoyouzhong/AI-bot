#pragma once
#include <stddef.h>
#include <stdint.h>
#include <string.h>

enum class ResourceComparison { Equal, Different, ReadError };

// A failed read cannot authorize either deduplication or replacement.
template <typename Reader, typename Yield>
ResourceComparison compareResourceBytes(Reader& existing, Reader& candidate, size_t length, Yield yield) {
  uint8_t left[256], right[256];
  while (length) {
    const size_t count = length < sizeof(left) ? length : sizeof(left);
    if (existing.read(left, count) != static_cast<int>(count) ||
        candidate.read(right, count) != static_cast<int>(count)) return ResourceComparison::ReadError;
    if (memcmp(left, right, count) != 0) return ResourceComparison::Different;
    length -= count;
    yield();
  }
  return ResourceComparison::Equal;
}
