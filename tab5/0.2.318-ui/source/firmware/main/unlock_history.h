#pragma once
#include <stdatomic.h>
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
// Single UI writer, atomic cells and bounded seqlock readers. Numeric-only;
// never consulted by input admission. Old failures survive successful retries.
#define UNLOCK_HISTORY_COUNT 8
#define UNLOCK_HISTORY_WORDS 9
#define UNLOCK_HISTORY_SIZE 587
extern atomic_uint tab5_unlock_history[2+UNLOCK_HISTORY_COUNT*UNLOCK_HISTORY_WORDS];
extern atomic_uint tab5_unlock_history_received;
static inline void unlock_history_append(const unsigned words[UNLOCK_HISTORY_WORDS]) {
    atomic_fetch_add(&tab5_unlock_history[0],1u);
    unsigned count=atomic_load(&tab5_unlock_history[1]);
    unsigned slot=2+(count%UNLOCK_HISTORY_COUNT)*UNLOCK_HISTORY_WORDS;
    atomic_store(&tab5_unlock_history[slot],count+1u);
    for(unsigned i=1;i<UNLOCK_HISTORY_WORDS;i++)atomic_store(&tab5_unlock_history[slot+i],words[i]);
    atomic_store(&tab5_unlock_history[1],count+1u);
    atomic_fetch_add(&tab5_unlock_history[0],1u);
}
static inline unsigned unlock_history_snapshot(char out[UNLOCK_HISTORY_SIZE]) {
    for(int retry=0;retry<2;retry++) {
        unsigned before=atomic_load(&tab5_unlock_history[0]);if(before&1u)continue;
        unsigned count=atomic_load(&tab5_unlock_history[1]),n=count<UNLOCK_HISTORY_COUNT?count:UNLOCK_HISTORY_COUNT;
        size_t used=2;out[0]='1';out[1]=';';out[2]=0;
        for(unsigned j=count-n;j<count;j++) {
            if(j>count-n)out[used++]=';';
            for(unsigned i=0;i<UNLOCK_HISTORY_WORDS;i++)
                used+=(size_t)snprintf(out+used,UNLOCK_HISTORY_SIZE-used,"%08X",atomic_load(&tab5_unlock_history[2+(j%UNLOCK_HISTORY_COUNT)*UNLOCK_HISTORY_WORDS+i]));
        }
        if(before==atomic_load(&tab5_unlock_history[0]))return count;
    }
    out[0]=0;return 0;
}
static inline bool unlock_history_pending(void) {
    return atomic_load(&tab5_unlock_history[1])!=atomic_load(&tab5_unlock_history_received);
}
