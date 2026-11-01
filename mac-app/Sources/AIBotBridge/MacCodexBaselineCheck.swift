import Foundation

/// Inspect the real pre-action receipt contract without navigating or sending.
/// Only availability/counts/timings leave the process; no IDs or text are logged.
enum MacCodexBaselineCheck {
    static func run(_ output: URL) throws {
        // Standalone debug binaries lack the installed app's preference domain.
        // Add its read-only search domain for the same existing opt-in and CLI path.
        if Bundle.main.bundleIdentifier == nil { UserDefaults.standard.addSuite(named: "io.github.yaoyouzhong.aibot.bridge") }
        guard UserDefaults.standard.bool(forKey: MacTab5Codex.enabledKey) else { throw MacTab5Error.unavailable }
        let server = MacTab5CodexRPC()
        defer { server.stop() }
        let result = try server.call("thread/list", ["limit": 64, "archived": false, "sortKey": "recency_at", "sortDirection": "desc", "sourceKinds": ["cli", "vscode", "appServer"], "modelProviders": [String]()])
        let cwd = URL(fileURLWithPath: FileManager.default.currentDirectoryPath).standardizedFileURL.path
        guard let row = (result["data"] as? [[String: Any]])?.first(where: {
            ($0["cwd"] as? String).map { URL(fileURLWithPath: $0).standardizedFileURL.path == cwd } == true
        }), let task = MacTab5Codex.id(row["id"]) else { throw MacTab5Error.unavailable }
        let desktop = MacCodexDesktopIPC()
        var samples: [[String: Any]] = []
        for _ in 0..<3 {
            let start = ProcessInfo.processInfo.systemUptime
            do {
                let receipt = try desktop.submissionReceipt(task, expected: "AIBot read-only baseline probe; never submitted")
                samples.append(["acceptedBaseline": true, "localQueueBaseline": receipt.queue != nil, "serverQueueBaseline": receipt.serverQueue != nil,
                    "complete": receipt.queue != nil && receipt.serverQueue != nil,
                    "elapsedMs": Int(((ProcessInfo.processInfo.systemUptime - start) * 1000).rounded())])
            } catch {
                samples.append(["acceptedBaseline": false, "complete": false,
                    "elapsedMs": Int(((ProcessInfo.processInfo.systemUptime - start) * 1000).rounded())])
            }
        }
        try JSONSerialization.data(withJSONObject: ["readOnly": true, "submitted": false, "samples": samples], options: [.prettyPrinted, .sortedKeys]).write(to: output, options: .atomic)
        guard samples.allSatisfy({ $0["complete"] as? Bool == true }) else { throw MacTab5Error.unconfirmed }
        print("MAC_CODEX_BASELINE_OK 3 real complete pre-action baselines; no navigation or submission")
    }
}
