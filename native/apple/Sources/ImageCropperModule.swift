import Foundation
import CoreGraphics
import ImageIO
import UniformTypeIdentifiers
import NativeChannelsNative

@objc(MCImageCropperModule)
public final class ImageCropperModule: NSObject, NativeModule {
    public required override init() { super.init() }

    public func register(with bridge: NativeBridge) {
        precondition(MethodChannel("nativechannels.maui.imagecropper", bridge: bridge)
            .setHandler(onMainThread: false) { call, result in
                guard call.method == "crop" else { result.notImplemented(); return }
                do {
                    guard !result.isCancelled else { return }
                    let bytes = try Self.crop(call.arguments)
                    if !result.isCancelled { result.success(bytes) }
                }
                catch { result.failure("crop_failed", message: error.localizedDescription, details: nil) }
            })
    }

    private static func crop(_ arguments: Any?) throws -> Data {
        func invalid(_ message: String) -> NSError {
            NSError(domain: "ImageCropper", code: 1, userInfo: [NSLocalizedDescriptionKey: message])
        }
        guard let args = arguments as? [String: Any], let path = args["path"] as? String,
            let x = (args["x"] as? NSNumber)?.doubleValue,
            let y = (args["y"] as? NSNumber)?.doubleValue,
            let w = (args["width"] as? NSNumber)?.doubleValue,
            let h = (args["height"] as? NSNumber)?.doubleValue,
            [x, y, w, h].allSatisfy({ $0.isFinite }),
            x >= 0, y >= 0, w > 0, h > 0, x + w <= 1, y + h <= 1
        else { throw invalid("Expected a local path and a normalized rectangle") }
        let attributes = try FileManager.default.attributesOfItem(atPath: path)
        guard let size = attributes[.size] as? NSNumber, size.intValue <= 64 * 1024 * 1024
        else { throw invalid("Source exceeds 64 MiB") }
        guard let source = CGImageSourceCreateWithURL(URL(fileURLWithPath: path) as CFURL, nil),
            let properties = CGImageSourceCopyPropertiesAtIndex(source, 0, nil) as? [CFString: Any],
            let width = properties[kCGImagePropertyPixelWidth] as? NSNumber,
            let height = properties[kCGImagePropertyPixelHeight] as? NSNumber,
            width.doubleValue > 0, height.doubleValue > 0,
            width.doubleValue * height.doubleValue <= 40_000_000,
            let image = CGImageSourceCreateImageAtIndex(source, 0, nil)
        else { throw invalid("Invalid image or exceeds 40 megapixels") }
        let left = floor(x * Double(image.width)), top = floor(y * Double(image.height))
        let right = min(Double(image.width), ceil((x + w) * Double(image.width)))
        let bottom = min(Double(image.height), ceil((y + h) * Double(image.height)))
        guard let cropped = image.cropping(to: CGRect(x: left, y: top, width: right - left, height: bottom - top))
        else { throw invalid("Cannot crop image") }
        let output = NSMutableData()
        guard let destination = CGImageDestinationCreateWithData(output, UTType.png.identifier as CFString, 1, nil)
        else { throw invalid("Cannot create PNG encoder") }
        CGImageDestinationAddImage(destination, cropped, nil)
        guard CGImageDestinationFinalize(destination) else { throw invalid("PNG encoding failed") }
        guard output.length <= 8 * 1024 * 1024 else { throw invalid("PNG exceeds the 8 MiB result limit. Select a smaller crop.") }
        return output as Data
    }
}
