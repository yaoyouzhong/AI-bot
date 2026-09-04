// swift-tools-version: 5.9
import PackageDescription

let package = Package(
    name: "AIBotBridge",
    platforms: [.macOS(.v13)],
    products: [.executable(name: "AIBotBridge", targets: ["AIBotBridge"])],
    targets: [
        .executableTarget(name: "AIBotBridge"),
        .testTarget(name: "AIBotBridgeTests", dependencies: ["AIBotBridge"])
    ]
)
