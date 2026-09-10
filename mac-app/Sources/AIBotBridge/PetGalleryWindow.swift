import AppKit
import Foundation
import ImageIO

struct MacGalleryPet: Decodable { let slug:String; let displayName:String; let spritesheetUrl:URL }
struct MacGalleryMotion {
    let label:String;let row:Int;let count:Int;let duration:Int
    static let all:[MacGalleryMotion] = [
        .init(label:"待机",row:0,count:6,duration:1100),.init(label:"右跑",row:1,count:8,duration:1060),.init(label:"左跑",row:2,count:8,duration:1060),
        .init(label:"挥手",row:3,count:4,duration:700),.init(label:"跳跃",row:4,count:5,duration:840),.init(label:"失败",row:5,count:8,duration:1220),
        .init(label:"等待",row:6,count:6,duration:1010),.init(label:"原地跑",row:7,count:6,duration:820),.init(label:"思考",row:8,count:6,duration:1030)]
}
final class MacGalleryService: NSObject, URLSessionTaskDelegate {
    static let shared=MacGalleryService()
    private lazy var session=URLSession(configuration:.ephemeral,delegate:self,delegateQueue:nil)
    func urlSession(_ session:URLSession,task:URLSessionTask,willPerformHTTPRedirection response:HTTPURLResponse,newRequest request:URLRequest,completionHandler:@escaping(URLRequest?)->Void) {completionHandler(nil)}
    static func allowed(_ url:URL)->Bool {url.scheme=="https" && url.host=="assets.petdex.dev" && (url.port==nil || url.port==443) && url.user==nil && url.password==nil}
    func fetch(_ url:URL,limit:Int) async throws -> Data {
        guard Self.allowed(url) else {throw MacPetAssetError.decodeFailed}
        let (bytes,response)=try await session.bytes(for:URLRequest(url:url,timeoutInterval:45))
        guard (response as? HTTPURLResponse)?.statusCode==200,response.expectedContentLength<=Int64(limit) else {throw MacPetAssetError.decodeFailed}
        var result=Data()
        for try await byte in bytes {try Task.checkCancellation();guard result.count<limit else {throw MacPetAssetError.invalidFile};result.append(byte)}
        return result
    }
    func list() async throws -> [MacGalleryPet] {
        struct Manifest:Decodable {let pets:[MacGalleryPet]}
        let cache=FileManager.default.urls(for:.cachesDirectory,in:.userDomainMask)[0].appendingPathComponent("AI-bot-petdex.json")
        do {
            let data=try await fetch(URL(string:"https://assets.petdex.dev/manifests/petdex-v1.json")!,limit:8*1024*1024)
            let pets=try JSONDecoder().decode(Manifest.self,from:data).pets.filter{Self.allowed($0.spritesheetUrl)}
            guard !pets.isEmpty else {throw MacPetAssetError.decodeFailed}
            try? data.write(to:cache,options:.atomic);return pets
        } catch {
            if Task.isCancelled {throw CancellationError()}
            let data=try Data(contentsOf:cache);guard data.count<=8*1024*1024 else {throw MacPetAssetError.invalidFile}
            return try JSONDecoder().decode(Manifest.self,from:data).pets.filter{Self.allowed($0.spritesheetUrl)}
        }
    }
    func sheet(_ pet:MacGalleryPet) async throws -> CGImage {
        let data=try await fetch(pet.spritesheetUrl,limit:20*1024*1024)
        guard let source=CGImageSourceCreateWithData(data as CFData,nil),let image=CGImageSourceCreateImageAtIndex(source,0,nil),
              image.width==1536,[1872,2288].contains(image.height) else {throw MacPetAssetError.decodeFailed}
        return image
    }
    static func animate(_ image:CGImage,motion:MacGalleryMotion,owner:String) throws -> MacPetAnimation {
        let width=owner=="claude" ? 111:120
        var frames:[Data]=[]
        for index in 0..<motion.count {
            guard let crop=image.cropping(to:CGRect(x:index*192,y:motion.row*208,width:192,height:208)),
                  let pixels=MacRgb565Renderer.renderImage(crop,width:width,height:120) else {throw MacPetAssetError.renderFailed}
            frames.append(pixels)
        }
        return MacPetAnimation(width:width,height:120,delays:Array(repeating:max(50,motion.duration/motion.count/10*10),count:motion.count),frames:frames)
    }
}

final class MacPetGalleryWindow: NSWindowController, NSTableViewDataSource, NSTableViewDelegate, NSSearchFieldDelegate, NSWindowDelegate {
    private let search=NSSearchField(), table=NSTableView(), preview=NSImageView(), owner=NSPopUpButton(), motion=NSPopUpButton()
    private let status=NSTextField(wrappingLabelWithString:"正在读取图库…")
    private let apply=NSButton(title:"应用并同步",target:nil,action:nil)
    private var all:[MacGalleryPet]=[], filtered:[MacGalleryPet]=[], animation:MacPetAnimation?
    private var work:Task<Void,Never>?, loadWork:Task<Void,Never>?, timer:Timer?
    private var generation=0, started=Date()
    private var cachedSlug:String?,cachedImage:CGImage?
    private let selectPage:(String)->Void
    init(selectPage:@escaping(String)->Void) {
        self.selectPage=selectPage
        let window=NSWindow(contentRect:NSRect(x:0,y:0,width:500,height:620),styleMask:[.titled,.closable,.resizable],backing:.buffered,defer:false)
        super.init(window:window);window.title="更换桌宠动画（petdex）";window.minSize=NSSize(width:450,height:540);window.delegate=self
        let stack=NSStackView();stack.orientation = .vertical;stack.spacing=10;stack.edgeInsets=NSEdgeInsets(top:14,left:14,bottom:14,right:14);stack.translatesAutoresizingMaskIntoConstraints=false
        window.contentView!.addSubview(stack);NSLayoutConstraint.activate([stack.leadingAnchor.constraint(equalTo:window.contentView!.leadingAnchor),stack.trailingAnchor.constraint(equalTo:window.contentView!.trailingAnchor),stack.topAnchor.constraint(equalTo:window.contentView!.topAnchor),stack.bottomAnchor.constraint(equalTo:window.contentView!.bottomAnchor)])
        search.placeholderString="搜索桌宠名称…";search.delegate=self;stack.addArrangedSubview(search)
        table.addTableColumn(NSTableColumn(identifier:NSUserInterfaceItemIdentifier("pet")));table.headerView=nil;table.dataSource=self;table.delegate=self
        let scroll=NSScrollView();scroll.documentView=table;scroll.hasVerticalScroller=true;stack.addArrangedSubview(scroll)
        preview.imageScaling = .scaleNone;preview.wantsLayer=true;preview.layer?.backgroundColor=NSColor.black.cgColor;stack.addArrangedSubview(preview);preview.heightAnchor.constraint(equalToConstant:140).isActive=true
        owner.addItems(withTitles:["Claude","Codex"]);motion.addItems(withTitles:MacGalleryMotion.all.map(\.label));motion.selectItem(at:7)
        owner.target=self;owner.action=#selector(selectionChanged);motion.target=self;motion.action=#selector(selectionChanged);apply.target=self;apply.action=#selector(save);apply.isEnabled=false
        stack.addArrangedSubview(NSStackView(views:[owner,motion,apply]));stack.addArrangedSubview(status)
        stack.addArrangedSubview(NSTextField(wrappingLabelWithString:"素材只保存在本机，不随公开包分发；同步与设备验收是不同状态。"))
        for view in [search,scroll,preview,status] {view.widthAnchor.constraint(equalTo:stack.widthAnchor,constant:-28).isActive=true}
        scroll.heightAnchor.constraint(greaterThanOrEqualToConstant:160).isActive=true
    }
    required init?(coder:NSCoder) {fatalError("init(coder:) unsupported")}
    func open() {
        showWindow(nil);window?.center();NSApp.activate(ignoringOtherApps:true)
        timer?.invalidate();timer=Timer.scheduledTimer(withTimeInterval:0.04,repeats:true){[weak self] _ in self?.paint()}
        guard all.isEmpty else {return}
        loadWork?.cancel();loadWork=Task {@MainActor [weak self] in
            guard let self else {return}
            do {let pets=try await MacGalleryService.shared.list();try Task.checkCancellation();self.all=pets.sorted{$0.displayName.localizedCaseInsensitiveCompare($1.displayName) == .orderedAscending};self.filter();self.status.stringValue="共 \(pets.count) 个桌宠"}
            catch {if !Task.isCancelled {self.status.stringValue="加载失败："+error.localizedDescription}}
        }
    }
    func windowWillClose(_ notification:Notification) {work?.cancel();loadWork?.cancel();timer?.invalidate();generation+=1}
    func numberOfRows(in tableView:NSTableView)->Int {filtered.count}
    func tableView(_ tableView:NSTableView,viewFor tableColumn:NSTableColumn?,row:Int)->NSView? {NSTextField(labelWithString:"\(filtered[row].displayName)（\(filtered[row].slug)）")}
    func tableViewSelectionDidChange(_ notification:Notification) {selectionChanged()}
    func controlTextDidChange(_ obj:Notification) {filter()}
    private func filter() {let q=search.stringValue;filtered=all.filter{q.isEmpty || $0.slug.localizedCaseInsensitiveContains(q) || $0.displayName.localizedCaseInsensitiveContains(q)};table.reloadData();selectionChanged()}
    @objc private func selectionChanged() {
        generation+=1;let current=generation;work?.cancel();animation=nil;apply.isEnabled=false;preview.image=nil
        guard filtered.indices.contains(table.selectedRow) else {return}
        let pet=filtered[table.selectedRow],selectedOwner=owner.indexOfSelectedItem==0 ? "claude":"codex",selectedMotion=MacGalleryMotion.all[motion.indexOfSelectedItem]
        work=Task {@MainActor [weak self] in
            guard let self else {return}
            do {
                let image:CGImage
                if self.cachedSlug==pet.slug,let cached=self.cachedImage {image=cached} else {image=try await MacGalleryService.shared.sheet(pet)}
                try Task.checkCancellation();guard self.generation==current else {return}
                self.cachedSlug=pet.slug;self.cachedImage=image;self.animation=try MacGalleryService.animate(image,motion:selectedMotion,owner:selectedOwner);self.started=Date();self.apply.isEnabled=true;self.paint();self.status.stringValue=pet.displayName+" · "+selectedMotion.label
            } catch {if !Task.isCancelled && self.generation==current {self.status.stringValue="预览失败："+error.localizedDescription}}
        }
    }
    private func paint() {preview.image=animation?.image(milliseconds:Int(Date().timeIntervalSince(started)*1000))}
    @objc private func save() {
        guard let animation else {return};let role=owner.indexOfSelectedItem==0 ? "claude":"codex"
        do {try MacPetCache.shared.select(role,data:animation.encode());selectPage(role);status.stringValue="已保存到 \(role)，等待桥接同步；未确认设备显示。"}
        catch {status.stringValue="保存失败："+error.localizedDescription}
    }
}
