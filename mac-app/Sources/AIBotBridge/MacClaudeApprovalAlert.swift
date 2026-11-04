import AppKit

// Constructed only by a user-opened settings action or a synthetic offscreen
// renderer. Tool arguments never enter device frames, logs or preferences.
enum MacClaudeApprovalAlert {
    static func make(tool: String, input: [String:Any]) -> NSAlert {
        let alert = NSAlert()
        alert.messageText = "Claude 请求使用 \(tool)"
        alert.informativeText = "请核对本次操作参数。允许仅适用于这一次请求，沿用 Claude 当前权限设置。"
        alert.addButton(withTitle:"拒绝")
        alert.addButton(withTitle:"允许本次")
        alert.addButton(withTitle:"稍后处理")
        let scroll = NSScrollView(frame:NSRect(x:0,y:0,width:640,height:300))
        scroll.hasVerticalScroller = true; scroll.hasHorizontalScroller = true; scroll.borderType = .bezelBorder
        let text = NSTextView(frame:NSRect(x:0,y:0,width:620,height:300))
        text.isEditable = false; text.isSelectable = true; text.isRichText = false
        text.font = .monospacedSystemFont(ofSize:12,weight:.regular)
        text.textContainerInset = NSSize(width:12,height:12)
        text.string = (try? JSONSerialization.data(withJSONObject:input,options:[.prettyPrinted,.sortedKeys])).map { String(decoding:$0,as:UTF8.self) } ?? "参数暂不可读；请拒绝并重新检查。"
        text.isVerticallyResizable = true; text.autoresizingMask = [.width]
        scroll.documentView = text; alert.accessoryView = scroll
        return alert
    }
}
