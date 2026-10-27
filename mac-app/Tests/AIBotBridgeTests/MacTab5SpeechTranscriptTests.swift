import XCTest
@testable import AIBotBridge

final class MacTab5SpeechTranscriptTests: XCTestCase {
    func testPauseKeepsEarlierUtteranceWhileSecondHypothesisChanges() {
        var text = MacTab5SpeechTranscript()
        XCTAssertEqual(text.update("第一段", start: 0.4, end: 2.2), "第一段")
        XCTAssertEqual(text.update("第二", start: 5.3, end: 5.8), "第一段第二")
        XCTAssertEqual(text.update("第二段", start: 5.3, end: 6.5), "第一段第二段")
        XCTAssertEqual(text.update("", start: nil, end: nil), "第一段第二段")
    }
    func testCorrectionsReplaceTheSameAudioInsteadOfAppendingWords() {
        var text = MacTab5SpeechTranscript()
        _ = text.update("先检查常用城序", start: 0.4, end: 2.5)
        XCTAssertEqual(text.update("先检查常用程序", start: 0.45, end: 2.6), "先检查常用程序")
        XCTAssertEqual(text.update("先检查程序", start: 0.45, end: 2.4), "先检查程序")
    }
    func testCumulativeRecoveryReplacesCoveredUtterancesWithoutDuplication() {
        var text = MacTab5SpeechTranscript()
        _ = text.update("第一段", start: 0.5, end: 2)
        _ = text.update("第二段", start: 5, end: 7)
        XCTAssertEqual(text.update("第一段，第二段已修正", start: 0.5, end: 7.2), "第一段，第二段已修正")
        XCTAssertEqual(text.update("第三段", start: 10, end: 12), "第一段，第二段已修正第三段")
    }
    func testRepeatedWordsAtDifferentAudioPositionsRemainRepeated() {
        var text = MacTab5SpeechTranscript()
        _ = text.update("测试", start: 1, end: 2)
        XCTAssertEqual(text.update("测试", start: 5, end: 6), "测试测试")
    }
    func testAdjacentUtteranceCorrectionCannotRemovePreviousPart() {
        var text = MacTab5SpeechTranscript()
        _ = text.update("第一段", start: 0, end: 2)
        _ = text.update("第二", start: 2.01, end: 3)
        XCTAssertEqual(text.update("第二段", start: 2.02, end: 3.5), "第一段第二段")
    }
    func testTimingUnavailableKeepsProvisionalTextSeparateFromTimedUtterances() {
        var text = MacTab5SpeechTranscript()
        _ = text.update("无时间", start: nil, end: nil)
        XCTAssertEqual(text.update("无时间的修正", start: nil, end: nil), "无时间的修正")
        _ = text.update("有时间", start: 1, end: 2)
        XCTAssertEqual(text.update("临时文字", start: .nan, end: 2), "有时间临时文字")
        XCTAssertEqual(text.update("临时文字修正", start: 3, end: 2), "有时间临时文字修正")
        XCTAssertEqual(text.utteranceCount, 1)
    }
    func testZeroTimingPartialsCannotProduceSecondFirstSecondOrder() {
        var text = MacTab5SpeechTranscript()
        _ = text.update("这是第一段", start: 0, end: 0)
        _ = text.update("这是第一段测试", start: 0, end: 0)
        XCTAssertEqual(text.update("这是第一段测试", start: 0.5, end: 3), "这是第一段测试")
        XCTAssertEqual(text.update("这是第二段", start: 0, end: 0), "这是第一段测试这是第二段")
        XCTAssertEqual(text.update("这是第二段测试", start: 0, end: 0), "这是第一段测试这是第二段测试")
        XCTAssertEqual(text.update("这是第二段测试", start: 7, end: 10), "这是第一段测试这是第二段测试")
        XCTAssertEqual(text.update("这是第二段测试", start: 7, end: 10), "这是第一段测试这是第二段测试")
        XCTAssertEqual(text.utteranceCount, 2)
    }
    func testUntimedCopiesAndCumulativeRecoveryDoNotDuplicateStableText() {
        var text = MacTab5SpeechTranscript()
        _ = text.update("第一段", start: 1, end: 3)
        XCTAssertEqual(text.update("第一", start: 0, end: 0), "第一段")
        XCTAssertEqual(text.update("第一段修正", start: 0, end: 0), "第一段修正")
        _ = text.update("第一段修正", start: 1, end: 3)
        _ = text.update("第二段", start: 6, end: 8)
        XCTAssertEqual(text.update("第一段修正，第二段，还有第三段", start: 0, end: 0), "第一段修正，第二段，还有第三段")
        XCTAssertEqual(text.update("第一段修正，第二段，还有第三段", start: 1, end: 12), "第一段修正，第二段，还有第三段")
    }
    func testUntimedRepeatedUtteranceIsRetainedOnceItsOwnPositionArrives() {
        var text = MacTab5SpeechTranscript()
        _ = text.update("测试", start: 1, end: 2)
        _ = text.update("测试", start: 0, end: 0)
        XCTAssertEqual(text.update("测试", start: 5, end: 6), "测试测试")
    }
    func testResetStartsAnIndependentRecording() {
        var text = MacTab5SpeechTranscript()
        _ = text.update("上一轮", start: 0.5, end: 2)
        text = MacTab5SpeechTranscript()
        XCTAssertEqual(text.update("下一轮", start: 0.5, end: 2), "下一轮")
    }
    func testRecognizedLineSeparatorsStayInOneParagraph() {
        var text = MacTab5SpeechTranscript()
        XCTAssertEqual(text.update("第一句\r\n第二句\u{2028}第三句\n\n", start: 0.5, end: 4), "第一句第二句第三句")
        XCTAssertEqual(text.update("第四句", start: 7, end: 8), "第一句第二句第三句第四句")
    }
    func testEnglishPauseKeepsWordBoundariesWithoutLineBreaks() {
        var text = MacTab5SpeechTranscript()
        _ = text.update("hello", start: 0.5, end: 1)
        XCTAssertEqual(text.update("world", start: 4, end: 5), "hello world")
        XCTAssertEqual(text.update("world again", start: 4, end: 6), "hello world again")
    }
    func testEnglishSentenceBoundaryIsOneParagraph() {
        var text = MacTab5SpeechTranscript()
        _ = text.update("hello.", start: 0.5, end: 1)
        XCTAssertEqual(text.update("world.", start: 4, end: 5), "hello. world.")
    }
    func testNormalizedProvisionalAndCumulativeCopiesDoNotDuplicate() {
        var text = MacTab5SpeechTranscript()
        XCTAssertEqual(text.update("第一句\n第二句", start: 0.5, end: 3), "第一句第二句")
        XCTAssertEqual(text.update("第一句第二句", start: 0, end: 0), "第一句第二句")
        XCTAssertEqual(text.update("第三句", start: 6, end: 7), "第一句第二句第三句")
        XCTAssertEqual(text.update("\r\n \t ", start: 0, end: 0), "第一句第二句第三句")
    }

}
