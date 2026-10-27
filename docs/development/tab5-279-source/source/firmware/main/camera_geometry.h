#pragma once
#include <stdbool.h>
#include <stdint.h>
#include <stddef.h>

// Mounted BMI270: X+ normal, X- inverted; Y+ lens at top, Y- lens at bottom.
static inline bool camera_pose_valid(int pose) {return pose>=-1&&pose<=2;}
static inline bool camera_pose_portrait(int pose) {return pose==1||pose==-1;}
static inline unsigned camera_pose_width(int pose) {return camera_pose_portrait(pose)?720:1280;}
static inline unsigned camera_pose_height(int pose) {return camera_pose_portrait(pose)?1280:720;}
// Raw front-facing sensor -> upright photograph, clockwise quarter turns.
static inline size_t camera_pose_source(int pose,unsigned x,unsigned y) {
    if(pose==1)return (size_t)(719-x)*1280+y;
    if(pose==-1)return (size_t)x*1280+1279-y;
    if(pose==2)return (size_t)(719-y)*1280+1279-x;
    return (size_t)y*1280+x;
}
typedef struct {int width,height,shutter_x,shutter_y;} camera_geometry;
static inline camera_geometry camera_geometry_for(int pose) {
    if(pose==1)return (camera_geometry){720,1280,266,960};
    if(pose==-1)return (camera_geometry){720,1280,266,16};
    return (camera_geometry){1280,720,pose==2?1092:0,208};
}
