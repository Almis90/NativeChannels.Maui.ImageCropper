package dev.nativechannels.imagecropper

import android.graphics.Bitmap
import android.graphics.BitmapFactory
import dev.nativechannels.MethodChannel
import dev.nativechannels.NativeBridge
import dev.nativechannels.NativeModule
import java.io.ByteArrayOutputStream
import kotlin.math.ceil
import kotlin.math.floor

class ImageCropperModule : NativeModule {
    override fun register(bridge: NativeBridge) {
        MethodChannel("nativechannels.maui.imagecropper", bridge).setHandler { call, result ->
            if (call.method != "crop") {
                result.notImplemented()
                return@setHandler
            }
            try {
                val args = call.arguments as? Map<*, *> ?: error("Expected crop arguments")
                val path = args["path"] as? String ?: error("Expected local path")
                require(java.io.File(path).length() <= 64 * 1024 * 1024) { "Source exceeds 64 MiB" }
                fun number(key: String): Double = (args[key] as? Number)?.toDouble()
                    ?.takeIf { it.isFinite() } ?: error("Invalid $key")
                val x = number("x"); val y = number("y")
                val w = number("width"); val h = number("height")
                require(x >= 0 && y >= 0 && w > 0 && h > 0 && x + w <= 1 && y + h <= 1)
                val bounds = BitmapFactory.Options().apply { inJustDecodeBounds = true }
                BitmapFactory.decodeFile(path, bounds)
                require(bounds.outWidth > 0 && bounds.outHeight > 0 &&
                    bounds.outWidth.toLong() * bounds.outHeight <= 40_000_000) { "Invalid image or exceeds 40 megapixels" }
                val bitmap = BitmapFactory.decodeFile(path) ?: error("Cannot decode image")
                try {
                    if (result.isCancelled) return@setHandler
                    val left = floor(x * bitmap.width).toInt()
                    val top = floor(y * bitmap.height).toInt()
                    val right = ceil((x + w) * bitmap.width).toInt().coerceAtMost(bitmap.width)
                    val bottom = ceil((y + h) * bitmap.height).toInt().coerceAtMost(bitmap.height)
                    val cropped = Bitmap.createBitmap(bitmap, left, top, right - left, bottom - top)
                    try {
                        ByteArrayOutputStream().use { output ->
                            check(cropped.compress(Bitmap.CompressFormat.PNG, 100, output)) { "PNG encoding failed" }
                            require(output.size() <= 8 * 1024 * 1024) { "PNG exceeds the 8 MiB result limit. Select a smaller crop." }
                            if (!result.isCancelled) result.success(output.toByteArray())
                        }
                    } finally { if (cropped !== bitmap) cropped.recycle() }
                } finally { bitmap.recycle() }
            } catch (error: Exception) {
                result.error("crop_failed", error.message ?: "Image crop failed")
            }
        }
    }
}
