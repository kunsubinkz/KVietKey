// swift-tools-version:5.9
import PackageDescription

let package = Package(
    name: "VietEngine",
    platforms: [.macOS(.v13)],
    products: [
        .library(name: "VietEngine", targets: ["VietEngine"]),
    ],
    targets: [
        .target(name: "VietEngine"),
        .testTarget(name: "VietEngineTests", dependencies: ["VietEngine"]),
    ]
)
