import Foundation

enum MacTab5BLEControlPacing {
    static func active(now:TimeInterval,lastControl:TimeInterval,lastFrame:TimeInterval) -> Bool {
        now.isFinite && lastControl.isFinite && lastFrame.isFinite &&
        now >= lastControl && now-lastControl < 1 && now >= lastFrame && now-lastFrame < 4
    }
}

enum MacTab5BLEVoicePacing {
    enum Mailbox { case voice, control }
    // Only completed, new voice requests extend this lease; idle polls don't.
    static func active(now: TimeInterval, lastVoice: TimeInterval) -> Bool {
        now.isFinite && lastVoice.isFinite && now >= lastVoice && now - lastVoice < 1
    }
    static func continuationDelay(now: TimeInterval, lastVoice: TimeInterval, completedVoice: Bool) -> TimeInterval? {
        guard active(now: now, lastVoice: lastVoice) else { return nil }
        // A newly confirmed request can have captured audio queued behind it.
        // Match Windows' immediate continuation; empty/duplicate reads still back off.
        return completedVoice ? 0 : 0.015
    }
    static func priority(now: TimeInterval, lastVoice: TimeInterval, lastControl: TimeInterval,
                         lastFrame: TimeInterval) -> Mailbox? {
        guard active(now: now, lastVoice: lastVoice), lastFrame.isFinite,
              now >= lastFrame, now - lastFrame < 4 else { return nil }
        // Probe control about once a second, instead of after every audio block.
        return !lastControl.isFinite || now < lastControl || now - lastControl >= 1 ? .control : .voice
    }
}
