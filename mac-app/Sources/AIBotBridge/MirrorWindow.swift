import AppKit

struct MacDisplayPolicy: Codable {
    var selectedMode:String
    var cycleEnabled:Bool
    var intervalSeconds:Int
    var pages:[String]
    var cycleStartedAt:Int64? = nil
    static var cycleAnchor=Int64(Date().timeIntervalSince1970)
    static let modes=["claude","codex","dual","weather","stocks","system","music","pet"]
    static func load(selected:String?=nil)->MacDisplayPolicy {
        let defaults=UserDefaults.standard
        let pages=(defaults.stringArray(forKey:"display_cycle_pages") ?? ["codex","claude","weather","stocks"]).filter{modes.contains($0)}
        let interval=defaults.integer(forKey:"display_cycle_interval_seconds")
        return .init(selectedMode:selected ?? defaults.string(forKey:"display_mode") ?? "auto",cycleEnabled:defaults.object(forKey:"display_cycle_enabled")==nil || defaults.bool(forKey:"display_cycle_enabled"),intervalSeconds:[10,15,30,60].contains(interval) ? interval:15,pages:pages.isEmpty ? ["codex"]:pages,cycleStartedAt:cycleAnchor)
    }
    func resolve(_ s:MacStatusSnapshot)->String {
        if selectedMode == "screensaver" {return selectedMode}
        if s.domesticActivity?.needsInput == true {return "activity"}
        if s.claude.needsInput && s.codex.needsInput, let app=s.followApp,["claude","codex"].contains(app) {return app}
        if s.codex.needsInput {return "codex"};if s.claude.needsInput {return "claude"};if s.codex.completionActive {return "codex"}
        if selectedMode != "auto" {return selectedMode}
        if cycleEnabled && !pages.isEmpty {return pages[Int(max(0,s.epochUtc-(cycleStartedAt ?? 0))/Int64(max(1,intervalSeconds)) % Int64(pages.count))]}
        if s.music?.playing==true {return "music"}
        if s.domesticActivity?.state == "working" {return "activity"}
        if let app=s.followApp,["claude","codex"].contains(app) {return app}
        let codexWorking=s.codex.state=="working",claudeWorking=s.claude.state=="working"
        if codexWorking != claudeWorking {return codexWorking ? "codex":"claude"}
        return max(0,s.epochUtc-(cycleStartedAt ?? 0))/Int64(codexWorking ? 2:6)%2==0 ? "claude":"codex"
    }
}

final class MacMirrorView:NSView {
    var capture:(()->MacStatusSnapshot)?
    var resources:(()->[MacResourcePayload])?
    override var isFlipped:Bool {true}
    private var history:[(Double,Double)]=[]
    private var lastMetricDate:Date?
    override func draw(_ dirtyRect:NSRect) {
        NSColor.black.setFill();bounds.fill()
        guard let s=capture?() else {return}
        let side=min(bounds.width,bounds.height)
        NSGraphicsContext.saveGraphicsState();defer {NSGraphicsContext.restoreGraphicsState()}
        let transform=NSAffineTransform();transform.translateX(by:(bounds.width-side)/2,yBy:(bounds.height-side)/2);transform.concat()
        let scale=NSAffineTransform();scale.scale(by:side/240);scale.concat();NSGraphicsContext.current?.imageInterpolation = .none
        let mode=(s.displayPolicy ?? MacDisplayPolicy.load()).resolve(s)
        switch mode {
        case "claude","codex","dual","quotas":
            if mode=="dual" || mode=="quotas" {quota(s.quotas?.claude,label:"CLAUDE",top:30);quota(s.quotas?.codex,label:"CODEX",top:132)}
            else {
                let claude=mode=="claude",q=claude ? s.quotas?.claude:s.quotas?.codex
                quotaRing(claude ? q?.primaryPercent ?? 0 : q?.primaryPercent ?? q?.weeklyPercent ?? 0)
                if let logo=resources?().first(where:{$0.kind == (claude ? .claudeLogo:.codexLogo)}),let image=MacPetAnimation.image(logo.data,width:40,height:40) {image.draw(in:NSRect(x:14,y:18,width:40,height:40))}
                else {text(claude ? "CLAUDE":"CODEX",14,31,40,14,8,claude ? .orange:.cyan)}
                pet(mode,centerY:120,animate:(claude ? s.claude.state:s.codex.state)=="working" || !claude && s.codex.completionActive)
                let weeklyOnly = !claude && q?.primaryPercent == nil && q?.weeklyPercent != nil
                centered(weeklyOnly ? "Weekly \(percent(q?.weeklyPercent))":"5h \(percent(q?.primaryPercent))",NSRect(x:0,y:188,width:240,height:18),.monospacedSystemFont(ofSize:13,weight:.semibold),.white)
                if !weeklyOnly {centered("Weekly \(percent(q?.weeklyPercent))",NSRect(x:0,y:206,width:240,height:18),.monospacedSystemFont(ofSize:13,weight:.semibold),.white)}
            }
        case "weather":
            guard let w=s.weather else {text("等待天气数据",20,100,210,35,18,.gray);return}
            text(w.city,14,1,122,26,18,.white);text(w.condition,184,9,54,32,14,.orange)
            text(String(format:"L %.0fC",w.low),14,34,64,22,14,.cyan);text(String(format:"H %.0fC",w.high),78,34,58,22,14,.orange)
            text(w.airQualityIndex.map{"AQI \($0)"} ?? "",136,12,46,30,10,.lightGray)
            let numbers=Array(s.time.filter{$0.isNumber}).compactMap{$0.wholeNumberValue}
            if numbers.count==6 {for i in 0..<4 {digit(numbers[i],CGFloat([16,50,94,128][i]),57,30,48,i<2 ? .white:.orange)};digit(numbers[4],174,75,16,28,.lightGray);digit(numbers[5],194,75,16,28,.lightGray)}
            let date=Date(timeIntervalSince1970:Double(s.epochUtc));let f=DateFormatter();f.dateFormat="M月d日 EEEE";f.locale=Locale(identifier:"zh_CN");text(f.string(from:date),14,118,214,28,18,.white)
            for row in 0..<2 {
                let y=CGFloat(row==0 ? 162:199),color:NSColor=row==0 ? .red:.green
                color.setFill();NSBezierPath(ovalIn:NSRect(x:14,y:y+9,width:9,height:9)).fill();text(row==0 ? "TEMP":"HUMID",30,y,61,13,9,.lightGray)
                NSColor.darkGray.setFill();NSRect(x:30,y:y+15,width:60,height:5).fill();color.setFill();NSRect(x:30,y:y+15,width:60*min(1,max(0,row==0 ? (w.temperature+10)/40:Double(w.humidity)/100)),height:5).fill()
                text(row==0 ? String(format:"%.0fC",w.temperature):"\(w.humidity)%",94,y,54,29,22,.white)
            }
        case "stocks":
            let quotes=s.stocks?.quotes ?? [];let pages=max(1,(quotes.count+3)/4),page=Int(s.epochUtc/5)%pages
            if quotes.isEmpty {centered("未配置自选股\n右键菜单 → 设置自选股…",NSRect(x:0,y:104,width:240,height:40),.systemFont(ofSize:11),.gray)}
            for (index,q) in quotes.dropFirst(page*4).prefix(4).enumerated() {
                let y=CGFloat(10+index*54);text(q.code+(q.name.isEmpty ? "":"  "+q.name),14,y,212,15,10,.gray)
                (q.price as NSString).draw(at:NSPoint(x:14,y:y+15),withAttributes:[.font:NSFont.monospacedSystemFont(ofSize:17,weight:.bold),.foregroundColor:NSColor.white])
                let alignment=NSMutableParagraphStyle();alignment.alignment = .right
                (q.changePercent as NSString).draw(in:NSRect(x:120,y:y+15,width:106,height:22),withAttributes:[.font:NSFont.monospacedSystemFont(ofSize:17,weight:.bold),.foregroundColor:q.trend>0 ? NSColor(calibratedRed:1,green:0.23,blue:0.19,alpha:1):q.trend<0 ? NSColor(calibratedRed:0,green:0.85,blue:0.2,alpha:1):NSColor.lightGray,.paragraphStyle:alignment])
            };if !quotes.isEmpty {centered("STOCKS",NSRect(x:0,y:224,width:240,height:12),.monospacedSystemFont(ofSize:8,weight:.medium),.gray)}
        case "system":
            guard let m=s.systemMetrics else {return}
            if lastMetricDate != m.updatedAt {history.append((Double(m.downloadBytesPerSecond),Double(m.uploadBytesPerSecond)));history=Array(history.suffix(224));lastMetricDate=m.updatedAt}
            text("DOWN",14,8,80,15,8,.gray);text("UP",134,8,80,15,8,.gray)
            text(rate(m.downloadBytesPerSecond),12,19,118,28,19,.green);text(rate(m.uploadBytesPerSecond),132,19,104,28,19,.yellow)
            let maximum=max(10240,(history.map{max($0.0,$0.1)}.max() ?? 0)*1.15)
            NSColor(white:0.16,alpha:1).setFill();for quarter in 1...3{NSRect(x:8,y:60+CGFloat(quarter*32),width:224,height:1).fill()}
            let samples=Array(repeating:(0.0,0.0),count:max(0,224-history.count))+history
            for channel in 0...1 {
                let line=NSBezierPath()
                for i in 0..<224 {
                    func value(_ j:Int)->Double{channel==0 ? samples[j].0:samples[j].1}
                    let average=(value(max(0,i-1))+value(i)+value(min(223,i+1)))/3
                    let point=NSPoint(x:8+CGFloat(i),y:187-CGFloat(min(1,average/maximum))*126)
                    if i==0{line.move(to:point)}else{line.line(to:point)}
                }
                if channel==0 {let fill=line.copy() as! NSBezierPath;fill.line(to:NSPoint(x:231,y:187));fill.line(to:NSPoint(x:8,y:187));fill.close();NSColor(calibratedRed:0,green:0.33,blue:0,alpha:1).setFill();fill.fill()}
                (channel==0 ? NSColor(calibratedRed:0,green:0.85,blue:0.2,alpha:1):NSColor(calibratedRed:1,green:0.8,blue:0,alpha:1)).setStroke();line.lineWidth=2;line.stroke()
            }
            text("CPU",28,196,34,18,9,.gray);text("MEM",130,196,34,18,9,.gray)
            for (value,x) in [(m.cpuPercent,CGFloat(62)),(m.memoryPercent,CGFloat(164))]{(String(format:"%.0f%%",value) as NSString).draw(at:NSPoint(x:x,y:190),withAttributes:[.font:NSFont.monospacedSystemFont(ofSize:15,weight:.bold),.foregroundColor:NSColor.white])}
            let axis=NSMutableParagraphStyle();axis.alignment = .right
            (rate(Int64(maximum)).replacingOccurrences(of:"/s",with:"") as NSString).draw(in:NSRect(x:120,y:46,width:112,height:12),withAttributes:[.font:NSFont.monospacedSystemFont(ofSize:8,weight:.medium),.foregroundColor:NSColor.gray,.paragraphStyle:axis])
            centered("MAC NET  -  56s",NSRect(x:0,y:212,width:240,height:12),.monospacedSystemFont(ofSize:8,weight:.medium),.gray)
        case "music":
            if s.music?.hasArtwork != false, let cover=resources?().first(where:{$0.kind == .musicCover}),let image=MacPetAnimation.image(cover.data,width:112,height:112){image.draw(in:NSRect(x:56,y:16,width:128,height:128))}
            else {NSColor.darkGray.setFill();NSRect(x:56,y:16,width:128,height:128).fill();centered("No Art",NSRect(x:56,y:72,width:128,height:20),.monospacedSystemFont(ofSize:13,weight:.semibold),.lightGray)}
            let title=s.music?.title ?? ""
            centered(title.isEmpty ? "No Music":title,NSRect(x:12,y:154,width:216,height:24),.systemFont(ofSize:15,weight:.bold),.white)
            centered(s.music?.artist ?? "",NSRect(x:12,y:178,width:216,height:20),.systemFont(ofSize:12),.lightGray)
            NSColor.darkGray.setFill();NSRect(x:20,y:210,width:200,height:8).fill()
            if let m=s.music,m.durationSeconds>0{(m.playing ? NSColor.green:NSColor.gray).setFill();NSRect(x:20,y:210,width:200*min(1,max(0,m.elapsedSeconds/m.durationSeconds)),height:8).fill()}
        case "screensaver":
            let tick=s.epochUtc/5
            func bounce(_ value:Int64,_ range:Int64)->CGFloat {let phase=value%(range*2);return CGFloat(phase>range ? range*2-phase:phase)}
            let x=6+bounce(tick*2,24),y=12+bounce(tick,90)
            let numbers=Array(s.time.filter{$0.isNumber}.prefix(4)).compactMap{$0.wholeNumberValue}
            if numbers.count==4 {for i in 0..<4 {digit(numbers[i],x+CGFloat([0,47,115,162][i]),y,42,76,.cyan)}}
            NSColor.yellow.setFill();NSBezierPath(ovalIn:NSRect(x:x+97,y:y+21,width:10,height:10)).fill();NSBezierPath(ovalIn:NSRect(x:x+97,y:y+45,width:10,height:10)).fill()
            let f=DateFormatter();f.dateFormat="MM-dd";text(f.string(from:s.capturedAt),x+25,y+86,100,28,22,.lightGray)
            text("周"+String("日一二三四五六"[String.Index(utf16Offset:Calendar.current.component(.weekday,from:s.capturedAt)-1,in:"日一二三四五六")]),x+133,y+86,75,28,22,.yellow)
        case "activity","domestic":
            text(s.domesticActivity?.activeProvider.uppercased() ?? "LOCAL ACTIVITY",18,30,210,30,20,.cyan)
            text(s.domesticActivity?.state ?? "offline",18,90,210,30,20,.white)
            text("本机 Tokens",18,150,210,22,14,.gray);text(String(s.domesticActivity?.tokensToday ?? 0),18,177,210,32,25,.white)
        default:
            let role=s.claude.state=="working" && s.codex.state != "working" ? "claude":"codex"
            text(role.uppercased(),20,20,210,22,15,.cyan);pet(role,centerY:120,animate:s.claude.state=="working" || s.codex.state=="working")
        }
        if ((mode=="codex" && s.codex.needsInput)||(mode=="claude" && s.claude.needsInput)) && Int64(s.capturedAt.timeIntervalSince1970*1000)%800<400 {NSColor.systemRed.setFill();for edge in [NSRect(x:4,y:4,width:232,height:10),NSRect(x:226,y:4,width:10,height:232),NSRect(x:4,y:226,width:232,height:10),NSRect(x:4,y:4,width:10,height:232)]{edge.fill()}}
        if mode=="codex",s.codex.completionActive {
            let phase=Int((Date().timeIntervalSince1970-Double(s.codex.completionAt))*1000/70)
            if (0..<50).contains(phase) {NSColor(calibratedRed:0,green:CGFloat([40,88,144,208,255,255,208,144,88,0][phase%10])/255,blue:0,alpha:1).setStroke();let ring=NSBezierPath(rect:NSRect(x:6,y:6,width:228,height:228));ring.lineWidth=8;ring.stroke()}
        }
    }
    private var animationTime:[String:(Date,Int)]=[:]
    private func centered(_ value:String,_ rect:NSRect,_ font:NSFont,_ color:NSColor){let style=NSMutableParagraphStyle();style.alignment = .center;style.lineBreakMode = .byTruncatingTail;(value as NSString).draw(in:rect,withAttributes:[.font:font,.foregroundColor:color,.paragraphStyle:style])}
    private func quotaRing(_ percent:Double){var remaining=CGFloat(min(100,max(0,percent)))*9.28;NSColor(calibratedRed:0,green:0.85,blue:0.2,alpha:1).setFill();for index in 0..<4 {let length=min(232,max(0,remaining));let rect:NSRect;switch index {case 0:rect=NSRect(x:4,y:4,width:length,height:10);case 1:rect=NSRect(x:226,y:4,width:10,height:length);case 2:rect=NSRect(x:236-length,y:226,width:length,height:10);default:rect=NSRect(x:4,y:236-length,width:10,height:length)};rect.fill();remaining-=232}}
    private func digit(_ value:Int,_ x:CGFloat,_ y:CGFloat,_ w:CGFloat,_ h:CGFloat,_ color:NSColor) {
        let t=max(2,w/7),half=h/2
        let segments:[(NSRect,String)]=[(NSRect(x:t,y:0,width:w-2*t,height:t),"02356789"),(NSRect(x:t,y:half-t/2,width:w-2*t,height:t),"2345689"),(NSRect(x:t,y:h-t,width:w-2*t,height:t),"0235689"),(NSRect(x:0,y:t,width:t,height:half-t),"045689"),(NSRect(x:w-t,y:t,width:t,height:half-t),"01234789"),(NSRect(x:0,y:half,width:t,height:half-t),"0268"),(NSRect(x:w-t,y:half,width:t,height:half-t),"013456789")]
        color.setFill();for (rect,numbers) in segments where numbers.contains(String(value)) {rect.offsetBy(dx:x,dy:y).fill()}
    }
    private func pet(_ role:String,centerY:CGFloat,animate:Bool) {
        let now=Date(),previous=animationTime[role] ?? (now,0);let tick=previous.1+(animate ? min(250,max(0,Int(now.timeIntervalSince(previous.0)*1000))):0);animationTime[role]=(now,tick)
        guard let animation=MacPetCache.shared.selection(role),let image=animation.image(milliseconds:tick) else {text("请导入桌宠",60,100,140,30,14,.gray);return}
        image.draw(in:NSRect(x:120-CGFloat(animation.width)/2,y:centerY-CGFloat(animation.height)/2,width:CGFloat(animation.width),height:CGFloat(animation.height)))
    }
    private func quota(_ q:ProviderQuotaSnapshot?,label:String,top:CGFloat){text(label+"  "+(q?.plan ?? ""),20,top,200,23,15,.cyan);text("5H \(percent(q?.primaryPercent))  \(reset(q?.primaryResetsAt))",20,top+28,200,24,14,.white);text("WK \(percent(q?.weeklyPercent))  \(reset(q?.weeklyResetsAt))",20,top+55,200,24,14,.white)}
    private func percent(_ value:Double?)->String {value.map{String(format:"%.0f%%",$0)} ?? "--"}
    private func reset(_ date:Date?)->String {guard let date else{return "--"};let minutes=max(0,Int(ceil(date.timeIntervalSinceNow/60)));return minutes>=1440 ? "\(minutes/1440)d":minutes>=60 ? "\(minutes/60)h":"\(minutes)m"}
    private func rate(_ bytes:Int64)->String {bytes>=1000000 ? String(format:"%.1fM/s",Double(bytes)/1000000):bytes>=1000 ? String(format:"%.0fK/s",Double(bytes)/1000):"\(bytes)B/s"}
    private func text(_ text:String,_ x:CGFloat,_ y:CGFloat,_ w:CGFloat,_ h:CGFloat,_ size:CGFloat,_ color:NSColor){let paragraph=NSMutableParagraphStyle();paragraph.lineBreakMode = .byTruncatingTail;(text as NSString).draw(in:NSRect(x:x,y:y,width:w,height:h),withAttributes:[.font:NSFont.monospacedSystemFont(ofSize:size,weight:.medium),.foregroundColor:color,.paragraphStyle:paragraph])}
}

final class MacMirrorWindow:NSObject,NSPopoverDelegate {
    private var timer:Timer?
    private let panel=NSPopover()
    private let scene=MacMirrorView()
    private weak var anchor:NSStatusBarButton?
    private let changeMode:(String)->Void
    private let changeBrightness:(Int)->Bool
    private let capture:()->MacStatusSnapshot
    private var controls:NSSegmentedControl!
    private var slider:NSSlider!
    private let valueLabel=NSTextField(labelWithString:"100%")
    private let statusLabel=NSTextField(labelWithString:"亮度操作通过 USB 发送")
    private let modes=["auto","claude","codex","system","music","stocks"]
    private var lastSent=Date.distantPast
    init(anchor:NSStatusBarButton,capture:@escaping()->MacStatusSnapshot,resources:@escaping()->[MacResourcePayload],select:@escaping(String)->Void,brightness:@escaping(Int)->Bool) {
        self.anchor=anchor;self.capture=capture;changeMode=select;changeBrightness=brightness
        super.init()
        let content=NSViewController();content.view=NSView(frame:NSRect(x:0,y:0,width:316,height:424))
        scene.frame=NSRect(x:14,y:122,width:288,height:288);scene.capture=capture;scene.resources=resources
        content.view.addSubview(scene)
        controls=NSSegmentedControl(labels:["自动","Claude","Codex","网速","音乐","股票"],trackingMode:.selectOne,target:self,action:#selector(modeChanged))
        controls.frame=NSRect(x:8,y:86,width:300,height:24);content.view.addSubview(controls)
        let sun=NSTextField(labelWithString:"☀");sun.frame=NSRect(x:16,y:56,width:22,height:22);content.view.addSubview(sun)
        slider=NSSlider(value:100,minValue:0,maxValue:100,target:self,action:#selector(levelChanged));slider.isContinuous=true
        slider.frame=NSRect(x:44,y:56,width:208,height:22);content.view.addSubview(slider)
        valueLabel.frame=NSRect(x:260,y:56,width:40,height:22);valueLabel.alignment = .right;valueLabel.font = .monospacedDigitSystemFont(ofSize:11,weight:.regular);content.view.addSubview(valueLabel)
        statusLabel.frame=NSRect(x:10,y:26,width:296,height:22);statusLabel.alignment = .center;statusLabel.font = .systemFont(ofSize:11);statusLabel.textColor = .secondaryLabelColor;content.view.addSubview(statusLabel)
        panel.contentViewController=content;panel.contentSize=NSSize(width:316,height:424);panel.behavior = .transient;panel.delegate=self
    }
    @objc private func modeChanged(){let index=controls.selectedSegment;if modes.indices.contains(index){changeMode(modes[index])}}
    @objc private func levelChanged(){let level=Int(slider.doubleValue.rounded());valueLabel.stringValue="\(level)%";if NSApp.currentEvent?.type == .leftMouseDragged && Date().timeIntervalSince(lastSent)<0.25{return};lastSent=Date();statusLabel.stringValue=changeBrightness(level) ? "亮度已发送":"设备未连接，亮度未发送"}
    func open(){if panel.isShown{panel.performClose(nil);return};guard let anchor else{return};panel.show(relativeTo:anchor.bounds,of:anchor,preferredEdge:.minY);tick();timer?.invalidate();timer=Timer.scheduledTimer(withTimeInterval:0.07,repeats:true){[weak self] _ in self?.tick()}}
    private func tick(){controls.selectedSegment=modes.firstIndex(of:capture().displayPolicy?.selectedMode ?? "auto") ?? -1;scene.needsDisplay=true}
    func popoverDidClose(_ notification:Notification){timer?.invalidate();timer=nil}
}
