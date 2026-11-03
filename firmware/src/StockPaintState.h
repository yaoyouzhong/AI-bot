#pragma once
#include <stdint.h>

// Track only visible rows. A quote on another stock page must not repaint the
// current page. Text is supplied by the caller, so this works with Arduino
// String and a native std::string test without adding protocol state.
template <typename Text, int Rows>
class StockPaintState {
 public:
  bool begin(int page,int count,uint32_t generation) {
    bool full=!initialized_ || page!=page_ || count!=count_ || generation!=generation_;
    initialized_=true;page_=page;count_=count;generation_=generation;
    return full;
  }
  bool codeChanged(int row,const Text& code) const { return !valid_[row] || codes_[row]!=code; }
  bool valuesChanged(int row,const Text& price,const Text& percent,int trend) const {
    return !valid_[row] || prices_[row]!=price || percents_[row]!=percent || trends_[row]!=trend;
  }
  void remember(int row,const Text& code,const Text& price,const Text& percent,int trend) {
    codes_[row]=code;prices_[row]=price;percents_[row]=percent;trends_[row]=trend;valid_[row]=true;
  }
 private:
  bool initialized_=false,valid_[Rows]={};
  int page_=-1,count_=-1,trends_[Rows]={};
  uint32_t generation_=0;
  Text codes_[Rows],prices_[Rows],percents_[Rows];
};
