#undef NDEBUG
#include <assert.h>
#include <stdio.h>
#include "../../firmware/main/ui_startup_home.h"

int main(void) {
    int selections[]={-1,0,4,7},intervals[]={10,15,30,60};
    for(unsigned i=0;i<4;i++)for(unsigned j=0;j<4;j++) {
        ui_startup_home state=ui_startup_home_begin();
        int selected=selections[i],interval=intervals[j],resume;
        int64_t last=1000;
        assert(ui_startup_home_tick(&state,0,selected,interval,1001,last,&resume)&&resume==-1);
        ui_startup_home_snapshot(&state,0,4000,&last);assert(last==4000);
        // Later periodic frames must not extend the startup hold forever.
        ui_startup_home_snapshot(&state,0,5000,&last);assert(last==4000);
        assert(ui_startup_home_tick(&state,0,selected,interval,4000+interval*1000-1,last,&resume));
        assert(!ui_startup_home_tick(&state,0,selected,interval,4000+interval*1000,last,&resume)&&resume==selected);
        assert(!ui_startup_home_tick(&state,0,selected,interval,5000+interval*1000,last,&resume)&&resume==-1);
        assert(selected==selections[i]&&interval==intervals[j]);
    }
    ui_startup_home manual=ui_startup_home_begin();int resume;int64_t last=2000;
    assert(!ui_startup_home_tick(&manual,9,-1,15,2100,last,&resume));
    ui_startup_home_snapshot(&manual,9,3000,&last);assert(last==2000);
    assert(!ui_startup_home_tick(&manual,0,-1,15,3100,last,&resume));
    // An offline restart still resumes the saved policy after one interval.
    ui_startup_home offline=ui_startup_home_begin();
    assert(!ui_startup_home_tick(&offline,0,4,15,17000,2000,&resume)&&resume==4);
    puts("UI_STARTUP_HOME_OK overview_interval_first_frame_anchor_periodic_frame_no_extension_manual_navigation_offline_saved_mode_resume");
}
