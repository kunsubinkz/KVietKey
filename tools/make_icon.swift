#!/usr/bin/env swift
// Sinh Resources/AppIcon.icns: nền gradient + chữ "V". Dùng: swift tools/make_icon.swift Resources/AppIcon.icns
import AppKit

let output = CommandLine.arguments.count > 1 ? CommandLine.arguments[1] : "Resources/AppIcon.icns"
let iconset = NSTemporaryDirectory() + "KVietKey.iconset"
try? FileManager.default.removeItem(atPath: iconset)
try FileManager.default.createDirectory(atPath: iconset, withIntermediateDirectories: true)

func render(_ px: Int) -> Data {
    let rep = NSBitmapImageRep(bitmapDataPlanes: nil, pixelsWide: px, pixelsHigh: px, bitsPerSample: 8,
                               samplesPerPixel: 4, hasAlpha: true, isPlanar: false, colorSpaceName: .deviceRGB,
                               bytesPerRow: 0, bitsPerPixel: 0)!
    let ctx = NSGraphicsContext(bitmapImageRep: rep)!
    NSGraphicsContext.saveGraphicsState()
    NSGraphicsContext.current = ctx
    let s = CGFloat(px)
    let rect = NSRect(x: s * 0.06, y: s * 0.06, width: s * 0.88, height: s * 0.88)
    let path = NSBezierPath(roundedRect: rect, xRadius: s * 0.2, yRadius: s * 0.2)
    NSGradient(starting: NSColor(red: 0.85, green: 0.10, blue: 0.16, alpha: 1),
               ending: NSColor(red: 0.60, green: 0.04, blue: 0.10, alpha: 1))!.draw(in: path, angle: -90)
    let font = NSFont.systemFont(ofSize: s * 0.6, weight: .heavy)
    let str = NSAttributedString(string: "V", attributes: [.font: font, .foregroundColor: NSColor(red: 1, green: 0.85, blue: 0.2, alpha: 1)])
    let size = str.size()
    str.draw(at: NSPoint(x: (s - size.width) / 2, y: (s - size.height) / 2 - s * 0.01))
    NSGraphicsContext.restoreGraphicsState()
    return rep.representation(using: .png, properties: [:])!
}

for point in [16, 32, 128, 256, 512] {
    for scale in [1, 2] {
        let suffix = scale == 2 ? "@2x" : ""
        try render(point * scale).write(to: URL(fileURLWithPath: "\(iconset)/icon_\(point)x\(point)\(suffix).png"))
    }
}

let p = Process()
p.executableURL = URL(fileURLWithPath: "/usr/bin/iconutil")
p.arguments = ["-c", "icns", iconset, "-o", output]
try p.run()
p.waitUntilExit()
exit(p.terminationStatus)
