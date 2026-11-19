import AppKit

enum MacUpdateNotes {
    static func selected(_ body: String, component: String, version: String) -> String {
        let clean = body.replacingOccurrences(of: #"(?s)<!--.*?-->"#, with: "", options: .regularExpression)
        var lines: [String] = []
        var sections: [String: [String]] = [:]
        var scope: String?, downloads = false
        for raw in clean.components(separatedBy: .newlines) {
            let line = raw.trimmingCharacters(in: .whitespaces)
            if line.hasPrefix("## ") {
                let heading = String(line.dropFirst(3))
                let name = heading.lowercased()
                if name == "english", lines.contains(where: { !$0.isEmpty }) { break }
                downloads = name.hasPrefix("下载") || name.hasPrefix("完整下载") || name.hasPrefix("download") || name.hasPrefix("all downloads")
                scope = nil
                if let number = heading.range(of: #"[0-9]+\.[0-9]+\.[0-9]+(?:-ui)?"#, options: .regularExpression) {
                    let key = name.contains("tab5") ? "tab5" :
                        (name.contains("esp8266") || name.contains("小屏") ? "esp8266" :
                        (name.contains("桥接") || name.contains("bridge") ? "bridge" : nil))
                    if let key {
                        let section = key + ":" + heading[number]
                        scope = section; sections[section] = []
                        continue
                    }
                }
            }
            guard !downloads, line != "---", line != "## English" else { continue }
            lines.append(line)
            if let scope { sections[scope, default: []].append(line) }
        }
        let matching = sections[component + ":" + version] ?? (sections.isEmpty ? lines : [])
        let result = matching.joined(separator: "\n").trimmingCharacters(in: .whitespacesAndNewlines)
        return result.isEmpty ? "暂无更新说明。" : result
    }
}

final class MacUpdateNotesView: NSScrollView {
    private let text = NSTextView()
    private var current = ""
    init() {
        super.init(frame: .zero)
        borderType = .bezelBorder
        hasVerticalScroller = true
        autohidesScrollers = true
        text.isEditable = false
        text.isVerticallyResizable = true
        text.isHorizontallyResizable = false
        text.autoresizingMask = [.width]
        text.textContainer?.widthTracksTextView = true
        text.textContainerInset = NSSize(width: 9, height: 9)
        text.setAccessibilityLabel("所选更新说明")
        documentView = text
        heightAnchor.constraint(equalToConstant: 156).isActive = true
    }

    required init?(coder: NSCoder) { fatalError() }

    func show(_ notes: String) {
        guard notes != current else { return }
        current = notes
        let result = NSMutableAttributedString()
        for raw in notes.split(separator: "\n") {
            var line = raw.trimmingCharacters(in: .whitespaces)
            guard !line.isEmpty else { continue }
            let heading = line.hasPrefix("#")
            let bullet = line.hasPrefix("- ") || line.hasPrefix("* ")
            if heading { line = line.drop(while: { $0 == "#" }).trimmingCharacters(in: .whitespaces) }
            if bullet { line = "•  " + line.dropFirst(2) }
            line = line.replacingOccurrences(of: #"\[([^\]]+)\]\([^\s)]+\)"#, with: "$1", options: .regularExpression)
                .replacingOccurrences(of: "**", with: "").replacingOccurrences(of: "`", with: "")
            let style = NSMutableParagraphStyle()
            style.paragraphSpacingBefore = heading && result.length > 0 ? 5 : 0
            style.paragraphSpacing = heading ? 5 : 8
            if bullet { style.headIndent = 14 }
            result.append(NSAttributedString(string: line + "\n", attributes: [
                .font: NSFont.systemFont(ofSize: 13, weight: heading ? .semibold : .regular),
                .foregroundColor: NSColor.labelColor, .paragraphStyle: style
            ]))
        }
        text.textStorage?.setAttributedString(result)
        text.scrollRangeToVisible(NSRange(location: 0, length: 0))
    }
}
