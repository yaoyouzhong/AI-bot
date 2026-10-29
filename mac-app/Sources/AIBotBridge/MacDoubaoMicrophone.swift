import AppKit
import CoreAudio

enum MacDoubaoSettingsVisibility {
    static func visible(pid:pid_t,windows:[[String:Any]]) -> Bool {
        windows.contains {
            ($0[kCGWindowOwnerPID as String] as? NSNumber)?.int32Value == pid &&
            ($0[kCGWindowIsOnscreen as String] as? Bool) == true &&
            ($0[kCGWindowLayer as String] as? NSNumber)?.intValue == 0 &&
            (($0[kCGWindowAlpha as String] as? NSNumber)?.doubleValue ?? 0) > 0
        }
    }
}

/// The microphone lease updates only the verified local preference through
/// MMKV's inter-process lock, then notifies the installed input method. Actual
/// input IO remains a separate requirement before forwarding TAB5 audio.
final class MacDoubaoMicrophone {
    private static let queue=DispatchQueue(label:"AI-bot.doubao-microphone")
    private static let journal="doubao_microphone_restore_v1"
    private static let statusLock=NSLock()
    private static var status=""
    static var restorationStatus:String {
        statusLock.lock();defer {statusLock.unlock()}
        if !status.isEmpty {return status}
        return UserDefaults.standard.dictionary(forKey:journal)==nil ? "":"豆包麦克风存在待恢复记录，请点击“恢复豆包麦克风”。"
    }
    private static func record(_ value:String) {statusLock.lock();status=value;statusLock.unlock()}
    private static func verify() throws {
        guard MacDoubaoSpeech.installed(),Bundle(path:MacDoubaoBackground.path)?.object(forInfoDictionaryKey:"CFBundleShortVersionString") as? String == "1.0.3" else {
            throw MacTab5LocalError.invalid("当前豆包版本尚未适配后台麦克风切换，未打开设置页或开始录音。")
        }
    }
    static func physical() throws -> MacAudioInput {
        let devices = MacAudioInputs.devices().filter { !$0.uid.hasPrefix("com.vbaudio.vbcable:") && $0.uid != "BlackHole2ch_UID" }
        if let uid = UserDefaults.standard.string(forKey:MacAudioInputs.preference),!uid.isEmpty {
            guard let selected = devices.first(where:{$0.uid == uid}) else { throw MacTab5LocalError.invalid("所选麦克风未连接，未改用其他麦克风。") }; return selected
        }
        let dji = devices.filter { $0.name.localizedCaseInsensitiveContains("DJI") || $0.name.contains("大疆") }
        if dji.count == 1 { return dji[0] }
        var property = AudioObjectPropertyAddress(mSelector:kAudioHardwarePropertyDefaultInputDevice,mScope:kAudioObjectPropertyScopeGlobal,mElement:kAudioObjectPropertyElementMain)
        var id = AudioDeviceID(0), size = UInt32(MemoryLayout<AudioDeviceID>.size)
        guard AudioObjectGetPropertyData(AudioObjectID(kAudioObjectSystemObject),&property,0,nil,&size,&id) == noErr,let selected = devices.first(where:{$0.id == id}) else { throw MacTab5LocalError.invalid("未找到可用的实体麦克风。") }
        return selected
    }
    func acquire(_ device:MacAudioInput,cancelled:@escaping () -> Bool,observe:@escaping (String)->Void = {_ in},completion:@escaping (Result<Void,Error>)->Void) {
        Self.queue.async {
            do {
                try Self.restore();try Self.verify()
                guard !cancelled() else {throw MacTab5Error.unconfirmed}
                guard try MacDoubaoCapture(device:device.id).running()==false else {throw MacTab5LocalError.invalid("豆包正在收音或状态未确认，未切换麦克风。")}
                let previous=try MacDoubaoBackground.selection();observe("原麦克风已确认")
                if previous != device.uid {
                    UserDefaults.standard.set(["mode":"store","previousUID":previous,"temporaryUID":device.uid],forKey:Self.journal)
                    guard UserDefaults.standard.synchronize() else {throw MacTab5Error.unconfirmed}
                    try MacDoubaoBackground.update(expected:previous,next:device.uid)
                    MacDoubaoBackground.select(device.uid)
                    observe("后台麦克风选择已读回确认")
                }
                guard !cancelled() else {throw MacTab5Error.unconfirmed}
                observe("麦克风准备完成")
                DispatchQueue.main.async {completion(.success(()))}
            } catch {
                try? Self.restore()
                DispatchQueue.main.async {completion(.failure(error))}
            }
        }
    }
    func release(completion:((Result<Void,Error>)->Void)? = nil) {
        Self.queue.async {
            var result:Result<Void,Error> = .success(())
            for attempt in 0..<2 {
                do {try Self.restore();result = .success(());break}
                catch {result = .failure(error);if attempt==0 {Thread.sleep(forTimeInterval:0.25)}}
            }
            if case .failure = result {Self.record("豆包麦克风恢复未完成，请点击“恢复豆包麦克风”。")}
            let outcome=result
            DispatchQueue.main.async {completion?(outcome)}
        }
    }
    private static func restore() throws {
        guard let saved=UserDefaults.standard.dictionary(forKey:journal) else {return}
        try verify()
        let capture=try MacDoubaoCapture(device:0),deadline=ProcessInfo.processInfo.systemUptime+2
        while capture.running() != false,ProcessInfo.processInfo.systemUptime<deadline {Thread.sleep(forTimeInterval:0.05)}
        guard capture.running()==false else {throw MacTab5LocalError.invalid("豆包尚未停止收音，麦克风恢复将重试。")}
        let current=try MacDoubaoBackground.selection()
        if saved["mode"] as? String == "notification" {
            MacDoubaoBackground.select(current)
        } else {
            guard let previous=saved["previousUID"] as? String,let temporary=saved["temporaryUID"] as? String else {throw MacTab5Error.unconfirmed}
            if current==temporary {try MacDoubaoBackground.update(expected:temporary,next:previous);MacDoubaoBackground.select(previous)}
            else {MacDoubaoBackground.select(current)}
        }
        UserDefaults.standard.removeObject(forKey:journal)
        guard UserDefaults.standard.synchronize() else {UserDefaults.standard.set(saved,forKey:journal);throw MacTab5Error.unconfirmed}
        record(current==saved["temporaryUID"] as? String ? "豆包麦克风已后台恢复为原选择。":"已保留豆包当前麦克风选择。")
    }
}
