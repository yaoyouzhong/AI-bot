#undef NDEBUG
#include <assert.h>
#include <stdio.h>
#include <string.h>
#include "agenda_storage.h"

static agenda_document document,decoded;
static agenda_deletions deleted,removed;
static uint8_t bytes[AGENDA_STORAGE_MAX+1],mutated[AGENDA_STORAGE_MAX+1];
static void id(char out[33],unsigned n){snprintf(out,33,"%032x",n);}
int main(int argc,char **argv) {
    document.version=1;id(document.revision,0);
    size_t size=agenda_storage_encode(&document,&deleted,bytes,sizeof(bytes));
    assert(size==22&&agenda_storage_decode(bytes,size,&decoded,&removed));
    assert(!memcmp(&document,&decoded,sizeof(document))&&!memcmp(&deleted,&removed,sizeof(deleted)));
    document.count=deleted.count=AGENDA_LIMIT;
    for(unsigned n=0;n<AGENDA_LIMIT;n++) {
        agenda_item *item=&document.items[n];id(item->id,n+1);id(item->edit,100+n);if(n%2)id(item->base_edit,0);
        for(unsigned j=0;j<32;j++)memcpy(item->title+j*3,"程",3);
        item->due=4102444799LL;item->end=4102531199LL;item->done=item->due;item->snooze_occurrence=item->due;item->snooze_until=4105123200LL;item->alerted=4105123200LL;
        item->repeat=n%5;item->lead=60;item->dirty=7;
        agenda_deletion *d=&deleted.items[n];id(d->id,200+n);id(d->edit,300+n);if(n%2)id(d->base_edit,0);
    }
    assert(agenda_document_valid(&document)&&agenda_deletions_valid(&document,&deleted));
    size=agenda_storage_encode(&document,&deleted,bytes,sizeof(bytes));assert(size==AGENDA_STORAGE_MAX);
    assert(!agenda_storage_encode(&document,&deleted,mutated,size-1));
    assert(agenda_storage_decode(bytes,size,&decoded,&removed));
    assert(!memcmp(&document,&decoded,sizeof(document))&&!memcmp(&deleted,&removed,sizeof(deleted)));
    // A valid prefix must never be accepted as a complete atomic snapshot.
    for(size_t n=0;n<size;n++)assert(!agenda_storage_decode(bytes,n,&decoded,&removed));
    assert(!agenda_storage_decode(bytes,size+1,&decoded,&removed));
    memcpy(mutated,bytes,size);mutated[3]=2;assert(!agenda_storage_decode(mutated,size,&decoded,&removed));
    memcpy(mutated,bytes,size);mutated[4]=17;assert(!agenda_storage_decode(mutated,size,&decoded,&removed));
    memcpy(mutated,bytes,size);mutated[5]=17;assert(!agenda_storage_decode(mutated,size,&decoded,&removed));
    memcpy(mutated,bytes,size);mutated[22+32]=2;assert(!agenda_storage_decode(mutated,size,&decoded,&removed));
    memcpy(mutated,bytes,size);mutated[22+49]=0;assert(!agenda_storage_decode(mutated,size,&decoded,&removed));
    memcpy(mutated,bytes,size);mutated[22+50]=0;assert(!agenda_storage_decode(mutated,size,&decoded,&removed));
    // Optional output is synthetic test data for the real NVS allocator replay.
    if(argc==2){FILE *file=fopen(argv[1],"wb");assert(file&&fwrite(bytes,1,size,file)==size);fclose(file);}
    document.items[0].alerted=-1;assert(!agenda_storage_encode(&document,&deleted,bytes,sizeof(bytes)));
    puts("AGENDA_STORAGE_OK maximum items/tombstones, 96-byte Chinese names, empty/zero GUID distinction, time boundaries, every truncated prefix and malformed records");
}
