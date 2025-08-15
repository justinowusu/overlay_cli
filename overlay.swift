#!/usr/bin/env swift

import Cocoa
import Foundation

// Get the rectangle parameters from command line arguments
let rectParams: (x: Int, y: Int, width: Int, height: Int) = {
    let args = CommandLine.arguments
    if args.count != 5 {
        print("Usage: \(args[0]) <x> <y> <width> <height>")
        exit(1)
    }

    guard let x = Int(args[1]),
          let y = Int(args[2]),
          let width = Int(args[3]),
          let height = Int(args[4]) else {
        print("All parameters must be integers")
        exit(1)
    }

    return (x, y, width, height)
}()

/// A simple NSView subclass to display and animate the highlight rectangle
class HighlightView: NSView {
    private let highlightRect: NSRect
    private var fadeTimer: Timer?
    private var opacity: CGFloat = 0.0
    private let targetOpacity: CGFloat = 0.2
    private let animationDuration: TimeInterval = 0.3
    private let displayDuration: TimeInterval = 2.8

    init(frame frameRect: NSRect, highlightRect: NSRect) {
        self.highlightRect = highlightRect
        super.init(frame: frameRect)
        self.wantsLayer = true
        self.layer?.backgroundColor = NSColor.clear.cgColor

        // Start fade-in animation
        startFadeInAnimation()
    }

    required init?(coder: NSCoder) {
        fatalError("init(coder:) has not been implemented")
    }

    override func draw(_ dirtyRect: NSRect) {
        super.draw(dirtyRect)

        NSColor.systemBlue.withAlphaComponent(opacity).setFill()
        highlightRect.fill()

        NSColor.systemBlue.withAlphaComponent(opacity * 4).setStroke()
        let borderPath = NSBezierPath(rect: highlightRect)
        borderPath.lineWidth = 2
        borderPath.stroke()
    }

    private func startFadeInAnimation() {
        // Create a fade-in animation
        let animationInterval = 0.01
        let opacityIncrement = targetOpacity / (animationDuration / animationInterval)

        fadeTimer = Timer.scheduledTimer(withTimeInterval: animationInterval, repeats: true) { [weak self] timer in
            guard let self = self else {
                timer.invalidate()
                return
            }

            if self.opacity < self.targetOpacity {
                self.opacity += opacityIncrement
                self.needsDisplay = true
            } else {
                // We've reached full opacity, stop the timer
                timer.invalidate()

                // Schedule the fade-out after display duration
                DispatchQueue.main.asyncAfter(deadline: .now() + self.displayDuration) { [weak self] in
                    self?.startFadeOutAnimation()
                }
            }
        }
    }

    private func startFadeOutAnimation() {
        // Create a fade-out animation
        let animationInterval = 0.01
        let opacityDecrement = targetOpacity / (animationDuration / animationInterval)

        fadeTimer = Timer.scheduledTimer(withTimeInterval: animationInterval, repeats: true) { [weak self] timer in
            guard let self = self else {
                timer.invalidate()
                return
            }

            if self.opacity > 0 {
                self.opacity -= opacityDecrement
                self.needsDisplay = true
            } else {
                // We've reached zero opacity, stop the timer and terminate
                timer.invalidate()
                DispatchQueue.main.async {
                    NSApp.terminate(nil)
                }
            }
        }
    }
}

// Helper function to find which screen contains the rect
func findScreenContainingRect(x: CGFloat, y: CGFloat, width: CGFloat, height: CGFloat) -> NSScreen? {
    // Get the primary screen to calculate global coordinates
    guard let primaryScreen = NSScreen.screens.first else { return nil }
    let primaryScreenHeight = primaryScreen.frame.height

    // Convert y from top-left origin to bottom-left origin (Cocoa coordinates)
    // The input y is in global top-left coordinates
    let cocoaY = primaryScreenHeight - y - height

    // Create rect in Cocoa coordinates
    let rectInCocoaCoords = NSRect(x: x, y: cocoaY, width: width, height: height)

    // Check each screen to see if it contains the rect's center point
    let centerPoint = NSPoint(x: rectInCocoaCoords.midX, y: rectInCocoaCoords.midY)

    for screen in NSScreen.screens {
        if screen.frame.contains(centerPoint) {
            return screen
        }
    }

    // If no screen contains the center, check if any screen intersects with the rect
    for screen in NSScreen.screens {
        if screen.frame.intersects(rectInCocoaCoords) {
            return screen
        }
    }

    // Fallback to main screen
    return NSScreen.main
}

class AppDelegate: NSObject, NSApplicationDelegate {
    var window: NSWindow?

    func applicationDidFinishLaunching(_ notification: Notification) {
        NSApp.activate(ignoringOtherApps: true)

        // Find the screen that contains the rectangle
        guard let targetScreen = findScreenContainingRect(
            x: CGFloat(rectParams.x),
            y: CGFloat(rectParams.y),
            width: CGFloat(rectParams.width),
            height: CGFloat(rectParams.height)
        ) else {
            print("No screen found for the given rectangle!")
            NSApp.terminate(nil)
            return
        }

        let screenFrame = targetScreen.frame

        // Get primary screen height for coordinate conversion
        guard let primaryScreen = NSScreen.screens.first else {
            print("No primary screen found!")
            NSApp.terminate(nil)
            return
        }
        let primaryScreenHeight = primaryScreen.frame.height

        // Convert y-coordinate from global top-left to screen-local bottom-left
        // First convert to global Cocoa coordinates (bottom-left origin)
        let globalCocoaY = primaryScreenHeight - CGFloat(rectParams.y) - CGFloat(rectParams.height)

        // Then convert to screen-local coordinates
        let localX = CGFloat(rectParams.x) - screenFrame.origin.x
        let localY = globalCocoaY - screenFrame.origin.y

        // Create the highlight rectangle in screen-local coordinates
        let highlightRect = NSRect(
            x: localX,
            y: localY,
            width: CGFloat(rectParams.width),
            height: CGFloat(rectParams.height)
        )

        // Create window on the target screen
        window = NSWindow(
            contentRect: NSRect(origin: .zero, size: screenFrame.size),
            styleMask: [.borderless],
            backing: .buffered,
            defer: false,
            screen: targetScreen
        )

        window?.backgroundColor = NSColor.clear
        window?.isOpaque = false
        window?.level = .screenSaver
        window?.ignoresMouseEvents = true

        // Position the window at the screen's origin
        window?.setFrameOrigin(screenFrame.origin)
        window?.makeKeyAndOrderFront(nil)

        let highlightView = HighlightView(
            frame: NSRect(origin: .zero, size: screenFrame.size),
            highlightRect: highlightRect
        )
        window?.contentView = highlightView
    }
}

// 500,000 microseconds = 0.5 seconds

let app = NSApplication.shared
let delegate = AppDelegate()
app.delegate = delegate
app.setActivationPolicy(.accessory)
usleep(150000)
app.run()