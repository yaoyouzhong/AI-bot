import AppKit

/// The two tabs edit the same draft; changing tabs never saves or discards it.
final class MacDataSettingsForm {
    let automatic = NSButton(checkboxWithTitle: "根据电脑位置自动查询天气", target: nil, action: nil)
    let provider = NSPopUpButton()
    let weatherHost: NSTextField
    let weatherKey = NSSecureTextField()
    let city: NSTextField
    let latitude: NSTextField
    let longitude: NSTextField
    let stocks: NSTextField
    let form: MacBridgeSettingsForm
    private var checking = false
    private let sourceFields: MacWeatherSourceFields

    init(_ preferences: MacDataPreferences, selectedTab: Int = 0, standalone: Bool = false, preview: ((MacDataPreferences,String?) async throws -> WeatherSnapshot)? = nil) {
        automatic.state = preferences.automaticLocation ? .on : .off
        provider.addItems(withTitles: ["Open-Meteo", "和风天气"])
        provider.selectItem(at: preferences.weatherProvider == "qweather" ? 1 : 0)
        weatherHost = NSTextField(string: preferences.qweatherHost)
        weatherHost.placeholderString = "控制台分配的 xxx.qweatherapi.com"
        weatherKey.placeholderString = "留空保留该 Host 已保存的密钥"
        city = NSTextField(string: preferences.city)
        latitude = NSTextField(string: preferences.latitude.map { String($0) } ?? "")
        longitude = NSTextField(string: preferences.longitude.map { String($0) } ?? "")
        stocks = NSTextField(string: preferences.symbols.joined(separator: ","))
        let weather = MacBridgeSettingsPage(), stockPage = MacBridgeSettingsPage()
        weather.heading("天气定位", subtitle: "选择天气来源，使用电脑位置、城市或成对经纬度。")
        weather.field("天气来源", provider)
        var credentials: [NSView] = []
        for (title, control) in [("和风 API Host", weatherHost), ("和风 API Key（仅保存到钥匙串）", weatherKey)] {
            let group = NSStackView(); group.orientation = .vertical; group.alignment = .leading; group.spacing = 8
            group.addArrangedSubview(MacBridgeSettingsStyle.label(title)); group.addArrangedSubview(control)
            control.widthAnchor.constraint(equalTo: group.widthAnchor).isActive = true
            weather.add(group); credentials.append(group)
        }
        sourceFields = MacWeatherSourceFields(provider: provider, fields: credentials)
        weather.section("位置")
        weather.add(automatic); weather.field("天气城市", city)
        let coordinates = NSGridView(views: [[MacBridgeSettingsStyle.label("纬度（可空）"), latitude], [MacBridgeSettingsStyle.label("经度（可空）"), longitude]])
        coordinates.rowSpacing = 12; coordinates.columnSpacing = 12; weather.add(coordinates)
        weather.add(MacBridgeSettingsStyle.note("自动定位将大致位置发送给系统地名服务和所选天气服务，不保存精确位置。和风请求可能计入你的账户用量；Open-Meteo 不需要密钥。"))
        stockPage.heading("自选股票", subtitle: "管理这台 Mac 提供的股票与指数，保存后同步刷新。")
        stockPage.field("股票代码（逗号分隔，最多 20 个）", stocks)
        stockPage.add(MacBridgeSettingsStyle.note("支持 sh、sz、bj、hk、us 市场前缀，例如 sh000001、sz000001、hk00700、usAAPL。列表顺序按输入保留，重复代码只保留一项。"))
        stockPage.section("保存与刷新")
        stockPage.add(MacBridgeSettingsStyle.note("两个页签共用本次未保存的设置。点击保存后立即刷新，网络失败保留最近一次成功的数据。"))
        let tabs = MacBridgeSettingsTabs([("天气定位", weather), ("自选股票", stockPage)]); tabs.select(selectedTab)
        form = MacBridgeSettingsForm(standalone ? "天气定位" : "天气与股票", subtitle: "", height: 680, confirm:"保存并刷新", body: standalone ? weather : tabs)
        let locate = MacSettingsButton("获取当前位置") { [weak self] in
            guard let self,!self.checking else { return }; self.checking = true; self.form.status.stringValue = "正在获取大致位置…"
            Task { @MainActor [weak self] in
                guard let self else { return }; defer { self.checking = false }
                do { let value = try await MacWeatherLocation.shared.namedLocation(); self.latitude.stringValue = String(value.0); self.longitude.stringValue = String(value.1); self.city.stringValue = value.2; self.automatic.state = .on; self.form.status.stringValue = "定位成功，测试连接后保存。" }
                catch { self.form.status.stringValue = error.localizedDescription }
            }
        }
        let test = MacSettingsButton("测试连接") { [weak self] in
            guard let self,!self.checking,let preview else { return }
            do {
                let draft = try self.preferences(), key = self.weatherKey.stringValue.trimmingCharacters(in:.whitespacesAndNewlines)
                self.checking = true; self.form.status.stringValue = "正在测试，尚未保存设置…"
                Task { @MainActor [weak self] in guard let self else { return }; defer { self.checking = false }
                    do { let value = try await preview(draft,key.isEmpty ? nil:key); self.form.status.stringValue = "连接成功（\(value.source)）：\(value.city) · \(value.condition) · \(Int(value.temperature.rounded()))℃；尚未保存。" }
                    catch { self.form.status.stringValue = "测试未通过，原设置保留。" + error.localizedDescription }
                }
            } catch { self.form.status.stringValue = error.localizedDescription }
        }
        weather.add(MacBridgeSettingsStyle.row([locate,test]))
    }
    func preferences() throws -> MacDataPreferences {
        let a = latitude.stringValue.trimmingCharacters(in:.whitespacesAndNewlines), b = longitude.stringValue.trimmingCharacters(in:.whitespacesAndNewlines)
        let lat = Double(a), lon = Double(b)
        guard (a.isEmpty && b.isEmpty) || (lat.map { $0.isFinite && (-90...90).contains($0) } == true && lon.map { $0.isFinite && (-180...180).contains($0) } == true) else { throw MacTab5LocalError.invalid("经纬度必须同时留空，或填写有效数字。") }
        let city = city.stringValue.trimmingCharacters(in:.whitespacesAndNewlines), host = weatherHost.stringValue.trimmingCharacters(in:.whitespacesAndNewlines).lowercased()
        guard automatic.state == .on || !city.isEmpty || lat != nil else { throw MacTab5LocalError.invalid("请选择自动定位，或填写城市／经纬度。") }
        guard provider.indexOfSelectedItem == 0 || MacQWeatherService.validHost(host) else { throw MacQWeatherService.Failure.configuration }
        return .init(city:city,latitude:lat,longitude:lon,symbols:MacDataPreferences.load().symbols,automaticLocation:automatic.state == .on,weatherProvider:provider.indexOfSelectedItem == 0 ? "open-meteo":"qweather",qweatherHost:host)
    }
}
