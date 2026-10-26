// swift-tools-version: 5.9
import PackageDescription

let package = Package(
    name: "AIBotBridge",
    platforms: [.macOS(.v13)],
    products: [.executable(name: "AIBotBridge", targets: ["AIBotBridge"]),
               .executable(name: "AIBotKeychainHelper", targets: ["AIBotKeychainHelper"])],
    targets: [
        .target(name: "MacKeychainSupport"),
        .target(name: "MacARDKeyAgreement", linkerSettings: [.linkedFramework("Security")]),
        .executableTarget(name: "AIBotKeychainHelper", dependencies: ["MacKeychainSupport"]),
        .executableTarget(name: "AIBotBridge", dependencies: ["MacKeychainSupport", "MacARDKeyAgreement"]),
        .testTarget(name: "AIBotBridgeTests", dependencies: ["AIBotBridge", "MacKeychainSupport"], resources: [.copy("Fixtures")])
    ]
)
