import AppKit

if Array(CommandLine.arguments.dropFirst()) == ["--self-test-flash-tool"] {
    do { try MacFirmwareFlasher.checkBundledTool(); exit(0) }
    catch { fputs("\(error.localizedDescription)\n", stderr); exit(1) }
}

let application = NSApplication.shared
let delegate = AppDelegate()
application.delegate = delegate
application.setActivationPolicy(.accessory)
application.run()
