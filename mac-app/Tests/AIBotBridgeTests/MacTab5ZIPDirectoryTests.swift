import XCTest
@testable import AIBotBridge

final class MacTab5ZIPDirectoryTests: XCTestCase {
    func testSmallZIP64DirectoryAndMalformedVolumeAreHandledWithoutLargeFiles() throws {
        func put(_ value: UInt64, _ bytes: inout Data, _ at: Int, _ count: Int) { for index in 0..<count { bytes[at + index] = UInt8(truncatingIfNeeded: value >> (index * 8)) } }
        let name = Data("safe.txt".utf8)
        var local = Data(repeating: 0, count: 30); put(0x04034b50, &local, 0, 4); put(45, &local, 4, 2); put(UInt64(name.count), &local, 26, 2); local.append(name)
        var central = Data(repeating: 0, count: 46); put(0x02014b50, &central, 0, 4); put(45, &central, 4, 2); put(45, &central, 6, 2); put(UInt64(name.count), &central, 28, 2); central.append(name)
        var record = Data(repeating: 0, count: 56); put(0x06064b50, &record, 0, 4); put(44, &record, 4, 8); put(45, &record, 12, 2); put(45, &record, 14, 2)
        put(1, &record, 24, 8); put(1, &record, 32, 8); put(UInt64(central.count), &record, 40, 8); put(UInt64(local.count), &record, 48, 8)
        var locator = Data(repeating: 0, count: 20); put(0x07064b50, &locator, 0, 4); put(UInt64(local.count + central.count), &locator, 8, 8); put(1, &locator, 16, 4)
        var end = Data(repeating: 0, count: 22); put(0x06054b50, &end, 0, 4); put(0xffff, &end, 8, 2); put(0xffff, &end, 10, 2); put(0xffffffff, &end, 12, 4); put(0xffffffff, &end, 16, 4)
        let url = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString + ".zip"); defer { try? FileManager.default.removeItem(at: url) }
        try (local + central + record + locator + end).write(to: url)
        XCTAssertEqual(try MacTab5ZIPDirectory.names(url, maximumEntries: 5), ["safe.txt"])
        XCTAssertThrowsError(try MacTab5ZIPDirectory.names(url, maximumEntries: 0))
        put(2, &locator, 16, 4); try (local + central + record + locator + end).write(to: url)
        XCTAssertThrowsError(try MacTab5ZIPDirectory.names(url, maximumEntries: 5))
        try Data([1,2,3]).write(to: url); XCTAssertThrowsError(try MacTab5ZIPDirectory.names(url, maximumEntries: 5))
    }
}
