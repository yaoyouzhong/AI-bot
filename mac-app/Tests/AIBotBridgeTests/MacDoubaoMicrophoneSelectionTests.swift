import XCTest
@testable import AIBotBridge

final class MacDoubaoMicrophoneSelectionTests: XCTestCase {
    private let dji = MacAudioInput(id: 2, uid: "dji-input", name: "Wireless Mic Rx")
    private let cable = MacAudioInput(id: 3, uid: "com.vbaudio.vbcable:test", name: "VB-CABLE")
    private let automatic = "ime_auto_detect_microphone_id"

    func testMatchingExplicitInputNeedsNoDefaultOrSwitch() {
        XCTAssertTrue(MacDoubaoMicrophone.reusesSelection(dji.uid, device: dji, defaultInput: nil))
    }

    func testAutomaticMatchingDefaultPhysicalInputIsReused() {
        XCTAssertTrue(MacDoubaoMicrophone.reusesSelection(automatic, device: dji, defaultInput: dji.id))
    }

    func testOtherOrUnknownDefaultKeepsVerifiedTemporarySelection() {
        XCTAssertFalse(MacDoubaoMicrophone.reusesSelection(automatic, device: dji, defaultInput: 1))
        XCTAssertFalse(MacDoubaoMicrophone.reusesSelection(automatic, device: dji, defaultInput: nil))
        XCTAssertFalse(MacDoubaoMicrophone.reusesSelection("different-input", device: dji, defaultInput: dji.id))
        XCTAssertFalse(MacDoubaoMicrophone.reusesSelection("unknown-automatic-mode", device: dji, defaultInput: dji.id))
    }

    func testTab5VirtualInputStillRequiresExplicitSelection() {
        XCTAssertFalse(MacDoubaoMicrophone.reusesSelection(automatic, device: cable, defaultInput: cable.id))
        XCTAssertTrue(MacDoubaoMicrophone.reusesSelection(cable.uid, device: cable, defaultInput: nil))
    }
}
