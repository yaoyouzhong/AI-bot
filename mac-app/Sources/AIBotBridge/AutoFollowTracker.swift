import Foundation

final class MacAutoFollowTracker {
    private var current="claude"
    private var switchedAt:Int64?
    // Called under SessionActivityReader's lock, with monotonic time.
    func update(_ s:MacStatusSnapshot,milliseconds:Int64)->String {
        if switchedAt == nil || milliseconds < switchedAt! {switchedAt=milliseconds}
        let selected=s.displayPolicy?.selectedMode ?? "auto"
        let attention=s.claude.needsInput || s.codex.needsInput || s.codex.completionActive
        let hidden=selected=="screensaver" || s.domesticActivity?.needsInput==true || !attention &&
            (!["auto","claude","codex"].contains(selected) || selected=="auto" &&
             (s.displayPolicy?.cycleEnabled==true || s.music?.playing==true || s.domesticActivity?.state=="working"))
        if hidden {return current}
        var next=current
        if s.claude.needsInput != s.codex.needsInput {next=s.claude.needsInput ? "claude":"codex"}
        else if s.codex.completionActive {next="codex"}
        else if selected=="claude" || selected=="codex" {next=selected}
        else if (s.claude.state=="working") != (s.codex.state=="working") {next=s.claude.state=="working" ? "claude":"codex"}
        else if milliseconds-switchedAt! >= (s.claude.state=="working" ? 2000:6000) {next=current=="claude" ? "codex":"claude"}
        if next != current {current=next;switchedAt=milliseconds}
        return current
    }
}
