package dev.nativechannels.imagecropper

import android.content.Context
import android.graphics.Bitmap
import android.graphics.BitmapFactory
import android.graphics.Rect
import android.os.Handler
import android.os.Looper
import android.view.View
import com.canhub.cropper.CropImageView
import dev.nativechannels.MethodChannel
import dev.nativechannels.MethodResult
import dev.nativechannels.NativeBridge
import dev.nativechannels.NativeViewFactory
import dev.nativechannels.NativeViewInstance
import java.io.ByteArrayOutputStream
import java.util.concurrent.Executors

/** One CanHub crop view and isolated channel per MAUI view connection. */
class ImageCropperViewFactory : NativeViewFactory {
    override fun create(
        context: Context,
        bridge: NativeBridge,
        instanceId: String,
        channelName: String,
        arguments: ByteArray,
    ): NativeViewInstance = ImageCropperView(context, bridge, channelName)
}

private class ImageCropperView(
    context: Context,
    bridge: NativeBridge,
    channelName: String,
) : NativeViewInstance {
    private val control = CropImageView(context).apply {
        guidelines = CropImageView.Guidelines.ON
        setMinCropResultSize(1, 1)
    }
    private val main = Handler(Looper.getMainLooper())
    private val worker = Executors.newSingleThreadExecutor()
    private var source: Bitmap? = null
    private var busy = false
    private var closed = false

    init {
        MethodChannel(channelName, bridge).setHandler(onMainThread = true) { call, result ->
            when {
                closed -> result.error("view_disposed", "The crop view is detached")
                result.isCancelled -> Unit
                busy -> result.error("crop_busy", "An image operation is already running")
                else -> when (call.method) {
                    "load" -> load(call.arguments, result)
                    "crop" -> crop(result)
                    "reset" -> {
                        control.resetCropRect()
                        result.success(null)
                    }
                    else -> result.notImplemented()
                }
            }
        }
    }

    private fun load(arguments: Any?, result: MethodResult) {
        val bytes = arguments as? ByteArray
        if (bytes == null || bytes.isEmpty() || bytes.size > MAX_PNG_BYTES) {
            result.error("invalid_image", "Expected PNG bytes up to 8 MiB")
            return
        }
        runWork(result, work = {
            val bounds = BitmapFactory.Options().apply { inJustDecodeBounds = true }
            BitmapFactory.decodeByteArray(bytes, 0, bytes.size, bounds)
            require(bounds.outWidth > 0 && bounds.outHeight > 0 &&
                bounds.outWidth.toLong() * bounds.outHeight <= MAX_PIXELS) {
                "Invalid image or exceeds 40 megapixels"
            }
            BitmapFactory.decodeByteArray(bytes, 0, bytes.size) ?: error("Cannot decode image")
        }, discard = { it.recycle() }) { bitmap ->
            val previous = source
            control.setImageBitmap(bitmap)
            source = bitmap
            previous?.recycle()
            result.success(null)
        }
    }

    private fun crop(result: MethodResult) {
        val bitmap = source
        val bounds = control.cropRect?.let { Rect(it) }
        if (bitmap == null || bounds == null) {
            result.error("image_not_loaded", "Load an image first")
            return
        }
        // Snapshot CanHub's selection on the UI thread. Only bitmap work runs on the worker.
        // This view exposes rectangular selection/zoom, without rotation or flip commands.
        runWork(result, work = {
            require(bounds.width() > 0 && bounds.height() > 0 && bounds.left >= 0 && bounds.top >= 0 &&
                bounds.right <= bitmap.width && bounds.bottom <= bitmap.height) { "Invalid crop selection" }
            val cropped = Bitmap.createBitmap(bitmap, bounds.left, bounds.top, bounds.width(), bounds.height())
            try {
                ByteArrayOutputStream().use { output ->
                    check(cropped.compress(Bitmap.CompressFormat.PNG, 100, output)) { "PNG encoding failed" }
                    require(output.size() <= MAX_PNG_BYTES) { "PNG exceeds the 8 MiB result limit" }
                    output.toByteArray()
                }
            } finally {
                if (cropped !== bitmap) cropped.recycle()
            }
        }) { bytes -> result.success(bytes) }
    }

    private fun <T> runWork(
        result: MethodResult,
        work: () -> T,
        discard: (T) -> Unit = {},
        complete: (T) -> Unit,
    ) {
        busy = true
        control.isEnabled = false
        worker.execute {
            val outcome = runCatching {
                check(!result.isCancelled) { "Operation cancelled" }
                work()
            }
            main.post {
                busy = false
                if (!closed) control.isEnabled = true
                outcome.fold(
                    onSuccess = { value ->
                        if (closed || result.isCancelled) discard(value)
                        else try {
                            complete(value)
                        } catch (error: Exception) {
                            discard(value)
                            result.error("crop_failed", error.message ?: "Image operation failed")
                        }
                    },
                    onFailure = { error ->
                        if (!closed && !result.isCancelled)
                            result.error("crop_failed", error.message ?: "Image operation failed")
                    },
                )
            }
        }
    }

    override fun getView(): View = control

    override fun close() {
        check(Looper.myLooper() == Looper.getMainLooper())
        if (closed) return
        closed = true
        control.clearImage()
        val bitmap = source
        source = null
        // Drain any crop using this bitmap before recycling it. Never recycle an in-use source.
        worker.execute { bitmap?.recycle() }
        worker.shutdown()
    }

    private companion object {
        const val MAX_PNG_BYTES = 8 * 1024 * 1024
        const val MAX_PIXELS = 40_000_000L
    }
}
