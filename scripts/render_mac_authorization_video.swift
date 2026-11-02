import AppKit

// Original release-media renderer. Offscreen only; never reads app preferences.
_ = NSApplication.shared
NSApp.setActivationPolicy(.prohibited)
guard CommandLine.arguments.count == 3 else { fatalError("Usage: swift render_mac_authorization_video.swift INPUT_PNG_DIR OUTPUT_DIR") }
let input = URL(fileURLWithPath: CommandLine.arguments[1]), output = URL(fileURLWithPath: CommandLine.arguments[2])
try FileManager.default.createDirectory(at: output, withIntermediateDirectories: true)
let width = 1280, height = 720
func box(_ x: CGFloat, _ y: CGFloat, _ w: CGFloat, _ h: CGFloat) -> NSRect { NSRect(x:x,y:CGFloat(height)-y-h,width:w,height:h) }
func text(_ value:String,_ x:CGFloat,_ y:CGFloat,_ w:CGFloat,_ h:CGFloat,_ size:CGFloat,_ color:NSColor = .white,_ bold:Bool = false) {
    let paragraph = NSMutableParagraphStyle(); paragraph.lineSpacing = 7
    (value as NSString).draw(in:box(x,y,w,h),withAttributes:[.font:NSFont.systemFont(ofSize:size,weight:bold ? .semibold:.regular),.foregroundColor:color,.paragraphStyle:paragraph])
}
let blue = NSColor(srgbRed:0.40,green:0.73,blue:1,alpha:1)
let muted = NSColor(srgbRed:0.70,green:0.78,blue:0.88,alpha:1)
let scenes: [(String,String,String,String,String?)] = [
    ("先完成系统授权", "Start with macOS permissions", "可用功能默认开启。\n保留用户明确关闭的设置。\n系统权限仍由用户确认。", "Available features start enabled.\nExplicit opt-outs are preserved.\nmacOS permissions require user approval.", "direct-authorization-fresh-light.png"),
    ("核对当前应用的授权", "Check effective permission", "在辅助功能中允许 AI-bot Bridge。\n旧条目已勾选仍失效：重新添加。\n完成后回 TAB5 定位。", "Allow the current app in Accessibility.\nRe-add an ineffective old enabled entry.\nReturn to TAB5 and locate the chat.", "direct-authorization-upgrade-light.png"),
    ("一次入口，重启复用", "One credential item after migration", "本机统一钥匙串提示：一次。\n同一候选重启：没有新提示。\n旧资料首迁可能仍逐项批准。", "Local test: one consolidated Keychain prompt.\nNo new prompt after the same build restarted.\nLegacy-first migration may require per-item approval.", nil)
]
for (index, scene) in scenes.enumerated() {
    let image = NSBitmapImageRep(bitmapDataPlanes:nil,pixelsWide:width,pixelsHigh:height,bitsPerSample:8,samplesPerPixel:4,hasAlpha:true,isPlanar:false,colorSpaceName:.deviceRGB,bytesPerRow:0,bitsPerPixel:0)!
    NSGraphicsContext.saveGraphicsState(); NSGraphicsContext.current = NSGraphicsContext(bitmapImageRep:image)
    NSColor(srgbRed:0.035,green:0.065,blue:0.12,alpha:1).setFill(); NSRect(x:0,y:0,width:width,height:height).fill()
    text("AI-bot · macOS",64,38,650,40,22,blue,true)
    text(String(format:"%02d / 03",index+1),64,102,620,32,18,muted)
    text(scene.0,64,166,696,92,46,.white,true)
    text(scene.1,64,260,690,54,24,blue)
    text(scene.2,64,346,696,180,27)
    text(scene.3,64,537,696,120,18,muted)
    if let file = scene.4, let capture = NSImage(contentsOf:input.appendingPathComponent(file)) {
        let scale = min(416/capture.size.width,600/capture.size.height)
        let w = capture.size.width*scale, h = capture.size.height*scale
        capture.draw(in:box(800+(416-w)/2,52+(600-h)/2,w,h))
    } else {
        NSColor(srgbRed:0.09,green:0.15,blue:0.24,alpha:1).setFill()
        NSBezierPath(roundedRect:box(800,108,416,480),xRadius:22,yRadius:22).fill()
        text("本机验收 / Local test",830,137,360,38,21,blue,true)
        text("1 次",842,195,330,108,78,.white,true)
        text("启动钥匙串提示 / First start",834,307,354,42,19,muted)
        text("0 次",842,370,330,108,78,.white,true)
        text("同包重启提示 / Same build restart",824,484,374,42,18,muted)
        text("辅助功能仍需系统授权",832,548,368,35,19,blue)
    }
    text("原生界面演示 · 非系统授权录屏 / Native UI demo",64,680,1152,28,16,muted)
    NSGraphicsContext.restoreGraphicsState()
    try image.representation(using:.png,properties:[:])!.write(to:output.appendingPathComponent("scene-\(index+1).png"))
}
print("MAC_AUTHORIZATION_VIDEO_FRAMES_OK offscreen native rendering; no settings or device access")
