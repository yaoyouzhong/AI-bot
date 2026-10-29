// Independently implemented adapter to the licensed upstream MMKV Core.
#import "MacDoubaoStore.h"
#include <MMKVCore/MMKV.h>
using namespace mmkv;
@implementation AIBDoubaoStore
- (instancetype)initWithRoot:(NSString *)root {
 self=[super init];if(!self)return nil;
 std::string path(root.fileSystemRepresentation);
 MMKV::initializeMMKV(path,MMKVLogNone);
 _store=MMKV::mmkvWithID("com.apple.xpc.activity",MMKV_MULTI_PROCESS,nullptr,&path);
 return _store ? self : nil;
}
- (BOOL)tryLock {return static_cast<MMKV *>(_store)->try_lock();}
- (void)unlock {static_cast<MMKV *>(_store)->unlock();}
- (BOOL)writeSettings:(NSDictionary *)settings {return static_cast<MMKV *>(_store)->set((NSObject<NSCoding> *)settings,@"d");}
- (void)sync {static_cast<MMKV *>(_store)->sync(MMKV_SYNC);}
- (void)close {if(_store){static_cast<MMKV *>(_store)->close();_store=nullptr;}}
@end
