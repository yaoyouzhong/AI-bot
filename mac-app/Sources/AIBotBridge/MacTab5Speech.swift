import Foundation
import AVFoundation
import Speech

final class MacTab5Speech {
    private let lock = NSRecursiveLock()
    private var recognizer: SFSpeechRecognizer?
    private var request: SFSpeechAudioBufferRecognitionRequest?
    private var recognition: SFSpeechRecognitionTask?
    private var engine: AVAudioEngine?
    private var generation = UUID()
    private var transcript = MacTab5SpeechTranscript()
    private var updates = 0, timedUpdates = 0
    private var lastDiagnostics = ""
    func backend() -> MacTab5SpeechBackend { .init(start: start, feed: feed, finish: finish, cancel: cancel, diagnostics: diagnostics) }
    private func diagnostics() -> String {
        lock.lock(); defer { lock.unlock() }; return lastDiagnostics
    }
    static func authorize(_ completion: @escaping (Bool) -> Void) {
        SFSpeechRecognizer.requestAuthorization { status in
            guard status == .authorized else { DispatchQueue.main.async { completion(false) }; return }
            AVCaptureDevice.requestAccess(for: .audio) { allowed in DispatchQueue.main.async { completion(allowed) } }
        }
    }
    private func start(_ source: String, callback: @escaping (String, Bool, MacTab5SpeechFailure?) -> Void) throws {
        lock.lock(); defer { lock.unlock() }; cancel()
        updates = 0; timedUpdates = 0; lastDiagnostics = ""
        guard SFSpeechRecognizer.authorizationStatus() == .authorized, let recognizer = SFSpeechRecognizer(locale: Locale(identifier: "zh-CN")), recognizer.isAvailable, recognizer.supportsOnDeviceRecognition else { throw MacTab5Error.unavailable }
        let request = SFSpeechAudioBufferRecognitionRequest(); request.requiresOnDeviceRecognition = true; request.shouldReportPartialResults = true; request.taskHint = .dictation
        self.recognizer = recognizer; self.request = request
        let callbacks = OperationQueue(); callbacks.maxConcurrentOperationCount = 1; recognizer.queue = callbacks
        let owner = generation
        recognition = recognizer.recognitionTask(with: request) { [weak self] result, error in
            guard let self else { return }
            self.lock.lock()
            guard self.generation == owner else { self.lock.unlock(); return }
            let transcription = result?.bestTranscription
            let segments = transcription?.segments ?? []
            let text = self.transcript.update(transcription?.formattedString ?? "",
                start: segments.map(\.timestamp).min(), end: segments.map { $0.timestamp + $0.duration }.max())
            if transcription != nil {
                self.updates += 1
                if segments.contains(where: { $0.timestamp > 0 || $0.duration > 0 }) { self.timedUpdates += 1 }
                self.lastDiagnostics = "本地识别：\(self.updates) 次结果 · \(self.timedUpdates) 次含有效音频位置 · 保留 \(self.transcript.utteranceCount) 段"
            }
            self.lock.unlock()
            // Never call into the voice owner while holding the backend lock:
            // stop/feed take those locks in the opposite order.
            callback(text, result?.isFinal == true, error.map(MacTab5SpeechFailure.init))
        }
        if source == "pc" {
            guard AVCaptureDevice.authorizationStatus(for: .audio) == .authorized else { cancel(); throw MacTab5Error.unavailable }
            let engine = AVAudioEngine(), input = engine.inputNode
            do { try MacAudioInputs.apply(to:input) } catch { cancel(); throw error }
            let format = input.outputFormat(forBus:0)
            guard format.sampleRate > 0, format.channelCount > 0 else { cancel(); throw MacTab5Error.unavailable }
            input.installTap(onBus: 0, bufferSize: 1024, format: format) { buffer, _ in request.append(buffer) }
            self.engine = engine; engine.prepare()
            do { try engine.start() } catch { cancel(); throw error }
        }
    }
    private func feed(_ pcm: [Int16]) throws {
        lock.lock(); defer { lock.unlock() }
        guard let request, let format = AVAudioFormat(commonFormat: .pcmFormatFloat32, sampleRate: 16000, channels: 1, interleaved: false), let buffer = AVAudioPCMBuffer(pcmFormat: format, frameCapacity: AVAudioFrameCount(pcm.count)), let channel = buffer.floatChannelData?[0] else { throw MacTab5Error.unavailable }
        buffer.frameLength = AVAudioFrameCount(pcm.count)
        for index in pcm.indices { channel[index] = Float(pcm[index]) / 32768 }; request.append(buffer)
    }
    private func finish() {
        lock.lock(); defer { lock.unlock() }; engine?.stop(); engine?.inputNode.removeTap(onBus: 0); engine = nil; request?.endAudio()
    }
    private func cancel() {
        lock.lock(); defer { lock.unlock() }; engine?.stop(); engine?.inputNode.removeTap(onBus: 0); engine = nil
        generation = UUID(); transcript = MacTab5SpeechTranscript()
        recognition?.cancel(); recognition = nil; request = nil; recognizer = nil
    }
    deinit { cancel() }
}
