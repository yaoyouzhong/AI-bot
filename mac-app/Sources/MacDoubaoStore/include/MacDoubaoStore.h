#import <Foundation/Foundation.h>
NS_ASSUME_NONNULL_BEGIN
@interface AIBDoubaoStore:NSObject { void *_store; }
- (nullable instancetype)initWithRoot:(NSString *)root;
- (BOOL)tryLock;
- (void)unlock;
- (BOOL)writeSettings:(NSDictionary *)settings;
- (void)sync;
- (void)close;
@end
NS_ASSUME_NONNULL_END
