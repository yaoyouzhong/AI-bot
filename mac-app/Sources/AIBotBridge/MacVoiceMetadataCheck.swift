import AppKit

/// Read-only timing of metadata preparation. No recorder, AX navigation, input
/// source selection, credential access or preference write is performed.
enum MacVoiceMetadataCheck {
    static func run(_ output: URL) throws {
        _ = NSApplication.shared; NSApp.setActivationPolicy(.prohibited)
        var rows: [[String: Any]] = [], finished = false
        DispatchQueue.global(qos: .userInitiated).async {
            var samples: [[String: Any]] = []
            for index in 0..<3 {
                let queued = ProcessInfo.processInfo.systemUptime
                let mainWait = MacTab5ComputerPlatform.main { ProcessInfo.processInfo.systemUptime - queued }
                let start = ProcessInfo.processInfo.systemUptime, env = MacVoiceEnvironment.capture()
                samples.append(["sample": index + 1, "mainQueueWaitMs": Int((mainWait * 1000).rounded()),
                    "metadataMs": Int(((ProcessInfo.processInfo.systemUptime - start) * 1000).rounded()),
                    "inputs": env.inputs.count, "doubaoInstalled": env.doubaoInstalled,
                    "doubaoEnabled": env.doubaoEnabled, "doubaoRunning": env.doubaoRunning,
                    "localSpeechAuthorized": env.speechPermission, "microphoneAuthorized": env.microphonePermission,
                    "localRecognitionAvailable": env.localRecognition])
            }
            let result = samples
            DispatchQueue.main.async { rows = result; finished = true }
        }
        let deadline = ProcessInfo.processInfo.systemUptime + 45
        while !finished, ProcessInfo.processInfo.systemUptime < deadline { RunLoop.current.run(until: Date().addingTimeInterval(0.01)) }
        guard finished else { throw MacTab5LocalError.invalid("Voice metadata inspection timed out") }
        let data = try JSONSerialization.data(withJSONObject: ["samples": rows, "readOnly": true, "recordedAudio": false, "changedInputMethod": false], options: [.prettyPrinted, .sortedKeys])
        try data.write(to: output, options: .atomic)
        print("MAC_VOICE_METADATA_OK 3 read-only samples; no recording, navigation or preference changes")
    }
}
