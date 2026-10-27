import Foundation

struct MacModelAccountProvider {
    let id: String, name: String, product: String, address: String
    let loginDomains: [String]
    var url: URL { URL(string:address)! }
    var supportsQuota: Bool { !MacDomesticSources.modes(id).isEmpty }
    var supportsWebQuota: Bool { ["qwen","kimi","xiaomi","zhipu","minimax","deepseek"].contains(id) }
}

enum MacModelAccountCatalog {
    static let all: [MacModelAccountProvider] = [
        .init(id:"qwen",name:"阿里云百炼",product:"千问 Token Plan",address:"https://bailian.console.aliyun.com/cn-beijing?tab=plan#/efm/subscription/token-plan",loginDomains:["bailian.console.aliyun.com","account.aliyun.com","signin.aliyun.com","passport.aliyun.com","login.aliyun.com"]),
        .init(id:"kimi",name:"月之暗面 Kimi",product:"Kimi Coding Plan",address:"https://www.kimi.com/code/console",loginDomains:["www.kimi.com","kimi.com","auth.kimi.com"]),
        .init(id:"xiaomi",name:"小米 MiMo",product:"MiMo Token Plan",address:"https://platform.xiaomimimo.com/token-plan",loginDomains:["platform.xiaomimimo.com","account.xiaomi.com"]),
        .init(id:"zhipu",name:"智谱 GLM",product:"开放平台余额／Coding Plan",address:"https://bigmodel.cn/finance-center/finance/overview",loginDomains:["bigmodel.cn","www.bigmodel.cn","open.bigmodel.cn"]),
        .init(id:"volcengine",name:"火山方舟",product:"豆包大模型 · 官方网页",address:"https://console.volcengine.com/ark",loginDomains:["console.volcengine.com","www.volcengine.com","signin.volcengine.com"]),
        .init(id:"minimax",name:"MiniMax",product:"MiniMax Token Plan",address:"https://platform.minimaxi.com/console/usage",loginDomains:["platform.minimaxi.com","www.minimaxi.com"]),
        .init(id:"deepseek",name:"DeepSeek",product:"开放平台余额",address:"https://platform.deepseek.com/usage",loginDomains:["platform.deepseek.com"]),
        .init(id:"baidu",name:"百度智能云",product:"千帆模型资源包／现金余额",address:"https://console.bce.baidu.com/qianfan/overview",loginDomains:["console.bce.baidu.com","login.bce.baidu.com","passport.baidu.com"]),
        .init(id:"tencent",name:"腾讯云",product:"混元大模型 · 官方网页",address:"https://console.cloud.tencent.com/hunyuan",loginDomains:["console.cloud.tencent.com","cloud.tencent.com","login.cloud.tencent.com","qcloud.com"]),
        .init(id:"huawei",name:"华为云",product:"盘古／ModelArts · 官方网页",address:"https://console.huaweicloud.com/modelarts/",loginDomains:["console.huaweicloud.com","auth.huaweicloud.com","iam-login.cloud.huawei.com"]),
        .init(id:"iflytek",name:"讯飞开放平台",product:"讯飞星火 · 官方网页",address:"https://console.xfyun.cn/",loginDomains:["console.xfyun.cn","passport.xfyun.cn"]),
        .init(id:"stepfun",name:"阶跃星辰",product:"开放平台余额（非 Step Plan）",address:"https://platform.stepfun.com/account-overview",loginDomains:["platform.stepfun.com"]),
        .init(id:"baichuan",name:"百川智能",product:"Baichuan · 官方网页",address:"https://platform.baichuan-ai.com/",loginDomains:["platform.baichuan-ai.com"]),
        .init(id:"lingyi",name:"零一万物",product:"Yi · 官方网页",address:"https://platform.lingyiwanwu.com/",loginDomains:["platform.lingyiwanwu.com"])
    ]
    static func provider(_ id: String) -> MacModelAccountProvider? { all.first { $0.id == id } }
    static func navigationAllowed(_ url: URL, provider: String, mainFrame: Bool) -> Bool {
        if !mainFrame, url.absoluteString == "about:blank" { return true }
        guard url.scheme == "https", url.port == nil || url.port == 443, url.user == nil, url.password == nil,
              let host = url.host?.lowercased(), let item = self.provider(provider) else { return false }
        if !mainFrame, host == "open.weixin.qq.com", url.path == "/connect/qrconnect" { return true }
        return item.loginDomains.contains { host == $0 || host.hasSuffix("."+$0) }
    }
}

struct MacConsoleRefreshState {
    private(set) var awaiting = false
    private(set) var deadline: TimeInterval = 0
    private(set) var nextAttempt: TimeInterval = 0
    private(set) var needsLogin = false
    mutating func start(_ now: TimeInterval) { awaiting = true; deadline = now+60; nextAttempt = now+120 }
    mutating func success(_ now: TimeInterval) { awaiting = false; needsLogin = false; nextAttempt = now+120 }
    mutating func fail(_ now: TimeInterval, login: Bool = false) { awaiting = false; needsLogin = login; nextAttempt = now+300 }
    mutating func timeout(_ now: TimeInterval) -> Bool {
        guard awaiting, now >= deadline else { return false }; fail(now); return true
    }
    func due(_ now: TimeInterval, interactive: Bool) -> Bool { !interactive && !awaiting && now >= nextAttempt }
}
