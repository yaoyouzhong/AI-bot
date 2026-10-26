import AppKit
import CoreGraphics
import Security

/// A protected local screen can coexist with an unlocked automation session.
/// This read-only compatibility observation never dismisses a cover or grants
/// permission to type. No titles, pixels, AX fields or application files are read.
enum MacScreenProtection {
    static let guardianID = "com.openai.sky.CUAService.guardian"
    struct Observation {
        let active:Bool?
        let windows:Int
        var summary:String {
            let state = active.map { $0 ? "受保护":"未观察到" } ?? "未知"
            return "外层保护 \(state)，在屏窗口 \(windows)"
        }
    }
    private static func signedGuardian(_ app:NSRunningApplication) -> Bool {
        guard !app.isTerminated,app.bundleIdentifier == guardianID else { return false }
        var code:SecCode?, requirement:SecRequirement?
        let rule = "anchor apple generic and identifier \"\(guardianID)\" and certificate leaf[subject.OU] = \"2DC432GLL2\""
        return SecCodeCopyGuestWithAttributes(nil,[kSecGuestAttributePid:app.processIdentifier] as CFDictionary,[],&code) == errSecSuccess &&
            SecRequirementCreateWithString(rule as CFString,[],&requirement) == errSecSuccess &&
            code != nil && requirement != nil && SecCodeCheckValidity(code!,[],requirement) == errSecSuccess
    }
    static func coversDisplay(_ window:CGRect,displays:[CGRect]) -> Bool {
        func valid(_ rect:CGRect) -> Bool {
            !rect.isInfinite && !rect.isNull && [rect.origin.x,rect.origin.y,rect.width,rect.height].allSatisfy { $0.isFinite } && rect.width > 0 && rect.height > 0
        }
        guard valid(window) else { return false }
        return displays.contains { valid($0) && window.insetBy(dx:-2,dy:-2).contains($0) }
    }
    /// Partial/unsupported evidence is unknown. A resident helper with no
    /// visible windows is not a lock; neither is an arbitrary application's UI.
    static func classify(visible:Int,covers:Int,complete:Bool) -> Bool? {
        guard (0...16).contains(visible),(0...visible).contains(covers) else { return nil }
        if covers > 0 { return true }
        return complete && visible == 0 ? false:nil
    }
    static func read() -> Observation {
        let unknown = Observation(active:nil,windows:0)
        let apps = NSRunningApplication.runningApplications(withBundleIdentifier:guardianID)
        guard !apps.isEmpty else { return .init(active:false,windows:0) }
        guard apps.count <= 4,apps.allSatisfy(signedGuardian) else { return unknown }
        let pids = Set(apps.map(\.processIdentifier))
        guard let rows = CGWindowListCopyWindowInfo([.optionOnScreenOnly,.excludeDesktopElements],kCGNullWindowID) as? [[String:Any]],rows.count <= 512 else { return unknown }
        var ids = [CGDirectDisplayID](repeating:0,count:32), count:UInt32 = 0
        guard CGGetActiveDisplayList(32,&ids,&count) == .success,count > 0,count < 32 else { return unknown }
        let displays = ids.prefix(Int(count)).map { CGDisplayBounds($0) }
        let deadline = ProcessInfo.processInfo.systemUptime+0.25
        var visible = 0, covers = 0, complete = true
        for row in rows {
            guard ProcessInfo.processInfo.systemUptime < deadline else { complete = false; break }
            guard let owner = row[kCGWindowOwnerPID as String] as? NSNumber,CFGetTypeID(owner) != CFBooleanGetTypeID(),
                  owner.int32Value > 0,owner.doubleValue == Double(owner.int32Value) else { complete = false; continue }
            guard pids.contains(owner.int32Value) else { continue }
            guard let shown = row[kCGWindowIsOnscreen as String],CFGetTypeID(shown as CFTypeRef) == CFBooleanGetTypeID(),
                  let alpha = row[kCGWindowAlpha as String] as? NSNumber,CFGetTypeID(alpha) != CFBooleanGetTypeID(),
                  alpha.doubleValue.isFinite,(0...1).contains(alpha.doubleValue) else { complete = false; continue }
            guard shown as? Bool == true,alpha.doubleValue > 0 else { continue }
            visible += 1
            guard visible <= 16 else { complete = false; break }
            guard let layer = row[kCGWindowLayer as String] as? NSNumber,CFGetTypeID(layer) != CFBooleanGetTypeID(),
                  layer.doubleValue == Double(layer.int32Value),
                  let bounds = row[kCGWindowBounds as String] as? [String:Any],
                  let rect = CGRect(dictionaryRepresentation:bounds as CFDictionary) else { complete = false; continue }
            if layer.int32Value >= CGWindowLevelForKey(.screenSaverWindow),coversDisplay(rect,displays:displays) { covers += 1 }
        }
        // App termination/PID changes during observation invalidate absence.
        if apps.contains(where:{ $0.isTerminated }) { complete = false }
        return .init(active:classify(visible:visible,covers:covers,complete:complete),windows:visible)
    }
}
