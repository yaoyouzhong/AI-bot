#include "../src/PageTransition.h"
#include <cassert>
int main() {
  const int firstStock = PageTransition::target(2,3,0,true);
  const int nextStock = PageTransition::target(2,3,1,true);
  assert(firstStock != nextStock);
  assert(PageTransition::target(2,3,1,false) == -1);
  PageTransition pagination;
  assert(!pagination.update(firstStock,0).hold);
  assert(pagination.update(nextStock,1).hold);
  assert(!pagination.update(nextStock,91).hold);
  PageTransition t;
  auto r=t.update(1,100); assert(!r.hold && r.percent==100);
  r=t.update(2,110); assert(r.hold && r.percent==100);
  r=t.update(2,155); assert(r.hold && r.percent==85);
  r=t.update(2,200); assert(!r.hold && r.percent==70);
  // Even a long synchronous frame must not skip the fade back up.
  t.markPainted(700);
  r=t.update(2,755); assert(!r.hold && r.percent==85);
  r=t.update(2,810); assert(!r.hold && r.percent==100);
  r=t.update(3,820); assert(r.hold && r.percent==100);
  r=t.update(4,910); assert(!r.hold && r.percent==70);
  t.markPainted(930);
  r=t.update(5,940); assert(r.hold && r.percent>=70 && r.percent<=100);
  r=t.update(5,1030); assert(!r.hold && r.percent==70);
  t.markPainted(1040); r=t.update(5,1150); assert(!r.hold && r.percent==100);
  PageTransition wrapped;
  wrapped.update(1,0xfffffff0U); wrapped.update(2,0xfffffff5U);
  r=wrapped.update(2,0x0000004fU); assert(!r.hold && r.percent==70);
}
