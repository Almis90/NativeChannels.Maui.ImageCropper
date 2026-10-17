import UIKit
import ImageIO
import Mantis
import NativeChannelsNative

@objc(MCImageCropperViewFactory)
@MainActor
public final class ImageCropperViewFactory: NSObject, NativeViewFactory {
    public required override init() { super.init() }

    public func createView(_ bridge: NativeBridge, instanceId: String, channelName: String,
                           arguments: Data) throws -> NativeViewInstance {
        precondition(Thread.isMainThread)
        return ImageCropperView(bridge: bridge, channelName: channelName)
    }
}

@MainActor
private final class ImageCropperView: NSObject, NativeViewInstance, CropViewControllerDelegate {
    private let host = CropHostView()
    private let worker = DispatchQueue(label: "dev.nativechannels.imagecropper.apple")
    private var original: UIImage?
    private var pending: MethodResult?
    private var closed = false
    var view: UIView { host }

    init(bridge: NativeBridge, channelName: String) {
        super.init()
        precondition(MethodChannel(channelName, bridge: bridge).setHandler(onMainThread: true) { [weak self] call, result in
            guard let self = self, !self.closed else {
                result.failure("view_disposed", message: "The crop view is detached", details: nil)
                return
            }
            guard !result.isCancelled else { return }
            guard self.pending == nil else {
                result.failure("crop_busy", message: "An image operation is already running", details: nil)
                return
            }
            switch call.method {
            case "load": self.load(call.arguments, result: result)
            case "crop":
                guard let controller = self.host.controller else {
                    result.failure("image_not_loaded", message: "Load an image first", details: nil)
                    return
                }
                guard self.host.window != nil, self.host.bounds.width > 0, self.host.bounds.height > 0 else {
                    result.failure("view_not_ready", message: "Wait for the crop view to be visible", details: nil)
                    return
                }
                self.begin(result)
                self.host.layoutIfNeeded()
                controller.view.layoutIfNeeded()
                controller.crop()
            case "reset":
                if let image = self.original { self.install(image) }
                result.success(nil)
            default: result.notImplemented()
            }
        })
    }

    private func begin(_ result: MethodResult) {
        pending = result
        host.isUserInteractionEnabled = false
    }

    private func finish(_ result: MethodResult, bytes: Data? = nil, error: String? = nil) {
        guard pending === result else { return }
        pending = nil
        host.isUserInteractionEnabled = true
        guard !closed, !result.isCancelled else { return }
        if let error = error { result.failure("crop_failed", message: error, details: nil) }
        else { result.success(bytes) }
    }

    private func load(_ arguments: Any?, result: MethodResult) {
        guard let data = arguments as? Data, !data.isEmpty, data.count <= 8 * 1024 * 1024 else {
            result.failure("invalid_image", message: "Expected PNG bytes up to 8 MiB", details: nil)
            return
        }
        begin(result)
        worker.async { [weak self] in
            let image: UIImage? = autoreleasepool {
                guard !result.isCancelled,
                    let source = CGImageSourceCreateWithData(data as CFData, nil),
                    let properties = CGImageSourceCopyPropertiesAtIndex(source, 0, nil) as? [CFString: Any],
                    let width = properties[kCGImagePropertyPixelWidth] as? NSNumber,
                    let height = properties[kCGImagePropertyPixelHeight] as? NSNumber,
                    width.doubleValue > 0, height.doubleValue > 0,
                    width.doubleValue * height.doubleValue <= 40_000_000,
                    let pixels = CGImageSourceCreateImageAtIndex(source, 0, [kCGImageSourceShouldCacheImmediately: true] as CFDictionary)
                else { return nil }
                return UIImage(cgImage: pixels)
            }
            DispatchQueue.main.async {
                guard let self = self else { return }
                if !self.closed, !result.isCancelled, let image = image {
                    self.original = image
                    self.install(image)
                }
                self.finish(result, error: image == nil ? "Invalid image or exceeds 40 megapixels" : nil)
            }
        }
    }

    private func install(_ image: UIImage) {
        var config = Mantis.Config()
        config.cropToolbarConfig.mode = .embedded
        config.cropMode = .async
        config.appearanceMode = .system
        let controller = Mantis.cropViewController(image: image, config: config)
        controller.delegate = self
        host.install(controller)
    }

    func cropViewControllerDidCrop(_ cropViewController: CropViewController, cropped: UIImage,
                                   transformation: Transformation, cropInfo: CropInfo) {
        guard cropViewController === host.controller, let result = pending else { return }
        worker.async { [weak self] in
            let data = autoreleasepool { result.isCancelled ? nil : cropped.pngData() }
            DispatchQueue.main.async {
                guard let self = self else { return }
                guard let data = data, data.count <= 8 * 1024 * 1024 else {
                    self.finish(result, error: "PNG encoding failed or exceeds the 8 MiB result limit")
                    return
                }
                self.finish(result, bytes: data)
            }
        }
    }

    func cropViewControllerDidFailToCrop(_ cropViewController: CropViewController, original: UIImage) {
        guard cropViewController === host.controller, let result = pending else { return }
        finish(result, error: "Cannot crop image")
    }

    func cropViewControllerDidCancel(_ cropViewController: CropViewController, original: UIImage) {
        guard cropViewController === host.controller, let result = pending else { return }
        finish(result, error: "Crop cancelled")
    }

    func close() {
        precondition(Thread.isMainThread)
        guard !closed else { return }
        closed = true
        if let result = pending, !result.isCancelled {
            result.failure("view_disposed", message: "The crop view is detached", details: nil)
        }
        pending = nil
        original = nil
        host.install(nil)
    }
}

/// Retains the controller and establishes UIKit containment under MAUI's nearest controller.
private final class CropHostView: UIView {
    private(set) var controller: CropViewController?

    func install(_ replacement: CropViewController?) {
        controller?.delegate = nil
        detach()
        controller?.view.removeFromSuperview()
        controller = replacement
        guard let replacement = replacement else { return }
        attach()
        replacement.view.frame = bounds
        replacement.view.autoresizingMask = [.flexibleWidth, .flexibleHeight]
        if replacement.view.superview !== self { addSubview(replacement.view) }
        setNeedsLayout()
    }

    override func didMoveToWindow() {
        super.didMoveToWindow()
        if window == nil { detach() } else { attach() }
    }

    override func layoutSubviews() {
        super.layoutSubviews()
        attach()
        controller?.view.frame = bounds
    }

    private func attach() {
        guard window != nil, let controller = controller, controller.parent == nil else { return }
        var responder: UIResponder? = next
        while let current = responder {
            if let parent = current as? UIViewController {
                parent.addChild(controller)
                addSubview(controller.view)
                controller.didMove(toParent: parent)
                return
            }
            responder = current.next
        }
    }

    private func detach() {
        guard let controller = controller, controller.parent != nil else { return }
        controller.willMove(toParent: nil)
        controller.view.removeFromSuperview()
        controller.removeFromParent()
    }
}
