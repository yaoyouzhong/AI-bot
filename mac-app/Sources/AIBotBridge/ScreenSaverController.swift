import CoreGraphics
import Foundation

struct AutomaticScreenSaverState {
    private(set) var selectedMode = "auto"
    private(set) var active = false

    mutating func select(_ mode: String) {
        selectedMode = mode
        active = false
    }

    func desiredMode(idleSeconds: TimeInterval, timeoutMinutes: Int) -> String? {
        guard timeoutMinutes > 0, selectedMode != "screensaver" else { return nil }
        let timeout = TimeInterval(timeoutMinutes * 60)
        if !active && idleSeconds >= timeout { return "screensaver" }
        if active && idleSeconds < timeout { return selectedMode }
        return nil
    }

    mutating func confirm(_ mode: String, sent: Bool) {
        guard sent else { return }
        active = mode == "screensaver"
    }
}

enum MacIdleTime {
    static func seconds() -> TimeInterval {
        let anyInput = CGEventType(rawValue: UInt32.max)!
        return CGEventSource.secondsSinceLastEventType(.combinedSessionState, eventType: anyInput)
    }
}
