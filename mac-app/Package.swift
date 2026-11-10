// swift-tools-version: 5.9
import PackageDescription

let package = Package(
    name: "AIBotBridge",
    platforms: [.macOS(.v13)],
    products: [.executable(name: "AIBotBridge", targets: ["AIBotBridge"]),
               .executable(name: "AIBotKeychainHelper", targets: ["AIBotKeychainHelper"])],
    dependencies: [
        .package(url: "https://github.com/Tencent/MMKV.git", revision: "007a393cd5e255e9aa68d6648f7179159bdf0f9d"),
        .package(url: "https://github.com/migueldeicaza/SwiftTerm.git", revision: "5c83a9d214e7354697624c11deb4e488bdcfabad"),
    ],
    targets: [
        .target(name: "MacKeychainSupport"),
        .target(name: "MacDoubaoStore", dependencies: [.product(name: "MMKVCore", package: "MMKV")], cxxSettings: [.unsafeFlags(["-fobjc-arc"])], linkerSettings: [.linkedFramework("Foundation")]),
        .target(name: "MacARDKeyAgreement", linkerSettings: [.linkedFramework("Security")]),
        .executableTarget(name: "AIBotKeychainHelper", dependencies: ["MacKeychainSupport"]),
        .executableTarget(name: "AIBotBridge", dependencies: ["MacKeychainSupport", "MacARDKeyAgreement", "MacDoubaoStore", .product(name: "SwiftTerm", package: "SwiftTerm")]),
        .testTarget(name: "AIBotBridgeTests", dependencies: ["AIBotBridge", "MacKeychainSupport"], resources: [.copy("Fixtures")])
    ],
    cxxLanguageStandard: .cxx20
)
