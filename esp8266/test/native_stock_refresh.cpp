#include "../src/StockPaintState.h"
#include <cassert>
#include <string>

int main() {
  StockPaintState<std::string,4> state;
  assert(state.begin(0,7,1));
  for(int row=0;row<4;row++)state.remember(row,"code","100","+1",1);
  assert(!state.begin(0,7,1));
  for(int row=0;row<4;row++) {
    assert(!state.codeChanged(row,"code"));
    assert(!state.valuesChanged(row,"100","+1",1));
  }
  // Changes to a quote on the other page are not supplied to this painter.
  assert(!state.begin(0,7,1));
  assert(state.valuesChanged(2,"101","+1",1));
  for(int row: {0,1,3})assert(!state.valuesChanged(row,"100","+1",1));
  state.remember(2,"code","101","+1",1);
  assert(!state.valuesChanged(2,"101","+1",1));
  assert(state.valuesChanged(2,"101","-1",-1));
  assert(state.codeChanged(2,"new-code"));
  assert(state.begin(1,7,1)); // Actual stock subpage turn.
  assert(state.begin(1,7,2)); // Changed display resource or page entry.
  assert(state.begin(1,8,2)); // A changed row count changes the footer.
}
