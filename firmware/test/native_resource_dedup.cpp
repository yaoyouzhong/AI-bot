#include "../src/ResourceDedup.h"
#include <algorithm>
#include <cassert>
#include <cstring>
#include <vector>

struct Reader {
  std::vector<uint8_t> bytes;
  size_t position = 0;
  size_t limit = SIZE_MAX;
  int read(uint8_t* out, size_t size) {
    size = std::min({size, bytes.size() - position, limit});
    std::memcpy(out, bytes.data() + position, size);
    position += size;
    return static_cast<int>(size);
  }
};
int main() {
  std::vector<uint8_t> data(777);
  for (size_t i = 0; i < data.size(); ++i) data[i] = static_cast<uint8_t>(i);
  Reader a{data}, b{data}; int yields = 0;
  assert(compareResourceBytes(a,b,data.size(),[&] { ++yields; }) == ResourceComparison::Equal);
  assert(yields == 4);
  for (size_t offset : {size_t(0),size_t(256),size_t(776)}) {
    Reader x{data}, y{data}; y.bytes[offset] ^= 1;
    assert(compareResourceBytes(x,y,data.size(),[] {}) == ResourceComparison::Different);
  }
  Reader x{data}, shortRead{data}; shortRead.limit = 17;
  assert(compareResourceBytes(x,shortRead,data.size(),[] {}) == ResourceComparison::ReadError);
}
