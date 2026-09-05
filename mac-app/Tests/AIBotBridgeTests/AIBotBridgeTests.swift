import AppKit
import XCTest
@testable import AIBotBridge

final class AIBotBridgeTests: XCTestCase {
    func testActivityClassification() {
        XCTAssertEqual(SessionActivityReader.classify(ageSeconds: 89), "working")
        XCTAssertEqual(SessionActivityReader.classify(ageSeconds: 90), "idle")
        XCTAssertEqual(SessionActivityReader.classify(ageSeconds: 900), "idle")
        XCTAssertEqual(SessionActivityReader.classify(ageSeconds: 901), "offline")
    }

    func testPairingComparison() {
        XCTAssertTrue(HTTPStatusServer.constantTimeEqual(String(repeating: "a", count: 32), String(repeating: "a", count: 32)))
        XCTAssertFalse(HTTPStatusServer.constantTimeEqual(String(repeating: "a", count: 32), String(repeating: "b", count: 32)))
        XCTAssertFalse(HTTPStatusServer.constantTimeEqual("short", String(repeating: "a", count: 32)))
    }

    func testWeatherAndStockParsing() throws {
        let forecast = Data(#"{"current":{"temperature_2m":26.5,"relative_humidity_2m":58,"weather_code":2},"daily":{"temperature_2m_max":[31.0],"temperature_2m_min":[19.0]}}"#.utf8)
        let air = Data(#"{"current":{"us_aqi":46,"pm2_5":13.2}}"#.utf8)
        let weather = try MacDataService.parseWeather(forecast: forecast, air: air, city: "北京")
        XCTAssertEqual(weather.condition, "多云")
        XCTAssertEqual(weather.airQualityIndex, 46)

        let quotes = MacDataService.parseStocks(
            "v_sh000001=\"1~上证指数~000001~3821.44~~~~~~~~~~~~~~~~~~~~~~~~~~~~32.20~0.85~\";",
            order: ["sh000001"])
        XCTAssertEqual(quotes.first?.name, "上证指数")
        XCTAssertEqual(quotes.first?.changePercent, "+0.85%")
    }

    func testSystemMetricDeltas() {
        let previous = MacCpuTicks(user: 100, system: 50, idle: 850, nice: 0)
        let current = MacCpuTicks(user: 120, system: 60, idle: 920, nice: 0)
        XCTAssertEqual(MacSystemMetricsService.cpuPercent(previous: previous, current: current), 30,
                       accuracy: 0.001)
        XCTAssertEqual(MacSystemMetricsService.rate(previous: 1_000, current: 4_000, elapsed: 2), 1_500)
        XCTAssertEqual(MacSystemMetricsService.rate(previous: 4_000, current: 1_000, elapsed: 2), 0)
    }

    func testQuotaParsing() throws {
        let claude = try MacQuotaService.parseClaude(Data(#"{"plan_type":"max","five_hour":{"utilization":25,"resets_at":"2026-09-05T12:00:00Z"},"seven_day":{"utilization":40,"resets_at":"2026-09-09T12:00:00Z"}}"#.utf8))
        XCTAssertEqual(claude.primaryPercent, 25)
        XCTAssertEqual(claude.weeklyPercent, 40)

        let codex = try MacQuotaService.parseCodex(Data(#"{"plan_type":"plus","rate_limit":{"primary_window":{"limit_window_seconds":18000,"used_percent":12,"reset_at":1788600000},"secondary_window":{"limit_window_seconds":604800,"used_percent":34,"reset_at":1789000000}}}"#.utf8))
        XCTAssertEqual(codex.primaryPercent, 12)
        XCTAssertEqual(codex.weeklyPercent, 34)
    }

    func testSerialCandidateFiltering() {
        XCTAssertTrue(SerialBridge.isCandidateDeviceName("cu.wchusbserial1420"))
        XCTAssertTrue(SerialBridge.isCandidateDeviceName("cu.usbserial-110"))
        XCTAssertTrue(SerialBridge.isCandidateDeviceName("cu.SLAB_USBtoUART"))
        XCTAssertFalse(SerialBridge.isCandidateDeviceName("tty.Bluetooth-Incoming-Port"))
        XCTAssertFalse(SerialBridge.isCandidateDeviceName("cu.not-a-device"))
        XCTAssertTrue(SerialBridge.displayModes.contains("screensaver"))
        XCTAssertTrue(SerialBridge.displayModes.contains("pet"))
        XCTAssertFalse(SerialBridge.displayModes.contains("unknown"))
    }

    func testSerialStatusFrameUsesProtocolPrefix() throws {
        let snapshot = MacStatusSnapshot(
            version: 1, time: "12:34:56", epochUtc: 1_788_500_000, utcOffsetSeconds: 28_800,
            capturedAt: Date(timeIntervalSince1970: 1_788_500_000),
            codex: ToolState(state: "working", ageSeconds: 2),
            claude: ToolState(state: "idle", ageSeconds: 180), musicPlaying: nil,
            weather: nil, stocks: nil, systemMetrics: nil, quotas: nil, music: nil)
        let frame = try XCTUnwrap(SerialBridge.statusFrame(snapshot))
        XCTAssertTrue(String(decoding: frame, as: UTF8.self).hasPrefix("@AIBOT "))
        let payload = frame.dropFirst("@AIBOT ".utf8.count).dropLast()
        let root = try XCTUnwrap(JSONSerialization.jsonObject(with: Data(payload)) as? [String: Any])
        XCTAssertEqual(root["version"] as? Int, 1)
        XCTAssertEqual(root["type"] as? String, "status")
        XCTAssertNotNil(root["data"] as? [String: Any])
        XCTAssertLessThanOrEqual(frame.count, 6_144)
    }

    func testAutomaticScreenSaverEntryAndRestore() {
        var state = AutomaticScreenSaverState()
        XCTAssertNil(state.desiredMode(idleSeconds: 600, timeoutMinutes: 0))
        XCTAssertEqual(state.desiredMode(idleSeconds: 300, timeoutMinutes: 5), "screensaver")
        state.confirm("screensaver", sent: false)
        XCTAssertFalse(state.active)
        state.confirm("screensaver", sent: true)
        XCTAssertTrue(state.active)
        XCTAssertEqual(state.desiredMode(idleSeconds: 2, timeoutMinutes: 5), "auto")
        state.confirm("auto", sent: true)
        XCTAssertFalse(state.active)

        state.select("weather")
        XCTAssertEqual(state.desiredMode(idleSeconds: 301, timeoutMinutes: 5), "screensaver")
        state.select("screensaver")
        XCTAssertNil(state.desiredMode(idleSeconds: 0, timeoutMinutes: 5))
    }

    func testAutomaticScreenSaverTemporaryAiAndMusicWake() {
        let start = Date(timeIntervalSince1970: 1_788_500_000)
        var state = AutomaticScreenSaverState()
        XCTAssertEqual(state.desiredMode(idleSeconds: 300, timeoutMinutes: 5,
                                         now: start), "screensaver")
        state.confirm("screensaver", sent: true, now: start)

        XCTAssertEqual(state.desiredMode(idleSeconds: 310, timeoutMinutes: 5,
                                         aiWorking: true, now: start), "pet")
        state.confirm("pet", sent: true, now: start)
        XCTAssertTrue(state.active)
        XCTAssertNil(state.desiredMode(idleSeconds: 311, timeoutMinutes: 5,
                                       aiWorking: true, now: start.addingTimeInterval(11)))
        XCTAssertEqual(state.desiredMode(idleSeconds: 312, timeoutMinutes: 5,
                                         aiWorking: true, now: start.addingTimeInterval(12)),
                       "screensaver")
        state.confirm("screensaver", sent: true, now: start.addingTimeInterval(12))

        XCTAssertEqual(state.desiredMode(idleSeconds: 314, timeoutMinutes: 5,
                                         aiWorking: false, musicPlaying: true,
                                         now: start.addingTimeInterval(14)), "music")
        state.confirm("music", sent: true, now: start.addingTimeInterval(14))
        XCTAssertTrue(state.active)
        XCTAssertEqual(state.desiredMode(idleSeconds: 1, timeoutMinutes: 5,
                                         musicPlaying: true, now: start.addingTimeInterval(15)), "auto")
        state.confirm("auto", sent: true, now: start.addingTimeInterval(15))
        XCTAssertFalse(state.active)
    }

    func testAutomaticScreenSaverRetriesFailedRestore() {
        var state = AutomaticScreenSaverState()
        XCTAssertEqual(state.desiredMode(idleSeconds: 300, timeoutMinutes: 5), "screensaver")
        state.confirm("screensaver", sent: true)
        XCTAssertEqual(state.desiredMode(idleSeconds: 1, timeoutMinutes: 5), "auto")
        state.confirm("auto", sent: false)
        XCTAssertTrue(state.active)
        XCTAssertEqual(state.desiredMode(idleSeconds: 1, timeoutMinutes: 5), "auto")
    }

    func testBinaryResourceRoundTripAndChecksum() throws {
        let source = Data((0..<1_537).map { UInt8(truncatingIfNeeded: $0) })
        let chunks = try MacBinaryResourceProtocol.createChunks(
            kind: .petAsset, data: source, transferId: 0x1234_5678)
        XCTAssertEqual(chunks.count, 3)
        XCTAssertEqual(chunks[0].wireBytes.first, 0)
        XCTAssertEqual(chunks[0].wireBytes.last, 0)
        XCTAssertFalse(chunks[0].wireBytes.dropFirst().dropLast().contains(0))
        let decoded = try chunks.map { try MacBinaryResourceProtocol.decodeWire($0.wireBytes) }
        var restored = Data()
        decoded.forEach { restored.append($0.payload) }
        XCTAssertEqual(restored, source)
        XCTAssertEqual(decoded.first?.transferId, 0x1234_5678)
        XCTAssertEqual(decoded.last?.sequence, 2)
        XCTAssertEqual(decoded.last?.totalChunks, 3)
        XCTAssertEqual(MacBinaryResourceProtocol.crc32(Array("123456789".utf8)), 0xCBF4_3926)
    }

    func testBinaryResourceRejectsCorruption() throws {
        let chunk = try XCTUnwrap(try MacBinaryResourceProtocol.createChunks(
            kind: .weatherText, data: Data([1, 0, 2, 3]), transferId: 7).first)
        var corrupted = chunk.wireBytes
        corrupted[2] ^= 0x01
        XCTAssertThrowsError(try MacBinaryResourceProtocol.decodeWire(corrupted))
    }

    func testLocalizedTextRgb565Dimensions() throws {
        XCTAssertEqual(MacRgb565Renderer.rgb565(red: 255, green: 0, blue: 0), 0xF800)
        let weather = try XCTUnwrap(MacRgb565Renderer.renderLines(
            width: 232, height: 24, lines: ["北京  多云"], fontPixels: 20, rowHeight: 24))
        let stocks = try XCTUnwrap(MacRgb565Renderer.renderLines(
            width: 120, height: 400, lines: ["上证指数", "腾讯控股"],
            fontPixels: 18, rowHeight: 20))
        XCTAssertEqual(weather.count, 232 * 24 * 2)
        XCTAssertEqual(stocks.count, 120 * 400 * 2)
        XCTAssertTrue(weather.contains { $0 != 0 })
        XCTAssertTrue(stocks.contains { $0 != 0 })
    }

    func testMusicParsingStatusAndTextResource() throws {
        let now = Date(timeIntervalSince1970: 1_788_500_000)
        let music = try XCTUnwrap(MacMusicService.parse(
            values: ["夜空中最亮的星", "逃跑计划", "世界", "true", "95", "260"], now: now))
        XCTAssertTrue(music.playing)
        XCTAssertEqual(music.elapsedSeconds, 95)
        XCTAssertEqual(music.durationSeconds, 260)

        let suite = "AI-bot-tests-" + UUID().uuidString
        let defaults = try XCTUnwrap(UserDefaults(suiteName: suite))
        defer { defaults.removePersistentDomain(forName: suite) }
        let store = MacDataStore(defaults: defaults)
        store.update(music: music)
        let snapshot = SessionActivityReader().capture(extras: store.snapshot())
        XCTAssertEqual(snapshot.music?.title, "夜空中最亮的星")
        XCTAssertEqual(snapshot.musicPlaying, true)

        let resources = MacLocalizedTextResources().capture(
            weather: nil, stocks: nil, music: music, musicCover: nil)
        let text = try XCTUnwrap(resources.first { $0.kind == .textBitmap })
        let cover = try XCTUnwrap(resources.first { $0.kind == .musicCover })
        XCTAssertEqual(text.data.count, 232 * 44 * 2)
        XCTAssertTrue(text.data.contains { $0 != 0 })
        XCTAssertEqual(cover.data, Data(repeating: 0, count: 112 * 112 * 2))
    }

    func testMusicParsingClampsInvalidProgress() throws {
        let value = try XCTUnwrap(MacMusicService.parse(
            values: ["Track", "Artist", "Album", "false", "400", "240"]))
        XCTAssertFalse(value.playing)
        XCTAssertEqual(value.elapsedSeconds, 240)
        XCTAssertNil(MacMusicService.parse(
            values: ["Track", "Artist", "Album", "true", "nan", "240"]))

        let bounded = try XCTUnwrap(MacMusicService.parse(
            values: [String(repeating: "曲", count: 200) + "\n第二行", "Artist", "Album",
                     "true", "1", "2"]))
        XCTAssertEqual(bounded.title.count, 96)
        XCTAssertFalse(bounded.title.contains("\n"))
    }

    func testMusicArtworkDecodeAndScale() throws {
        let bitmap = try XCTUnwrap(NSBitmapImageRep(
            bitmapDataPlanes: nil, pixelsWide: 2, pixelsHigh: 1,
            bitsPerSample: 8, samplesPerPixel: 4, hasAlpha: true, isPlanar: false,
            colorSpaceName: .deviceRGB, bytesPerRow: 8, bitsPerPixel: 32))
        bitmap.setColor(.red, atX: 0, y: 0)
        bitmap.setColor(.blue, atX: 1, y: 0)
        let png = try XCTUnwrap(bitmap.representation(using: .png, properties: [:]))
        let cover = try XCTUnwrap(MacMusicService.renderArtwork(png))
        XCTAssertEqual(cover.count, 112 * 112 * 2)
        XCTAssertTrue(cover.contains { $0 != 0 })
        XCTAssertNil(MacMusicService.renderArtwork(Data([0, 1, 2, 3])))
        XCTAssertTrue(MacMusicService.allowedSpotifyArtworkURL(
            try XCTUnwrap(URL(string: "https://i.scdn.co/image/test"))))
        XCTAssertTrue(MacMusicService.allowedSpotifyArtworkURL(
            try XCTUnwrap(URL(string: "https://image-cdn-ak.spotifycdn.com/image/test"))))
        XCTAssertFalse(MacMusicService.allowedSpotifyArtworkURL(
            try XCTUnwrap(URL(string: "https://example.com/image/test"))))
        XCTAssertFalse(MacMusicService.allowedSpotifyArtworkURL(
            try XCTUnwrap(URL(string: "http://i.scdn.co/image/test"))))
    }

    func testPetAssetLicenseGateAndDimensions() throws {
        XCTAssertTrue(MacPetAssetImporter.validDimensions(width: 1, height: 4_096))
        XCTAssertFalse(MacPetAssetImporter.validDimensions(width: 0, height: 10))
        XCTAssertFalse(MacPetAssetImporter.validDimensions(width: 4_097, height: 10))

        let directory = FileManager.default.temporaryDirectory
            .appendingPathComponent("AI-bot-pet-test-" + UUID().uuidString, isDirectory: true)
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: false)
        defer { try? FileManager.default.removeItem(at: directory) }
        let image = directory.appendingPathComponent("pet.png")
        try Data([1]).write(to: image)
        XCTAssertNil(MacPetAssetImporter.licenseFile(for: image))
        let notice = directory.appendingPathComponent("pet.license.txt")
        try Data("CC0 test notice".utf8).write(to: notice)
        XCTAssertEqual(MacPetAssetImporter.licenseFile(for: image), notice)
    }
}
