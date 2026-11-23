import Foundation
import CoreGraphics
import os

/// Quản lý CGEventTap: bắt phím/chuột, tự bật lại khi hệ thống vô hiệu hóa tap.
final class EventTapManager {
    typealias Handler = (CGEventTapProxy, CGEventType, CGEvent) -> Unmanaged<CGEvent>?

    var handler: Handler?
    private var tap: CFMachPort?
    private var source: CFRunLoopSource?
    private let logger = Logger(subsystem: "vn.kvietkey.app", category: "tap")

    var isRunning: Bool { tap != nil }

    @discardableResult
    func start() -> Bool {
        guard tap == nil else { return true }
        var types: [CGEventType] = [.keyDown, .flagsChanged, .leftMouseDown, .rightMouseDown, .otherMouseDown]
        if InputController.trace != nil { types.append(.keyUp) }  // chỉ khi ghi vết: để thấy phím nào còn đang giữ
        let mask = types.reduce(CGEventMask(0)) { $0 | (CGEventMask(1) << $1.rawValue) }
        let callback: CGEventTapCallBack = { proxy, type, event, refcon in
            guard let refcon else { return Unmanaged.passUnretained(event) }
            let manager = Unmanaged<EventTapManager>.fromOpaque(refcon).takeUnretainedValue()
            return manager.dispatch(proxy: proxy, type: type, event: event)
        }
        guard let port = CGEvent.tapCreate(
            tap: .cgSessionEventTap, place: .headInsertEventTap, options: .defaultTap,
            eventsOfInterest: mask, callback: callback,
            userInfo: Unmanaged.passUnretained(self).toOpaque()
        ) else {
            logger.error("tapCreate failed (missing permission?)")
            return false
        }
        let src = CFMachPortCreateRunLoopSource(kCFAllocatorDefault, port, 0)
        CFRunLoopAddSource(CFRunLoopGetMain(), src, .commonModes)
        CGEvent.tapEnable(tap: port, enable: true)
        tap = port
        source = src
        return true
    }

    func stop() {
        if let tap { CGEvent.tapEnable(tap: tap, enable: false) }
        if let source { CFRunLoopRemoveSource(CFRunLoopGetMain(), source, .commonModes) }
        tap = nil
        source = nil
    }

    private func dispatch(proxy: CGEventTapProxy, type: CGEventType, event: CGEvent) -> Unmanaged<CGEvent>? {
        if type == .tapDisabledByTimeout || type == .tapDisabledByUserInput {
            logger.notice("tap disabled by system, re-enabling")
            InputController.trace?("TAP-DISABLED type=\(type.rawValue) → bật lại")
            if let tap { CGEvent.tapEnable(tap: tap, enable: true) }
            return Unmanaged.passUnretained(event)
        }
        // nil từ handler nghĩa là "nuốt phím" — không được đổi thành cho qua (chỉ cho qua khi chưa có handler).
        guard let handler else { return Unmanaged.passUnretained(event) }
        return handler(proxy, type, event)
    }
}
