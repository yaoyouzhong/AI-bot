import AppKit

if CommandLine.arguments.count == 3, CommandLine.arguments[1] == "--self-test-tab5-ui" {
    do { try MacTab5UIValidation.run(URL(fileURLWithPath: CommandLine.arguments[2], isDirectory: true)); exit(0) }
    catch { fputs("Native UI fixture render failed: \(error.localizedDescription)\n", stderr); exit(1) }
}

if Array(CommandLine.arguments.dropFirst()) == ["--self-test-flash-tool"] {
    do { try MacFirmwareFlasher.checkBundledTool(); exit(0) }
    catch { fputs("\(error.localizedDescription)\n", stderr); exit(1) }
}

let application = NSApplication.shared
let delegate = AppDelegate()
application.delegate = delegate
application.setActivationPolicy(.accessory)
application.run()
