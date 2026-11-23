// swift-tools-version:5.9
import PackageDescription

let package = Package(
    name: "KVietKey",
    platforms: [.macOS(.v13)],
    dependencies: [
        .package(path: "Packages/VietEngine"),
    ],
    targets: [
        .executableTarget(
            name: "KVietKey",
            dependencies: [.product(name: "VietEngine", package: "VietEngine")],
            path: "App"
        ),
    ]
)
