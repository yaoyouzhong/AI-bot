import Foundation
import ApplicationServices
import AppKit

enum MacVNCUnlockValidation {
    static func run() throws -> Int {
        var checks = 0
        func check(_ name:String,_ condition:@autoclosure () throws -> Bool) throws {
            guard try condition() else { throw MacTab5LocalError.invalid("Unlock test: "+name) }; checks += 1
        }
        for displayOnly in [false,true] {
            try check("regular desktop policy preserved",MacScreenLockObservation.desktopFocus(regularFront:true,ownProcess:false,displayOnly:displayOnly))
            try check("other accessory focus never proves a desktop",!MacScreenLockObservation.desktopFocus(regularFront:false,ownProcess:false,displayOnly:displayOnly))
            try check("own accessory focus is display-only",MacScreenLockObservation.desktopFocus(regularFront:false,ownProcess:true,displayOnly:displayOnly) == displayOnly)
        }
        for observed:Bool? in [true,false,nil] {
            try check("own desktop display still requires unlocked observation and no protection",MacScreenLockObservation.displayState(focus:false,observed:observed,protection:false) == (observed == true ? true:observed == false ? false:nil))
            try check("own accessory never weakens outer protection",MacScreenLockObservation.displayState(focus:false,observed:observed,protection:true) == true && MacScreenLockObservation.credentialState(focus:nil,observed:observed,protection:true) == nil)
            try check("cold own desktop proof is display-only and locked observation vetoes",MacScreenLockObservation.displayState(focus:nil,observed:observed,protection:false,desktopEvidence:true) == (observed == true ? true:false) && MacScreenLockObservation.credentialState(focus:nil,observed:observed) == nil)
            try check("cold desktop proof cannot override missing or positive protection",MacScreenLockObservation.displayState(focus:nil,observed:observed,protection:true,desktopEvidence:true) == true && MacScreenLockObservation.displayState(focus:nil,observed:observed,protection:nil,desktopEvidence:true) == (observed == true ? true:nil))
        }
        try check("automation desktop focus cannot override observed lock for display",MacScreenLockObservation.displayState(focus:false,observed:true) == true)
        try check("automation desktop focus cannot falsely confirm successful unlock",MacScreenLockObservation.credentialState(focus:false,observed:true) == nil)
        for observed:Bool? in [true,false,nil] {
            try check("notifications never authorize credential input without verified focus",MacScreenLockObservation.credentialState(focus:nil,observed:observed) == nil)
            try check("verified loginwindow remains locked despite notification state",MacScreenLockObservation.credentialState(focus:true,observed:observed) == true && MacScreenLockObservation.displayState(focus:true,observed:observed) == true)
        }
        try check("desktop confirmation resumes after unlock with fresh focus",MacScreenLockObservation.credentialState(focus:false,observed:false) == false)
        try check("unknown startup display remains unknown",MacScreenLockObservation.displayState(focus:nil,observed:nil) == nil)
        for label in ["Log In","Unlock","登录","登入","解锁"] {
            try check("explicit login label accepted",MacVNCUnlockTest.explicitLoginLabel(title:label,description:nil))
        }
        for label in ["Cancel","Show Password","登录帮助","Log In as Alice","Other"] {
            try check("arbitrary or conflicting button captions rejected",!MacVNCUnlockTest.explicitLoginLabel(title:label,description:"登录"))
        }
        try check("descriptive login label only for untitled button",MacVNCUnlockTest.explicitLoginLabel(title:"",description:"登录") && !MacVNCUnlockTest.explicitLoginLabel(title:nil,description:nil))
        try check("desktop focus without an unlock observation stays unknown",MacScreenLockObservation.credentialState(focus:false,observed:nil) == nil && MacScreenLockObservation.displayState(focus:false,observed:nil) == nil)
        try check("an unlock notification without desktop focus stays unknown",MacScreenLockObservation.displayState(focus:nil,observed:false) == nil)
        let lockObservation = MacScreenLockObservation()
        lockObservation.receive(.locked,onConsole:true)
        lockObservation.receive(.invalidate,onConsole:false)
        try check("session invalidation cannot erase an observed lock",lockObservation.value(onConsole:true) == true)
        try check("off-console reads are unknown without erasing lock",lockObservation.value(onConsole:false) == nil && lockObservation.value(onConsole:true) == true)
        lockObservation.receive(.unlocked,onConsole:true)
        try check("current console unlock clears lock latch",lockObservation.value(onConsole:true) == false)
        lockObservation.receive(.invalidate,onConsole:false)
        try check("stale unlocked evidence invalidates on session transition",lockObservation.value(onConsole:true) == nil)
        try check("compatibility hint accepts only boolean metadata",MacScreenLockObservation.lockHint(true) == true && MacScreenLockObservation.lockHint(false) == false && MacScreenLockObservation.lockHint(nil) == nil && MacScreenLockObservation.lockHint("true") == nil && MacScreenLockObservation.lockHint(NSNumber(value:2)) == nil)
        for focus:Bool? in [true,false,nil] {
            for observed:Bool? in [true,false,nil] {
                try check("visible protection overrides automation session for display without authorizing credentials",
                    MacScreenLockObservation.displayState(focus:focus,observed:observed,protection:true) == true &&
                    MacScreenLockObservation.credentialState(focus:focus,observed:observed,protection:true) == nil)
                try check("unavailable protection evidence cannot authorize input or confirm desktop",
                    MacScreenLockObservation.credentialState(focus:focus,observed:observed,protection:nil) == nil)
            }
        }
        try check("uncertain cover cannot be reported as unlocked despite matching session signals",
            MacScreenLockObservation.displayState(focus:false,observed:false,protection:nil) == nil)
        try check("protection clearing restores ordinary post-authentication desktop decision",
            MacScreenLockObservation.displayState(focus:false,observed:false,protection:false) == false &&
            MacScreenLockObservation.credentialState(focus:false,observed:false,protection:false) == false)
        try check("resident helper without visible windows does not imply protection",MacScreenProtection.classify(visible:0,covers:0,complete:true) == false)
        try check("incomplete or partial guardian windows do not establish an accessible desktop",
            MacScreenProtection.classify(visible:0,covers:0,complete:false) == nil && MacScreenProtection.classify(visible:1,covers:0,complete:true) == nil)
        try check("positive cover remains protective even with other incomplete observations",MacScreenProtection.classify(visible:2,covers:1,complete:false) == true)
        try check("invalid counts are unknown",MacScreenProtection.classify(visible:17,covers:1,complete:true) == nil && MacScreenProtection.classify(visible:0,covers:1,complete:true) == nil)
        let screen = CGRect(x:0,y:0,width:1440,height:900), left = CGRect(x:-1920,y:0,width:1920,height:1080)
        try check("full protection covers either display including negative display coordinates",MacScreenProtection.coversDisplay(screen,displays:[screen,left]) && MacScreenProtection.coversDisplay(left,displays:[screen,left]))
        try check("small HUD or offset window does not prove a full-screen guard",!MacScreenProtection.coversDisplay(CGRect(x:0,y:0,width:200,height:80),displays:[screen]) && !MacScreenProtection.coversDisplay(CGRect(x:200,y:0,width:1440,height:900),displays:[screen]))
        try check("missing displays and invalid geometry never prove a cover",!MacScreenProtection.coversDisplay(screen,displays:[]) && !MacScreenProtection.coversDisplay(.infinite,displays:[screen]) && !MacScreenProtection.coversDisplay(.zero,displays:[screen]))
        for failure in ["none","cancel-before","unknown","cancel-after","changed","late"] {
            var events:[String] = [], time = 0.0
            do {
                try MacVNCUnlockTest.publicCharacterOnce(ready:{ failure != "unknown" && (failure != "changed" || events.isEmpty) },isCancelled:{ failure == "cancel-before" || (failure == "cancel-after" && !events.isEmpty) },down:{ events.append("down") },up:{ events.append("up") },now:{time},hold:{ if failure == "late" { time = 3 } })
                try check("public sample pair occurs once",failure == "none" && events == ["down","up"])
            } catch {
                let expected:[String] = ["cancel-before","unknown"].contains(failure) ? []:["down","up"]
                try check("rejected/cancelled probe never replays and balances release",failure != "none" && events == expected)
            }
        }
        checks += try validateCancellationRecovery()
        var confirmationTime = 0.0
        try check("post-disconnect desktop must remain confirmed for two seconds",try MacVNCUnlockTest.confirmDisconnectedDesktop(state:{false},isCancelled:{false},now:{confirmationTime},pause:{confirmationTime += 0.1}) && confirmationTime >= 2)
        confirmationTime = 0
        do {
            _ = try MacVNCUnlockTest.confirmDisconnectedDesktop(state:{confirmationTime >= 0.6},isCancelled:{false},now:{confirmationTime},pause:{confirmationTime += 0.1})
            throw MacTab5Error.invalidPacket
        } catch let error as MacTab5LocalError { try check("service relock after disconnect is not success",error.localizedDescription.contains("再次锁屏") && confirmationTime < 1) }
        confirmationTime = 0
        try check("unknown desktop after disconnect cannot confirm",try !MacVNCUnlockTest.confirmDisconnectedDesktop(state:{nil},isCancelled:{false},now:{confirmationTime},pause:{confirmationTime += 0.1}))
        confirmationTime = 0
        try check("cancel interrupts post-disconnect confirmation",try !MacVNCUnlockTest.confirmDisconnectedDesktop(state:{false},isCancelled:{confirmationTime >= 0.5},now:{confirmationTime},pause:{confirmationTime += 0.1}) && confirmationTime < 1)
        confirmationTime = 0
        try check("late desktop read cannot confirm",try !MacVNCUnlockTest.confirmDisconnectedDesktop(state:{confirmationTime += 4; return false},isCancelled:{false},now:{confirmationTime},pause:{}))
        var activityReleased = false
        let activityResult = try MacVNCUnlockTest.withDisplayActivity(create:{42},release:{activityReleased = $0 == 42},operation:{!activityReleased})
        try check("activity is released before caller confirms desktop",activityResult && activityReleased)
        activityReleased = false
        do { let _:Bool = try MacVNCUnlockTest.withDisplayActivity(create:{42},release:{activityReleased = $0 == 42},operation:{throw MacTab5Error.unconfirmed}); throw MacTab5Error.invalidPacket }
        catch MacTab5Error.unconfirmed { try check("failed unlock also releases display activity",activityReleased) }
        for initial:Bool? in [nil,false] {
            var time = 0.0, observations = 0, activities = 0, releases = 0, operations = 0
            let result = try MacVNCUnlockTest.withVerifiedDisplay(protection:{
                observations += 1
                if observations == 1 { return initial }
                return time >= 0.2 ? false:nil
            },isCancelled:{false},create:{activities += 1;return 42},release:{_ in releases += 1},
                now:{time},pause:{time += 0.05},operation:{operations += 1;return observations})
            try check("unknown/clear initial protection permits only one display wake",activities == 1 && releases == 1)
            try check("fresh unprotected display precedes protected operation",result > 1 && time >= 0.2 && operations == 1)
        }
        for failure in ["cover-before","cover-after","unknown","cancel-before","cancel-after","cancel-read","slow-read","wake","operation"] {
            var time = 0.0, observations = 0, activities = 0, releases = 0, operations = 0
            do {
                let _:Bool = try MacVNCUnlockTest.withVerifiedDisplay(protection:{
                    observations += 1
                    if failure == "slow-read",observations > 1 { time = 4 }
                    if failure == "cover-before" { return true }
                    if failure == "cover-after" { return observations > 1 ? true:nil }
                    return failure == "operation" ? false:nil
                },isCancelled:{failure == "cancel-before" || (failure == "cancel-after" && activities > 0) || (failure == "cancel-read" && observations > 1)},
                    create:{activities += 1;if failure == "wake" { throw MacTab5Error.unavailable };return 42},
                    release:{_ in releases += 1},now:{time},pause:{time += 0.05},operation:{
                        operations += 1;throw MacTab5Error.unconfirmed
                    })
                throw MacTab5Error.invalidPacket
            } catch {
                let beforeWake = ["cover-before","cancel-before"].contains(failure)
                try check("cover/cancel/unknown/late evidence cannot reach credential operation",operations == (failure == "operation" ? 1:0))
                try check("display assertion has bounded lifetime on all failures",activities == (beforeWake ? 0:1) && releases == (beforeWake || failure == "wake" ? 0:1) && time <= 4)
            }
        }
        var directTime = 0.0
        try MacVNCUnlockTest.waitForLocked(state:{ directTime >= 0.2 ? true:nil },isCancelled:{false},now:{directTime},pause:{directTime += 0.1})
        try check("wake preparation waits for signed lock observation",directTime >= 0.2)
        for state:Bool? in [nil,false] {
            directTime = 0
            do {
                try MacVNCUnlockTest.waitForLocked(state:{state},isCancelled:{false},now:{directTime},pause:{directTime += 0.1})
                throw MacTab5Error.invalidPacket
            } catch is MacTab5LocalError { try check("unknown or desktop state never authorizes input",state == nil ? directTime >= 3:directTime == 0) }
        }
        directTime = 0
        do {
            try MacVNCUnlockTest.waitForLocked(state:{ directTime = 4; return true },isCancelled:{false},now:{directTime},pause:{})
            throw MacTab5Error.invalidPacket
        } catch MacTab5Error.unconfirmed { checks += 1 }
        var directWrites = 0, directConfirms = 0
        let directPassword = Data("Fixture9!".utf8)
        try MacVNCUnlockTest.submitDirectOnce(directPassword,ready:{true},write:{_ in directWrites += 1},confirm:{directConfirms += 1},isCancelled:{false})
        try check("direct submission writes and confirms exactly once",directWrites == 1 && directConfirms == 1)
        directWrites = 0; directConfirms = 0
        var buttonLookups = 0
        try MacVNCUnlockTest.submitDirectOnce(directPassword,ready:{true},write:{_ in directWrites += 1},confirm:{
            buttonLookups += 1
            guard directWrites == 1 else { throw MacTab5Error.unconfirmed }
            directConfirms += 1
        },isCancelled:{false})
        try check("nonempty-only login button is resolved after the single write",directWrites == 1 && buttonLookups == 1 && directConfirms == 1)
        directWrites = 0; directConfirms = 0; buttonLookups = 0
        do {
            try MacVNCUnlockTest.submitDirectOnce(directPassword,ready:{true},write:{_ in directWrites += 1},confirm:{
                buttonLookups += 1
                throw MacTab5Error.unconfirmed
            },isCancelled:{false})
            throw MacTab5Error.invalidPacket
        } catch MacTab5Error.unconfirmed {
            try check("missing button after write never resends password or submits",directWrites == 1 && buttonLookups == 1 && directConfirms == 0)
        }
        for failure in ["not-ready","cancelled","write","changed","slow","confirm"] {
            directWrites = 0; directConfirms = 0; directTime = 0
            do {
                try MacVNCUnlockTest.submitDirectOnce(directPassword,ready:{failure != "not-ready" && !(failure == "changed" && directWrites > 0)},write:{_ in
                    directWrites += 1
                    if failure == "write" { throw MacTab5Error.unconfirmed }
                    if failure == "slow" { directTime = 4 }
                },confirm:{directConfirms += 1; throw MacTab5Error.unconfirmed},isCancelled:{failure == "cancelled"},now:{directTime})
                throw MacTab5Error.invalidPacket
            } catch MacTab5Error.unconfirmed {
                let noWrite = ["not-ready","cancelled"].contains(failure)
                try check("direct error never retries or falls back",directWrites == (noWrite ? 0:1) && directConfirms == (failure == "confirm" ? 1:0))
            }
        }
        directTime = 0
        try check("direct success allows login delay and requires stable desktop",try MacVNCUnlockTest.confirmDirectDesktop(state:{directTime < 0.5},isCancelled:{false},now:{directTime},pause:{directTime += 0.1}) && directTime >= 2.5)
        directTime = 0
        do {
            _ = try MacVNCUnlockTest.confirmDirectDesktop(state:{directTime >= 0.6},isCancelled:{false},now:{directTime},pause:{directTime += 0.1})
            throw MacTab5Error.invalidPacket
        } catch is MacTab5LocalError { try check("direct relock cannot report success",directTime < 1) }
        directTime = 0
        try check("direct unknown never confirms",try !MacVNCUnlockTest.confirmDirectDesktop(state:{nil},isCancelled:{false},now:{directTime},pause:{directTime += 0.1}))
        directTime = 0
        try check("direct cancellation cannot confirm",try !MacVNCUnlockTest.confirmDirectDesktop(state:{false},isCancelled:{directTime > 0.5},now:{directTime},pause:{directTime += 0.1}) && directTime < 1)
        directTime = 0
        var localReads = 0
        try check("local feedback requires half-second stability and fresh desktop reads",try MacVNCUnlockTest.confirmDirectDesktop(state:{localReads += 1;return false},isCancelled:{false},stableSeconds:0.5,now:{directTime},pause:{directTime += 0.1}) && directTime >= 0.5 && directTime < 0.7 && localReads >= 3)
        directTime = 0;localReads = 0
        try check("duplicate-time samples cannot replace three distinct local observations",try MacVNCUnlockTest.confirmDirectDesktop(state:{localReads += 1;return false},isCancelled:{false},stableSeconds:0.5,now:{directTime},pause:{directTime = localReads < 4 ? 0.5:0.6}) && localReads == 5 && directTime == 0.6)
        directTime = 0
        try check("local unknown evidence restarts the complete stable interval",try MacVNCUnlockTest.confirmDirectDesktop(state:{directTime >= 0.2 && directTime < 0.4 ? nil:false},isCancelled:{false},stableSeconds:0.5,now:{directTime},pause:{directTime += 0.1}) && directTime >= 0.9)
        directTime = 0
        do {
            _ = try MacVNCUnlockTest.confirmDirectDesktop(state:{directTime >= 0.3},isCancelled:{false},stableSeconds:0.5,now:{directTime},pause:{directTime += 0.1})
            throw MacTab5Error.invalidPacket
        } catch is MacTab5LocalError { try check("local relock still rejects success",directTime < 0.5) }
        directTime = 0
        try check("local cancellation still wins before successful feedback",try !MacVNCUnlockTest.confirmDirectDesktop(state:{false},isCancelled:{directTime >= 0.3},stableSeconds:0.5,now:{directTime},pause:{directTime += 0.1}) && directTime < 0.5)
        directTime = 0
        try check("late local focus query cannot approve feedback",try !MacVNCUnlockTest.confirmDirectDesktop(state:{directTime = 9;return false},isCancelled:{false},stableSeconds:0.5,now:{directTime},pause:{}))
        for invalid:TimeInterval in [0,0.25,3,.nan] {
            var queried = false
            do {
                _ = try MacVNCUnlockTest.confirmDirectDesktop(state:{queried = true;return false},isCancelled:{false},stableSeconds:invalid)
                throw MacTab5Error.unconfirmed
            } catch MacTab5Error.invalidPacket { try check("invalid local confirmation duration rejects before state reads",!queried) }
        }
        let password = Data("Ab9!".utf8)
        let keyboardStart = MacVNCKeyboardStart(), first = UUID(), second = UUID()
        keyboardStart.begin(first,now:100)
        try check("authentication alone never authorizes keyboard input",!keyboardStart.consume(first,now:100) && !keyboardStart.consume(first,now:104))
        try check("connection popover settling rejects early clicks",!keyboardStart.canApprove(first,now:102.99) && !keyboardStart.approve(first,now:102.99))
        try check("settled connection still needs a fresh matching click",keyboardStart.canApprove(first,now:103) && !keyboardStart.approve(second,now:104) && !keyboardStart.consume(second,now:104) && !keyboardStart.consume(first,now:104))
        try check("one matching click authorizes exactly one keyboard stage",keyboardStart.approve(first,now:104) && !keyboardStart.approve(first,now:104) && keyboardStart.consume(first,now:104) && !keyboardStart.consume(first,now:104))
        keyboardStart.begin(first,now:100); keyboardStart.cancel()
        try check("cancelled connection cannot be authorized by a late click",!keyboardStart.approve(first,now:104) && !keyboardStart.consume(first,now:104))
        keyboardStart.begin(first,now:100); keyboardStart.begin(second,now:101)
        try check("old connection click cannot authorize its replacement",!keyboardStart.approve(first,now:105) && !keyboardStart.consume(first,now:105) && keyboardStart.approve(second,now:105) && keyboardStart.consume(second,now:105))
        keyboardStart.begin(first,now:100)
        try check("deadline rejects clicks even if the UI timer has not fired",!keyboardStart.canApprove(first,now:130) && !keyboardStart.approve(first,now:130) && !keyboardStart.consume(first,now:130))
        keyboardStart.begin(first,now:100); _ = keyboardStart.approve(first,now:129)
        try check("expired approval is not carried to keyboard input",!keyboardStart.consume(first,now:130))
        var sourceTime:TimeInterval = 100
        let source = MacVNCInputSourceSnapshot(source:"fixture.roman",revision:0)
        try MacVNCUnlockTest.waitForInputSource(snapshot:{ source },localIdleSeconds:{3},isCancelled:{false},sourceStableSeconds:0.15,now:{sourceTime},pause:{sourceTime += 0.05})
        try check("direct AX avoids a new three-second layout wait when local input is already quiet",sourceTime >= 100.15 && sourceTime < 100.25)
        sourceTime = 100
        try MacVNCUnlockTest.waitForInputSource(snapshot:{sourceTime < 100.1 ? source:MacVNCInputSourceSnapshot(source:"changed",revision:1)},localIdleSeconds:{3},isCancelled:{false},sourceStableSeconds:0.15,now:{sourceTime},pause:{sourceTime += 0.05})
        try check("direct AX focus/source change still restarts fresh stability",sourceTime >= 100.25 && sourceTime < 100.4)
        sourceTime = 100
        try MacVNCUnlockTest.waitForInputSource(snapshot:{source},localIdleSeconds:{max(0,sourceTime-100)},isCancelled:{false},sourceStableSeconds:0.15,now:{sourceTime},pause:{sourceTime += 0.05})
        try check("direct AX still requires three seconds of actual physical-input quiet",sourceTime >= 103 && sourceTime < 103.1)
        sourceTime = 100
        do {
            try MacVNCUnlockTest.waitForInputSource(snapshot:{source},localIdleSeconds:{nil},isCancelled:{false},sourceStableSeconds:0.15,now:{sourceTime},pause:{sourceTime += 0.05})
            throw MacTab5Error.invalidPacket
        } catch is MacTab5LocalError { try check("direct AX cannot bypass missing physical-input evidence",sourceTime >= 110) }
        sourceTime = 100
        do {
            try MacVNCUnlockTest.waitForInputSource(snapshot:{source},localIdleSeconds:{3},isCancelled:{sourceTime >= 100.1},sourceStableSeconds:0.15,now:{sourceTime},pause:{sourceTime += 0.05})
            throw MacTab5Error.invalidPacket
        } catch is MacTab5LocalError { try check("direct AX cancellation wins before early readiness",sourceTime < 100.15) }
        for invalid:TimeInterval in [0,.nan,4] {
            var queried = false
            do {
                try MacVNCUnlockTest.waitForInputSource(snapshot:{queried = true;return source},localIdleSeconds:{3},isCancelled:{false},sourceStableSeconds:invalid)
                throw MacTab5Error.unconfirmed
            } catch MacTab5Error.invalidPacket { try check("invalid source-stability configuration cannot weaken readiness",!queried) }
        }
        sourceTime = 100
        try MacVNCUnlockTest.waitForInputSource(snapshot:{ source },localIdleSeconds:{ 3 },isCancelled:{ false },now:{ sourceTime },pause:{ sourceTime += 0.25 })
        try check("first key waits for a complete quiet interval",sourceTime == 103)
        sourceTime = 100
        try MacVNCUnlockTest.waitForInputSource(snapshot:{ sourceTime < 100.5 ? source:MacVNCInputSourceSnapshot(source:"fixture.changed",revision:0) },localIdleSeconds:{ 3 },isCancelled:{ false },now:{ sourceTime },pause:{ sourceTime += 0.25 })
        try check("source change restarts readiness before any input",sourceTime == 103.5)
        sourceTime = 100
        try MacVNCUnlockTest.waitForInputSource(snapshot:{ MacVNCInputSourceSnapshot(source:source.source,revision:sourceTime < 100.75 ? 0:1) },localIdleSeconds:{ 3 },isCancelled:{ false },now:{ sourceTime },pause:{ sourceTime += 0.25 })
        try check("same-source change notification also restarts readiness",sourceTime == 103.75)
        sourceTime = 100
        do {
            try MacVNCUnlockTest.waitForInputSource(snapshot:{ source },localIdleSeconds:{ 3 },isCancelled:{ sourceTime >= 100.5 },now:{ sourceTime },pause:{ sourceTime += 0.25 })
            throw MacTab5Error.invalidPacket
        } catch is MacTab5LocalError { try check("cancelled preparation cannot start keyboard input",sourceTime == 100.5) }
        sourceTime = 100
        do {
            try MacVNCUnlockTest.waitForInputSource(snapshot:{ nil },localIdleSeconds:{ 3 },isCancelled:{ false },now:{ sourceTime },pause:{ sourceTime += 0.25 })
            throw MacTab5Error.invalidPacket
        } catch is MacTab5LocalError { try check("unknown input source times out without readiness",sourceTime == 110) }
        sourceTime = 100
        try MacVNCUnlockTest.waitForInputSource(snapshot:{ sourceTime < 100.5 ? nil:source },localIdleSeconds:{ 3 },isCancelled:{ false },now:{ sourceTime },pause:{ sourceTime += 0.25 })
        try check("initially unknown source needs its own complete quiet interval",sourceTime == 103.5)
        sourceTime = 100
        do {
            try MacVNCUnlockTest.waitForInputSource(snapshot:{ throw MacTab5Error.unavailable },localIdleSeconds:{ 3 },isCancelled:{ false },now:{ sourceTime },pause:{ sourceTime += 0.25 })
            throw MacTab5Error.invalidPacket
        } catch MacTab5Error.unavailable { try check("focus or nonempty-probe errors propagate without a key",sourceTime == 100) }
        do {
            try MacVNCUnlockTest.waitForInputSource(snapshot:{ MacVNCInputSourceSnapshot(source:source.source,revision:UInt64((sourceTime-100)*4)) },localIdleSeconds:{ 3 },isCancelled:{ false },now:{ sourceTime },pause:{ sourceTime += 0.25 })
            throw MacTab5Error.invalidPacket
        } catch is MacTab5LocalError { try check("continuous source changes have a bounded deadline",sourceTime == 110) }
        sourceTime = 100
        do {
            try MacVNCUnlockTest.waitForInputSource(snapshot:{ sourceTime += 10; return source },localIdleSeconds:{ 3 },isCancelled:{ false },now:{ sourceTime },pause:{ sourceTime += 0.25 })
            throw MacTab5Error.invalidPacket
        } catch is MacTab5LocalError { try check("slow focus query cannot approve an expired preparation",sourceTime == 110) }
        sourceTime = 100
        try MacVNCUnlockTest.waitForInputSource(snapshot:{ source },localIdleSeconds:{ max(0,sourceTime-102) },isCancelled:{ false },now:{ sourceTime },pause:{ sourceTime += 0.25 })
        try check("source stability cannot bypass recent local cursor activity",sourceTime == 105)
        sourceTime = 100
        do {
            try MacVNCUnlockTest.waitForInputSource(snapshot:{ source },localIdleSeconds:{ 2.99 },isCancelled:{ false },now:{ sourceTime },pause:{ sourceTime += 0.25 })
            throw MacTab5Error.invalidPacket
        } catch is MacTab5LocalError { try check("continuous local activity cannot authorize the first key",sourceTime == 110) }
        let invalidIdle:[TimeInterval?] = [nil,-1,.nan,.infinity,-.infinity]
        for idle in invalidIdle {
            sourceTime = 100
            do {
                try MacVNCUnlockTest.waitForInputSource(snapshot:{ source },localIdleSeconds:{ idle },isCancelled:{ false },now:{ sourceTime },pause:{ sourceTime += 0.25 })
                throw MacTab5Error.invalidPacket
            } catch is MacTab5LocalError { try check("unknown or invalid idle metadata never means ready",sourceTime == 110) }
        }
        sourceTime = 100
        var stopPreparation = false
        do {
            try MacVNCUnlockTest.waitForInputSource(snapshot:{ source },localIdleSeconds:{ stopPreparation = true; return 10 },isCancelled:{ stopPreparation },now:{ sourceTime },pause:{ sourceTime += 0.25 })
            throw MacTab5Error.invalidPacket
        } catch is MacTab5LocalError { try check("cancellation during idle query prevents input",sourceTime == 100) }
        sourceTime = 100
        do {
            try MacVNCUnlockTest.waitForInputSource(snapshot:{ source },localIdleSeconds:{ sourceTime += 10; return 10 },isCancelled:{ false },now:{ sourceTime },pause:{ sourceTime += 0.25 })
            throw MacTab5Error.invalidPacket
        } catch is MacTab5LocalError { try check("expired idle query cannot start typing",sourceTime == 110) }
        func focus(active:Bool = true,visible:Bool = true,key:Bool = true,editor:Bool = true,front:Bool? = true,system:Bool? = nil) -> Bool {
            MacVNCProbeFocusState(active:active,visible:visible,key:key,editor:editor,front:front,systemProbe:system).permits
        }
        try check("active probe needs system frontmost ownership without contradictory AX",focus() && !focus(front:false) && !focus(front:nil) && !focus(system:false))
        try check("inactive flag needs exact independent system probe",focus(active:false,system:true) && !focus(active:false) && !focus(active:false,system:false))
        try check("system proof never overrides changed app window or editor",!focus(active:false,visible:false,system:true) && !focus(active:false,key:false,system:true) && !focus(active:false,editor:false,system:true) && !focus(active:false,front:false,system:true) && !focus(active:false,front:nil,system:true))
        let desktopHeader = Data([0,0,0,1,0,0,0,0,13,128,8,186,255,255,255,33])
        try check("update metadata is diagnostic even for a different region or signed encoding",MacVNCConnectionCheck.updateHeaderNote(desktopHeader)?.contains("width=3456, height=2234, encoding=-223") == true)
        for bytes in [Data(),Data(repeating:0,count:16),Data([3,0,0,1])+Data(repeating:65,count:12)] {
            try check("no diagnostic decoding of short, empty or clipboard messages",MacVNCConnectionCheck.updateHeaderNote(bytes) == nil)
        }
        let pair = MacVNCUnlockTest.key(65,down:true)+MacVNCUnlockTest.key(65,down:false)
        var edges:[Data] = [], paused = false
        try MacVNCUnlockTest.pacedKey(pair,send:{ edge in
            guard edges.isEmpty ? !paused:paused else { throw MacTab5Error.invalidPacket }; edges.append(edge)
        },pause:{ paused = true })
        try check("key hold separates down and up like Windows",paused && edges == [MacVNCUnlockTest.key(65,down:true),MacVNCUnlockTest.key(65,down:false)])
        edges = []
        do { try MacVNCUnlockTest.pacedKey(Data(repeating:0,count:16),send:{ edges.append($0) },pause:{}); throw MacTab5Error.unavailable }
        catch MacTab5Error.invalidPacket { try check("authentication or malformed bytes never enter key pacer",edges.isEmpty) }
        do { try MacVNCUnlockTest.pacedKey(pair,send:{ edges.append($0); throw MacTab5Error.unavailable },pause:{}); throw MacTab5Error.invalidPacket }
        catch MacTab5Error.unavailable { try check("uncertain key down is never repeated",edges.count == 1 && edges[0][1] == 1) }
        edges = []
        do { try MacVNCUnlockTest.pacedKey(pair,send:{ edges.append($0); if edges.count == 2 { throw MacTab5Error.unavailable } },pause:{}); throw MacTab5Error.invalidPacket }
        catch MacTab5Error.unavailable { try check("uncertain key up never repeats down or submits another key",edges.count == 2 && edges[1][1] == 0) }
        var characters = 0, writes:[Data] = [], entered = 0, time = 100.0
        func code(_ data:Data) -> UInt32 { data[4..<8].reduce(0) { ($0 << 8) | UInt32($1) } }
        func send(_ data:Data) throws {
            writes.append(data)
            let key = code(data)
            if key == 0xff08 { characters -= 1 }
            else if key == 0xff0d { entered += 1 }
            else { characters += 1 }
        }
        try MacVNCUnlockTest.sendOnce(password,count:{ characters },send:send,isCancelled:{ false },now:{ time },pause:{ time += 0.1 })
        try check("exact marker, erase, mixed password and one Return",writes.map(code) == [0x76,0xff08,0x41,0x62,0x39,0x21,0xff0d] && entered == 1)
        try check("only balanced RFB key events, no clipboard or framebuffer",writes.allSatisfy { $0.count == 16 && $0[0] == 4 && $0[1] == 1 && $0[8] == 4 && $0[9] == 0 && $0[4..<8] == $0[12..<16] })
        writes = []
        do { try MacVNCUnlockTest.sendOnce(password,count:{ nil },send:{ writes.append($0) },isCancelled:{ false }); throw MacTab5Error.invalidPacket }
        catch is MacTab5LocalError { try check("unknown field sends no key",writes.isEmpty) }
        do { try MacVNCUnlockTest.sendOnce(password,count:{ 1 },send:{ writes.append($0) },isCancelled:{ false }); throw MacTab5Error.invalidPacket }
        catch is MacTab5LocalError { try check("nonempty field sends no key",writes.isEmpty) }
        do { try MacVNCUnlockTest.sendOnce(password,count:{ 0 },send:{ writes.append($0) },isCancelled:{ true }); throw MacTab5Error.invalidPacket }
        catch is MacTab5LocalError { try check("cancelled attempt sends no key",writes.isEmpty) }
        time = 100
        do {
            try MacVNCUnlockTest.sendOnce(password,count:{ 0 },send:{ writes.append($0) },isCancelled:{ false },now:{ time },pause:{ time += 0.1 })
            throw MacTab5Error.invalidPacket
        } catch is MacTab5LocalError { try check("unproven RFB route sends only marker and no Return",writes.count == 1 && code(writes[0]) == 0x76) }
        writes = []; characters = 0
        do {
            try MacVNCUnlockTest.sendOnce(password,count:{ writes.isEmpty ? 0:nil },send:{ writes.append($0) },isCancelled:{ false })
            throw MacTab5Error.invalidPacket
        } catch is MacTab5LocalError { try check("focus loss after marker never sends password",writes.count == 1) }
        writes = []; characters = 0
        do {
            try MacVNCUnlockTest.sendOnce(password,count:{ characters },send:{ try send($0) },isCancelled:{ writes.count >= 3 })
            throw MacTab5Error.invalidPacket
        } catch is MacTab5LocalError { try check("cancellation after partial password never appends Return",writes.map(code) == [0x76,0xff08,0x41]) }
        writes = []; characters = 0
        do {
            try MacVNCUnlockTest.sendOnce(password,count:{ characters },send:{ writes.append($0); throw MacTab5Error.unavailable },isCancelled:{ false })
            throw MacTab5Error.invalidPacket
        } catch MacTab5Error.unavailable { try check("uncertain write is never retried",writes.count == 1) }
        for bytes in [Data(),Data("unsafe\n".utf8),Data("密码".utf8),Data(repeating:0x61,count:129)] {
            writes = []
            do { try MacVNCUnlockTest.sendOnce(bytes,count:{ 0 },send:{ writes.append($0) },isCancelled:{ false }); throw MacTab5Error.invalidPacket }
            catch is MacTab5LocalError { try check("unsupported credential rejected before any input",writes.isEmpty) }
        }
        writes = []; time = 100
        let ready:Int = try MacVNCUnlockTest.prepareField(find:{ 7 },stillLoginWindow:{ true },send:{ writes.append($0) },isCancelled:{ false })
        try check("already visible password field requires no preparation key",ready == 7 && writes.isEmpty)
        let prepared:Int = try MacVNCUnlockTest.prepareField(find:{ writes.isEmpty ? nil:8 },stillLoginWindow:{ true },send:{ writes.append($0) },isCancelled:{ false })
        try check("hidden password page receives one balanced Tab only",prepared == 8 && writes == [MacVNCUnlockTest.key(0xff09,down:true)+MacVNCUnlockTest.key(0xff09,down:false)])
        writes = []; time = 100
        do {
            let _:Int = try MacVNCUnlockTest.prepareField(find:{ nil },stillLoginWindow:{ true },send:{ writes.append($0) },isCancelled:{ false },now:{ time },pause:{ time += 0.1 })
            throw MacTab5Error.invalidPacket
        } catch is MacTab5LocalError { try check("missing field never retries Tab or submits a password",writes.count == 1 && code(writes[0]) == 0xff09) }
        writes = []
        do {
            let _:Int = try MacVNCUnlockTest.prepareField(find:{ nil },stillLoginWindow:{ false },send:{ writes.append($0) },isCancelled:{ false })
            throw MacTab5Error.invalidPacket
        } catch is MacTab5LocalError { try check("unconfirmed loginwindow never receives preparation key",writes.isEmpty) }
        do {
            let _:Int = try MacVNCUnlockTest.prepareField(find:{ nil },stillLoginWindow:{ true },send:{ writes.append($0) },isCancelled:{ true })
            throw MacTab5Error.invalidPacket
        } catch is MacTab5LocalError { try check("cancel before preparation sends nothing",writes.isEmpty) }
        do {
            let _:Int = try MacVNCUnlockTest.prepareField(find:{ nil },stillLoginWindow:{ true },send:{ writes.append($0) },isCancelled:{ !writes.isEmpty },pause:{})
            throw MacTab5Error.invalidPacket
        } catch is MacTab5LocalError { try check("cancel after Tab never repeats preparation",writes.count == 1) }
        writes = []
        do {
            let _:Int = try MacVNCUnlockTest.prepareField(find:{ nil },stillLoginWindow:{ writes.isEmpty },send:{ writes.append($0) },isCancelled:{ false },pause:{})
            throw MacTab5Error.invalidPacket
        } catch is MacTab5LocalError { try check("changed owner after Tab never continues",writes.count == 1) }
        writes = []
        do {
            let _:Int = try MacVNCUnlockTest.prepareField(find:{ nil },stillLoginWindow:{ true },send:{ writes.append($0); throw MacTab5Error.unavailable },isCancelled:{ false })
            throw MacTab5Error.invalidPacket
        } catch MacTab5Error.unavailable { try check("uncertain preparation write never retries",writes.count == 1) }
        writes = []; time = 100
        do {
            let _:Int = try MacVNCUnlockTest.prepareField(find:{ time += 5; return 9 },stillLoginWindow:{ true },send:{ writes.append($0) },isCancelled:{ false },now:{ time })
            throw MacTab5Error.invalidPacket
        } catch is MacTab5LocalError { try check("expired field discovery cannot authorize input",writes.isEmpty) }
        try check("secure field unsupported count selects protected mode",try MacVNCUnlockTest.usesCounter(error:.attributeUnsupported,number:nil) == false)
        try check("readable empty field retains count checks",try MacVNCUnlockTest.usesCounter(error:.success,number:NSNumber(value:0)))
        for error in [AXError.cannotComplete,.apiDisabled,.noValue,.notImplemented] {
            do { _ = try MacVNCUnlockTest.usesCounter(error:error,number:nil); throw MacTab5Error.invalidPacket }
            catch is MacTab5LocalError { checks += 1 }
        }
        for raw:CFTypeRef? in [nil,NSNumber(value:1),kCFBooleanFalse] {
            do { _ = try MacVNCUnlockTest.usesCounter(error:.success,number:raw); throw MacTab5Error.invalidPacket }
            catch is MacTab5LocalError { checks += 1 }
        }
        checks += try validateCountModes()
        writes = []
        try MacVNCUnlockTest.sendFocused(password,ready:{ true },send:{ writes.append($0) },isCancelled:{ false },pause:{})
        try check("protected mode sends only mixed password and one Return",writes.map(code) == [0x41,0x62,0x39,0x21,0xff0d])
        writes = []
        do { try MacVNCUnlockTest.sendFocused(password,ready:{ false },send:{ writes.append($0) },isCancelled:{ false },pause:{}); throw MacTab5Error.invalidPacket }
        catch is MacTab5LocalError { try check("unknown protected-field focus sends no input",writes.isEmpty) }
        do { try MacVNCUnlockTest.sendFocused(password,ready:{ true },send:{ writes.append($0) },isCancelled:{ !writes.isEmpty },pause:{}); throw MacTab5Error.invalidPacket }
        catch is MacTab5LocalError { try check("protected input cancellation never submits partial password",writes.map(code) == [0x41]) }
        writes = []
        do { try MacVNCUnlockTest.sendFocused(password,ready:{ writes.isEmpty },send:{ writes.append($0) },isCancelled:{ false },pause:{}); throw MacTab5Error.invalidPacket }
        catch is MacTab5LocalError { try check("protected input stops after focus loss",writes.map(code) == [0x41]) }
        writes = []
        do { try MacVNCUnlockTest.sendFocused(password,ready:{ true },send:{ writes.append($0); throw MacTab5Error.unavailable },isCancelled:{ false },pause:{}); throw MacTab5Error.invalidPacket }
        catch MacTab5Error.unavailable { try check("protected uncertain write never retries",writes.count == 1) }
        var echo = ""
        writes = []; time = 100
        try MacVNCUnlockTest.verifyKeyboard(value:{ echo },send:{ writes.append($0); echo.append(Character(UnicodeScalar(code($0))!)) },isCancelled:{ false },now:{ time },pause:{ time += 0.1 })
        try check("desktop probe verifies exact case, digits and symbols",echo == MacVNCUnlockTest.keyboardSample && writes.map(code) == MacVNCUnlockTest.keyboardSample.utf8.map(UInt32.init))
        try check("desktop probe never sends Return, Tab, modifiers or password",writes.count == 8 && writes.allSatisfy { $0.count == 16 && $0[0] == 4 && $0[1] == 1 && $0[8] == 4 && $0[9] == 0 && code($0) < 128 })
        writes = []; time = 100
        do {
            try MacVNCUnlockTest.verifyKeyboard(value:{ "" },send:{ writes.append($0) },isCancelled:{ false },now:{ time },pause:{ time += 0.1 })
            throw MacTab5Error.invalidPacket
        } catch is MacTab5LocalError { try check("unresponsive VNC stops at first public probe character",writes.count == 1 && code(writes[0]) == 65) }
        writes = []
        do { try MacVNCUnlockTest.verifyKeyboard(value:{ nil },send:{ writes.append($0) },isCancelled:{ false }); throw MacTab5Error.invalidPacket }
        catch is MacTab5LocalError { try check("lost bridge-owned field focus sends no probe",writes.isEmpty) }
        do { try MacVNCUnlockTest.verifyKeyboard(value:{ writes.isEmpty ? "":nil },send:{ writes.append($0) },isCancelled:{ false }); throw MacTab5Error.invalidPacket }
        catch is MacTab5LocalError { try check("focus lost after first character stops without replay",writes.count == 1 && code(writes[0]) == 65) }
        writes = []
        do { try MacVNCUnlockTest.verifyKeyboard(value:{ throw MacTab5LocalError.invalid("fixture foreground changed") },send:{ writes.append($0) },isCancelled:{ false }); throw MacTab5Error.invalidPacket }
        catch let error as MacTab5LocalError { try check("precise probe-state errors survive verification",writes.isEmpty && error.localizedDescription == "fixture foreground changed") }
        do { try MacVNCUnlockTest.verifyKeyboard(value:{ "" },send:{ writes.append($0) },isCancelled:{ true }); throw MacTab5Error.invalidPacket }
        catch is MacTab5LocalError { try check("cancelled desktop probe sends no input",writes.isEmpty) }
        do {
            try MacVNCUnlockTest.verifyKeyboard(value:{ writes.isEmpty ? "":"a" },send:{ writes.append($0) },isCancelled:{ false })
            throw MacTab5Error.invalidPacket
        } catch let error as MacTab5LocalError { try check("incorrect case reports mapping failure before second character",writes.count == 1 && error.localizedDescription.contains("大小写不匹配")) }
        writes = []
        do {
            try MacVNCUnlockTest.verifyKeyboard(value:{ "" },send:{ writes.append($0); throw MacTab5Error.unavailable },isCancelled:{ false })
            throw MacTab5Error.invalidPacket
        } catch MacTab5Error.unavailable { try check("uncertain desktop probe write never repeats",writes.count == 1) }
        writes = []
        do { try MacVNCUnlockTest.verifyKeyboard(value:{ "unexpected" },send:{ writes.append($0) },isCancelled:{ false }); throw MacTab5Error.invalidPacket }
        catch is MacTab5LocalError { try check("nonempty desktop probe receives no keys",writes.isEmpty) }
        echo = ""; writes = []; time = 100
        var delayedEcho = ""
        try MacVNCUnlockTest.verifyKeyboard(value:{ echo },send:{ writes.append($0); delayedEcho.append(Character(UnicodeScalar(code($0))!)) },isCancelled:{ false },now:{ time },pause:{ time += 0.1; echo = delayedEcho })
        try check("desktop echo accepts asynchronous delivery without key replay",writes.count == 8 && echo == MacVNCUnlockTest.keyboardSample)
        time = 100; var countdown:[String] = []
        try MacVNCUnlockTest.waitForLock(isCancelled:{ false },progress:{ countdown.append($0) },now:{ time },pause:{ time += 1 })
        try check("lock countdown starts only after successful desktop probe",time == 115 && countdown.count == 15 && countdown.first?.contains("15 秒") == true && countdown.last?.contains("1 秒") == true)
        time = 100; countdown = []
        do {
            try MacVNCUnlockTest.waitForLock(isCancelled:{ time >= 102 },progress:{ countdown.append($0) },now:{ time },pause:{ time += 1 })
            throw MacTab5Error.invalidPacket
        } catch is MacTab5LocalError { try check("countdown cancellation aborts before login",time == 102 && countdown.count == 2) }
        checks += try validateDedicatedEditor()
        return checks
    }
    @inline(never)
    private static func validateCancellationRecovery() throws -> Int {
        var checks = 0
        func check(_ name:String,_ condition:@autoclosure () throws -> Bool) throws {
            guard try condition() else { throw MacTab5LocalError.invalid("Unlock test: "+name) }; checks += 1
        }
        for counted in [false,true] {
            var time = 0.0, samples = 0, fields:[String] = [], submits = 0
            try MacVNCUnlockTest.prepareForDirectSubmission(ready:{true},observe:{
                let delivered = samples == 1 && time >= 0.3
                return .init(characters:counted ? (delivered ? 1:0):nil,loginEnabled:delivered)
            },send:{ samples += 1; fields.append("a") },isCancelled:{false},now:{time},pause:{time += 0.05})
            try MacVNCUnlockTest.submitDirectOnce(Data("Fixture9!".utf8),ready:{true},write:{ value in
                fields = [String(decoding:value,as:UTF8.self)] // Full replacement, not append.
            },confirm:{submits += 1},isCancelled:{false})
            try check("delayed printable acknowledgement precedes one full replacement and submission",samples == 1 && time >= 0.45 && fields == ["Fixture9!"] && submits == 1)
        }
        for failure in ["counted-nonempty","no-ack","zero-count","wrong-count","lost-count","new-count","cancel-before","cancel-after","changed","read-before","read-after","slow-before","slow-after","send","lost-ack"] {
            var time = 0.0, samples = 0, credentials = 0, reads = 0
            do {
                try MacVNCUnlockTest.prepareForDirectSubmission(ready:{ failure != "changed" || samples == 0 },observe:{
                    reads += 1
                    if (failure == "read-before" && samples == 0) || (failure == "read-after" && samples > 0) { throw MacTab5Error.unconfirmed }
                    if (failure == "slow-before" && samples == 0) || (failure == "slow-after" && samples > 0) { time += 7 }
                    if samples == 0 {
                        let counted = ["counted-nonempty","zero-count","wrong-count","lost-count"].contains(failure)
                        return .init(characters:counted ? (failure == "counted-nonempty" ? 2:0):nil,loginEnabled:false)
                    }
                    if failure == "zero-count" { return .init(characters:0,loginEnabled:true) }
                    if failure == "wrong-count" { return .init(characters:2,loginEnabled:true) }
                    if failure == "new-count" { return .init(characters:1,loginEnabled:true) }
                    return .init(characters:nil,loginEnabled:failure == "lost-ack" ? reads == 2:failure != "no-ack")
                },send:{ samples += 1; if failure == "send" { throw MacTab5Error.unconfirmed } },
                isCancelled:{ failure == "cancel-before" || (failure == "cancel-after" && samples > 0) },now:{time},pause:{time += 0.05})
                credentials += 1
            } catch {
                // Only the simulated preparation may run, never credential input.
            }
            let rejectedBefore = ["counted-nonempty","cancel-before","read-before","slow-before"].contains(failure)
            try check("unacknowledged or changed preparation never sends credentials or repeats a sample",credentials == 0 && samples == (rejectedBefore ? 0:1))
        }
        for counted in [false,true] {
            var time = 0.0, samples = 0, field = "", writes = 0, submits = 0, cancelled = false
            let observe:() -> MacVNCUnlockTest.PreparationObservation = {
                .init(characters:counted ? field.count:nil,loginEnabled:!field.isEmpty)
            }
            do {
                try MacVNCUnlockTest.prepareForDirectSubmission(ready:{true},observe:observe,
                    send:{samples += 1;field = "a";cancelled = true},isCancelled:{cancelled},now:{time},pause:{time += 0.05})
                throw MacTab5Error.invalidPacket
            } catch is MacTab5LocalError {
                try check("cancel after preparation stops with no credential write or submission",field == "a" && samples == 1 && writes == 0 && submits == 0)
            }
            // A new explicit request, not an automatic retry of the cancelled one.
            cancelled = false
            let usesCount = try MacVNCUnlockTest.usesCounter(error:counted ? .success:.attributeUnsupported,
                number:counted ? NSNumber(value:field.count):nil,replacingExistingInput:true)
            try check("new direct request accepts existing input at the target count gate",usesCount == counted)
            try MacVNCUnlockTest.prepareForDirectSubmission(ready:{true},observe:observe,
                send:{samples += 1;field += "a"},isCancelled:{cancelled},now:{time},pause:{time += 0.05})
            try MacVNCUnlockTest.submitDirectOnce(Data("Fixture9!".utf8),ready:{true},write:{
                writes += 1;field = String(decoding:$0,as:UTF8.self)
            },confirm:{submits += 1},isCancelled:{cancelled})
            try check("new request after cancellation replaces residual sample exactly once",samples == 1 && field == "Fixture9!" && writes == 1 && submits == 1 && time >= 0.15)
        }
        for characters:Int? in [nil,1,9] {
            var time = 0.0, reads = 0, samples = 0
            try MacVNCUnlockTest.prepareForDirectSubmission(ready:{true},observe:{
                reads += 1;return .init(characters:characters,loginEnabled:true)
            },send:{samples += 1},isCancelled:{false},now:{time},pause:{time += 0.05})
            try check("existing ready field stays stable without another printable event",samples == 0 && reads >= 4 && time >= 0.15)
        }
        for failure in ["cancel-before","cancel-read","cancel-wait","focus","button","count","lost-count","new-count","read","slow-read","slow-wait"] {
            var time = 0.0, reads = 0, samples = 0, writes = 0, submits = 0
            do {
                try MacVNCUnlockTest.prepareForDirectSubmission(ready:{failure != "focus" || reads < 2},observe:{
                    reads += 1
                    if reads > 1 && failure == "read" { throw MacTab5Error.unconfirmed }
                    if reads > 1 && failure == "slow-read" { time = 7 }
                    let count:Int? = failure == "new-count" ? (reads == 1 ? nil:1):failure == "lost-count" && reads > 1 ? nil:failure == "count" && reads > 1 ? 2:1
                    return .init(characters:count,loginEnabled:failure != "button" || reads == 1)
                },send:{samples += 1},isCancelled:{failure == "cancel-before" || (failure == "cancel-read" && reads >= 2) || (failure == "cancel-wait" && time > 0)},
                    now:{time},pause:{time += failure == "slow-wait" ? 7:0.05})
                try MacVNCUnlockTest.submitDirectOnce(Data("Fixture9!".utf8),ready:{true},write:{_ in writes += 1},confirm:{submits += 1},isCancelled:{false})
            } catch { }
            try check("changed or cancelled existing field never posts input or submits",samples == 0 && writes == 0 && submits == 0)
        }
        return checks
    }
    @inline(never)
    private static func validateCountModes() throws -> Int {
        var checks = 0
        func check(_ name:String,_ condition:@autoclosure () throws -> Bool) throws {
            guard try condition() else { throw MacTab5LocalError.invalid("Unlock test: "+name) }; checks += 1
        }
        for replacing in [false,true] {
            try check("unsupported count remains optional for either input mode",try !MacVNCUnlockTest.usesCounter(error:.attributeUnsupported,number:nil,replacingExistingInput:replacing))
            for error in [AXError.cannotComplete,.apiDisabled,.noValue,.notImplemented] {
                do { _ = try MacVNCUnlockTest.usesCounter(error:error,number:NSNumber(value:0),replacingExistingInput:replacing); throw MacTab5Error.invalidPacket }
                catch is MacTab5LocalError { checks += 1 }
            }
            for raw:CFTypeRef? in [nil,kCFBooleanFalse,kCFBooleanTrue,"1" as CFString,NSNumber(value:-1),NSNumber(value:0.5),NSNumber(value:Double.nan),NSNumber(value:Double.infinity),NSNumber(value:Double(Int.max))] {
                do { _ = try MacVNCUnlockTest.usesCounter(error:.success,number:raw,replacingExistingInput:replacing); throw MacTab5Error.invalidPacket }
                catch is MacTab5LocalError { checks += 1 }
            }
            for count in [0,1,9,129] {
                do {
                    let counted = try MacVNCUnlockTest.usesCounter(error:.success,number:NSNumber(value:count),replacingExistingInput:replacing)
                    try check("only full replacement accepts a nonempty count",counted && (replacing || count == 0))
                } catch is MacTab5LocalError {
                    try check("keyboard and probe routes still require an empty field",!replacing && count > 0)
                }
            }
        }
        return checks
    }
    private static func validateDedicatedEditor() throws -> Int {
        _ = NSApplication.shared
        let window = NSWindow(contentRect:NSRect(x:0,y:0,width:400,height:150),styleMask:[.titled],backing:.buffered,defer:false)
        window.isReleasedWhenClosed = false
        let credential = NSSecureTextField(frame:NSRect(x:10,y:90,width:360,height:28))
        let probe = MacVNCKeyboardProbe(), field = probe.field
        field.frame = NSRect(x:10,y:30,width:360,height:28)
        window.contentView!.addSubview(credential); window.contentView!.addSubview(field)
        defer { field.string = ""; credential.stringValue = ""; window.close() }
        guard window.makeFirstResponder(credential),credential.currentEditor() != nil else { throw MacTab5LocalError.invalid("Probe fixture credential editor unavailable") }
        field.isSelectable = true; field.isEditable = true
        guard window.makeFirstResponder(field),window.firstResponder === field,!field.isFieldEditor,
              field.allowedInputSourceLocales == [NSAllRomanInputSourcesLocaleIdentifier] else { throw MacTab5LocalError.invalid("Probe fixture editor ownership") }
        var checks = 1, expected = ""
        let keyCodes:[UInt16] = [0,11,25,18,19,27,27,24]
        for (index,character) in MacVNCUnlockTest.keyboardSample.enumerated() {
            let text = String(character), shifted = "A!@_+".contains(character)
            // Direct invocation on a hidden fixture view, never OS input or a
            // real credential field. AppKit performs the text interpretation.
            let event = NSEvent.keyEvent(with:.keyDown,location:.zero,modifierFlags:shifted ? [.shift]:[],timestamp:0,windowNumber:window.windowNumber,context:nil,characters:text,charactersIgnoringModifiers:text,isARepeat:false,keyCode:keyCodes[index])!
            field.keyDown(with:event); expected.append(character)
            guard window.firstResponder === field,field.string == expected else { throw MacTab5LocalError.invalid("Probe fixture loses editor or exact public text") }
            checks += 1
        }
        guard credential.currentEditor() == nil,field.string == MacVNCUnlockTest.keyboardSample,!field.allowsUndo else { throw MacTab5Error.invalidPacket }
        return checks+1
    }
}
