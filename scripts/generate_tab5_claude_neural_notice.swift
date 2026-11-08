// Generate a local preview with the installed macOS neural Chinese voice.
// The generated audio is a local candidate, not cleared for redistribution.
import AVFoundation
import Foundation

guard CommandLine.arguments.count == 2 else {
    fatalError("Usage: swift generate_tab5_claude_neural_notice.swift OUTPUT.caf")
}
guard let voice = AVSpeechSynthesisVoice(identifier: "com.apple.ttsbundle.gryphon-neural_Linfei_zh-CN_premium") else {
    fatalError("Required macOS voice is unavailable")
}
let utterance = AVSpeechUtterance(string: "Claude，任务已完成")
utterance.voice = voice
utterance.rate = AVSpeechUtteranceDefaultSpeechRate * 0.9
let synthesizer = AVSpeechSynthesizer()
let lock = NSLock()
var output: AVAudioFile?
var finished = false
var failure: String?

synthesizer.write(utterance) { buffer in
    lock.lock()
    defer { lock.unlock() }
    guard let pcm = buffer as? AVAudioPCMBuffer else {
        failure = "Unexpected audio buffer"
        finished = true
        return
    }
    do {
        if pcm.frameLength > 0 {
            if output == nil {
                output = try AVAudioFile(forWriting: URL(fileURLWithPath: CommandLine.arguments[1]),
                                         settings: pcm.format.settings,
                                         commonFormat: pcm.format.commonFormat,
                                         interleaved: pcm.format.isInterleaved)
            }
            try output?.write(from: pcm)
        }
        // This macOS voice terminates the stream with one silent frame.
        if pcm.frameLength <= 1 { finished = true }
    } catch {
        failure = String(describing: error)
        finished = true
    }
}
let deadline = Date().addingTimeInterval(30)
while Date() < deadline {
    lock.lock()
    let isFinished = finished
    lock.unlock()
    if isFinished { break }
    RunLoop.current.run(until: Date().addingTimeInterval(0.05))
}
lock.lock()
let success = finished && failure == nil && output != nil
let message = failure
output = nil
lock.unlock()
guard success else { fatalError(message ?? "Voice generation timed out") }
print("CLAUDE_NOTICE_CAF_READY: local macOS voice, \(voice.name)")
