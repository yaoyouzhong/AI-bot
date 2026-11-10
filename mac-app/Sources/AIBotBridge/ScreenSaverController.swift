import CoreGraphics
import Foundation

struct AutomaticScreenSaverState {
    private(set) var selectedMode = "auto"
    private(set) var active = false
    private var lastAiWorking = false
    private var lastMusicPlaying = false
    private var temporaryWakeUntil: Date?
    private var pendingTemporaryMode: String?

    mutating func select(_ mode: String) {
        selectedMode = mode
        active = false
        temporaryWakeUntil = nil
        pendingTemporaryMode = nil
    }

    mutating func desiredMode(idleSeconds: TimeInterval, timeoutMinutes: Int,
                              aiWorking: Bool = false, musicPlaying: Bool = false,
                              now: Date = Date(), wakeMode: String? = nil) -> String? {
        defer {
            lastAiWorking = aiWorking
            lastMusicPlaying = musicPlaying
        }
        guard timeoutMinutes > 0, selectedMode != "screensaver" else { return nil }
        let timeout = TimeInterval(timeoutMinutes * 60)
        if !active && idleSeconds >= timeout {
            temporaryWakeUntil = nil
            return "screensaver"
        }
        if active && idleSeconds < 3 { return selectedMode }
        if active && ((musicPlaying && !lastMusicPlaying) || (aiWorking && !lastAiWorking)) {
            let mode = wakeMode ?? (musicPlaying ? "music" : "pet")
            pendingTemporaryMode = mode
            return mode
        }
        if active, let temporaryWakeUntil, temporaryWakeUntil <= now { return "screensaver" }
        return nil
    }

    mutating func confirm(_ mode: String, sent: Bool, now: Date = Date()) {
        if pendingTemporaryMode == mode {
            pendingTemporaryMode = nil
            if sent { temporaryWakeUntil = now.addingTimeInterval(12) }
            return
        }
        pendingTemporaryMode = nil
        guard sent else { return }
        if mode == "screensaver" {
            active = true
            temporaryWakeUntil = nil
        } else {
            active = false
            temporaryWakeUntil = nil
        }
    }
}

enum MacIdleTime {
    static func seconds() -> TimeInterval {
        let anyInput = CGEventType(rawValue: UInt32.max)!
        return CGEventSource.secondsSinceLastEventType(.combinedSessionState, eventType: anyInput)
    }
}


// TAB5 compares timestamps, not idle durations. Stabilize the estimate
// against sampling jitter so unchanged heartbeats cannot wake the display.
struct MacTab5InputActivity {
    private var lastTick: Int64?

    mutating func sample(uptime: TimeInterval, idle: TimeInterval) -> Int64? {
        guard uptime.isFinite, idle.isFinite, idle >= 0, uptime >= idle, uptime < 9_000_000_000_000 else { return nil }
        let tick = Int64((uptime - idle) * 1000)
        if lastTick.map({ tick > $0 + 50 }) ?? true { lastTick = tick }
        return lastTick
    }
}
