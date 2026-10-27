import Foundation

enum MacTab5USBDisplayOwnership {
    static func owns(now: TimeInterval, acknowledgedAt: TimeInterval, active: String?) -> Bool {
        active == "USB" && acknowledgedAt > 0 && now >= acknowledgedAt && now - acknowledgedAt < 8
    }
}
