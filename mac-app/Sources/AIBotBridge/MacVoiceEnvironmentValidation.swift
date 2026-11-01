import Foundation

enum MacVoiceEnvironmentValidation {
    static func run() throws -> Int {
        var count = 0
        func check(_ label: String, _ value: @autoclosure () -> Bool) throws {
            guard value() else { throw MacTab5LocalError.invalid("Voice availability: " + label) }; count += 1
        }
        var env = MacVoiceEnvironment(inputs: [.init(id: 1, uid: "mic", name: "Built-in microphone"), .init(id: 2, uid: "dji", name: "DJI Mini2"), .init(id: 3, uid: "com.vbaudio.vbcable:1", name: "VB-CABLE")], defaultInput: 1,
            doubaoInstalled: true, doubaoEnabled: true, doubaoRunning: true, cable: true, speechPermission: true, microphonePermission: true, localRecognition: true)
        let local = MacVoiceSettings(provider: "local", source: .mac, input: "mic")
        var doubao = MacVoiceSettings(provider: "doubao", source: .mac, input: "dji")
        try check("local ready", env.issue(for: local) == nil)
        try check("doubao ready", env.issue(for: doubao) == nil)
        env.doubaoRunning = false
        try check("stopped service is accurately described without false readiness claim", env.doubaoStatus.contains("尚未运行") && env.issue(for: doubao) == nil)
        env.doubaoEnabled = false
        try check("installed but disabled distinct", env.doubaoStatus.contains("尚未在系统键盘设置中启用") && env.issue(for: doubao) != nil)
        env.doubaoInstalled = false
        try check("missing installation distinct", env.doubaoStatus.contains("未安装") && env.issue(for: doubao) != nil)
        env.doubaoInstalled = true; env.doubaoEnabled = true
        doubao.input = "removed"
        try check("explicit disconnected microphone never falls back", env.issue(for: doubao)?.contains("断开") == true)
        doubao.input = "com.vbaudio.vbcable:1"
        try check("virtual endpoint not used as physical Doubao microphone", env.issue(for: doubao) != nil)
        doubao.source = .tab5; doubao.input = "dji"
        try check("tab5 uses virtual route", env.issue(for: doubao) == nil)
        env.cable = false
        try check("missing cable rejected for tab5", env.issue(for: doubao)?.contains("VB-CABLE") == true)
        env.speechPermission = false
        try check("local speech permission explicit", env.issue(for: local)?.contains("本地识别尚未授权") == true)
        env.speechPermission = true; env.microphonePermission = false
        try check("physical microphone permission explicit", env.issue(for: local)?.contains("麦克风尚未授权") == true)
        try check("local tab5 needs no physical capture permission", env.issue(for: .init(provider: "local", source: .tab5, input: "mic")) == nil)
        env.localRecognition = false
        try check("local service/assets unavailable explicit", env.issue(for: local)?.contains("本地识别暂不可用") == true)
        try check("invalid provider rejected", env.issue(for: .init(provider: "unknown", source: .mac, input: "")) != nil)
        try check("invalid microphone metadata rejected", env.issue(for: .init(provider: "doubao", source: .mac, input: "bad\nuid")) != nil)
        return count
    }
}
