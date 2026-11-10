#pragma once
#include <stdint.h>

// Non-blocking brightness easing; the selected brightness remains unchanged.
class PageTransition {
 public:
  struct Result { uint8_t percent; bool hold; };
  static int target(int page, int mode, int subpage, bool fresh) {
    return fresh ? page*64 + mode + subpage*4096 : -1;
  }
  Result update(int target, uint32_t now) {
    if (!initialized_) { initialized_=true; presented_=target; return {100,false}; }
    if (phase_ == Idle && target != presented_) beginOut(now,100);
    if (phase_ == Out) {
      uint32_t age=now-start_;
      if (age >= outMs) {
        presented_=target; phase_=In; start_=now; current_=low; awaitingPaint_=true;
        return {low,false};
      }
      current_=quantize(startPercent_ - ((startPercent_-low)*age/outMs));
      return {current_,true};
    }
    if (phase_ == In) {
      const uint32_t age=now-start_;
      current_=age >= inMs ? 100 : quantize(low + ((100-low)*age/inMs));
      if (target != presented_) { beginOut(now,current_); return {current_,true}; }
      if (age >= inMs) { phase_=Idle; current_=100; }
      return {current_,false};
    }
    return {100,false};
  }
  // The page render can take longer than the fade. Start easing back only
  // after it actually finishes, not before the synchronous display writes.
  void markPainted(uint32_t now) { if (awaitingPaint_) { start_=now; awaitingPaint_=false; } }
 private:
  static constexpr uint32_t outMs=90, inMs=110;
  static constexpr uint8_t low=70;
  enum Phase { Idle, Out, In } phase_=Idle;
  bool initialized_=false, awaitingPaint_=false;
  int presented_=0;
  uint32_t start_=0;
  uint8_t current_=100, startPercent_=100;
  static uint8_t quantize(uint32_t v) { return static_cast<uint8_t>(((v+2)/5)*5); }
  void beginOut(uint32_t now,uint8_t from) { phase_=Out; start_=now; startPercent_=from; }
};
