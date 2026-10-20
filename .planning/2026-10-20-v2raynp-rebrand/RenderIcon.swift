import AppKit
import CoreGraphics

let output = CommandLine.arguments[1]
let accentName = CommandLine.arguments.count > 2 ? CommandLine.arguments[2] : "teal"
let accentColor: NSColor = switch accentName {
case "blue": NSColor(calibratedRed: 0.23, green: 0.65, blue: 0.95, alpha: 1)
case "red": NSColor(calibratedRed: 0.96, green: 0.32, blue: 0.35, alpha: 1)
case "purple": NSColor(calibratedRed: 0.72, green: 0.43, blue: 0.94, alpha: 1)
case "yellow": NSColor(calibratedRed: 0.98, green: 0.79, blue: 0.30, alpha: 1)
default: NSColor(calibratedRed: 0.25, green: 0.88, blue: 0.76, alpha: 1)
}
let size = 1024
let image = NSImage(size: NSSize(width: size, height: size))
image.lockFocus()
guard let context = NSGraphicsContext.current?.cgContext else { fatalError("No graphics context") }
context.setAllowsAntialiasing(true)
context.setShouldAntialias(true)

let bounds = CGRect(x: 0, y: 0, width: size, height: size)
let background = CGPath(roundedRect: bounds.insetBy(dx: 24, dy: 24), cornerWidth: 220, cornerHeight: 220, transform: nil)
context.addPath(background)
context.setFillColor(NSColor(calibratedRed: 0.075, green: 0.12, blue: 0.18, alpha: 1).cgColor)
context.fillPath()

let route = CGMutablePath()
route.move(to: CGPoint(x: 270, y: 670))
route.addLine(to: CGPoint(x: 480, y: 670))
route.addCurve(to: CGPoint(x: 650, y: 500), control1: CGPoint(x: 560, y: 670), control2: CGPoint(x: 570, y: 500))
route.addLine(to: CGPoint(x: 650, y: 354))
route.addCurve(to: CGPoint(x: 755, y: 250), control1: CGPoint(x: 650, y: 295), control2: CGPoint(x: 705, y: 250))
context.addPath(route)
context.setStrokeColor(accentColor.cgColor)
context.setLineWidth(76)
context.setLineCap(.round)
context.setLineJoin(.round)
context.strokePath()

for point in [CGPoint(x: 270, y: 670), CGPoint(x: 755, y: 250)] {
    context.addEllipse(in: CGRect(x: point.x - 42, y: point.y - 42, width: 84, height: 84))
    context.setFillColor(NSColor(calibratedRed: 1, green: 0.79, blue: 0.35, alpha: 1).cgColor)
    context.fillPath()
}

let letter = NSAttributedString(string: "NP", attributes: [
    .font: NSFont.systemFont(ofSize: 175, weight: .heavy),
    .foregroundColor: NSColor.white
])
letter.draw(at: NSPoint(x: 312, y: 330))
image.unlockFocus()

guard let tiff = image.tiffRepresentation,
      let bitmap = NSBitmapImageRep(data: tiff),
      let png = bitmap.representation(using: .png, properties: [:]) else { fatalError("PNG export failed") }
try png.write(to: URL(fileURLWithPath: output))
