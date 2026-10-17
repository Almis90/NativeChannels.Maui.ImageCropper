package dev.nativechannels.imagecropper

import android.graphics.Bitmap
import android.graphics.BitmapFactory
import android.graphics.Color
import android.graphics.Rect
import android.os.Looper
import android.view.ContextThemeWrapper
import android.view.View
import com.canhub.cropper.CropImageView
import dev.nativechannels.JsonMethodCodec
import dev.nativechannels.NativeBridge
import dev.nativechannels.NativeViewInstance
import org.junit.Assert.*
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner
import org.robolectric.RuntimeEnvironment
import org.robolectric.Shadows.shadowOf
import org.robolectric.annotation.Config
import org.robolectric.annotation.GraphicsMode
import org.robolectric.annotation.LooperMode
import java.io.ByteArrayOutputStream
import java.util.UUID
import java.util.concurrent.CompletableFuture

@RunWith(RobolectricTestRunner::class)
@Config(sdk = [28])
@LooperMode(LooperMode.Mode.PAUSED)
@GraphicsMode(GraphicsMode.Mode.NATIVE)
class ImageCropperViewTests {
    @Test fun cropReturnsSelectedPixelsAndResetRestoresSelection() {
        Session().use { session ->
            session.call("load", image(Color.GREEN))
            session.layout()
            session.view.isAutoZoomEnabled = false
            session.view.cropRect = Rect(30, 25, 60, 55)
            shadowOf(Looper.getMainLooper()).idle()
            val bounds = session.view.cropRect!!
            val bytes = session.call("crop") as ByteArray
            val cropped = BitmapFactory.decodeByteArray(bytes, 0, bytes.size)
            assertEquals(bounds.width(), cropped.width)
            assertEquals(bounds.height(), cropped.height)
            assertEquals(Color.GREEN, cropped.getPixel(0, 0))
            cropped.recycle()
            session.call("reset")
            val initial = Rect(session.view.cropRect!!)
            session.call("reset")
            assertEquals(initial, session.view.cropRect)
        }
    }

    @Test fun viewInstancesKeepTheirOwnImage() {
        Session().use { first ->
            Session().use { second ->
                first.call("load", image(Color.RED))
                second.call("load", image(Color.BLUE))
                first.layout(); second.layout()
                for ((session, color) in listOf(first to Color.RED, second to Color.BLUE)) {
                    val bytes = session.call("crop") as ByteArray
                    val bitmap = BitmapFactory.decodeByteArray(bytes, 0, bytes.size)
                    assertEquals(color, bitmap.getPixel(0, 0))
                    bitmap.recycle()
                }
            }
            // Closing another instance must not dispose this view's bridge or bitmap.
            assertTrue(first.call("crop") is ByteArray)
        }
    }

    @Test fun invalidInputsAndDetachedViewReportErrors() {
        Session().use { session ->
            session.assertError("image_not_loaded", "crop")
            session.assertError("invalid_image", "load", "not bytes")
            session.assertError("crop_failed", "load", byteArrayOf(1, 2, 3))
            session.instance.close()
            session.assertError("view_disposed", "reset")
            session.instance.close() // Idempotent cleanup.
        }
    }

    @Test fun cancelledLoadDoesNotPopulateTheView() {
        Session().use { session ->
            val (id, reply) = session.send("load", image(Color.RED))
            session.bridge.cancel(id)
            val (status, _) = await(reply)
            assertEquals(4, status)
            shadowOf(Looper.getMainLooper()).idle()
            assertNull(session.view.wholeImageRect)
        }
    }

    private class Session : AutoCloseable {
        val bridge = NativeBridge()
        private val channel = "test.crop.${UUID.randomUUID()}"
        val instance: NativeViewInstance = ImageCropperViewFactory().create(
            ContextThemeWrapper(RuntimeEnvironment.getApplication(), androidx.appcompat.R.style.Theme_AppCompat),
            bridge, UUID.randomUUID().toString(), channel, byteArrayOf(),
        )
        val view get() = instance.view as CropImageView

        fun layout() {
            view.measure(View.MeasureSpec.makeMeasureSpec(400, View.MeasureSpec.EXACTLY),
                View.MeasureSpec.makeMeasureSpec(400, View.MeasureSpec.EXACTLY))
            view.layout(0, 0, 400, 400)
        }

        fun send(method: String, arguments: Any? = null): Pair<String, CompletableFuture<Pair<Int, ByteArray>>> {
            val id = UUID.randomUUID().toString()
            val reply = CompletableFuture<Pair<Int, ByteArray>>()
            bridge.sendRequest(id, channel, JsonMethodCodec.encodeCall(method, arguments)) { status, payload ->
                reply.complete(status to payload)
            }
            return id to reply
        }

        fun call(method: String, arguments: Any? = null): Any? {
            val (status, payload) = await(send(method, arguments).second)
            assertEquals(0, status)
            return JsonMethodCodec.result(payload)
        }

        fun assertError(code: String, method: String, arguments: Any? = null) {
            try {
                call(method, arguments)
                fail("Expected $code")
            } catch (error: dev.nativechannels.ChannelError) {
                assertEquals(code, error.code)
            }
        }

        override fun close() {
            instance.close()
            bridge.close()
        }
    }

    companion object {
        private fun image(color: Int): ByteArray {
            val bitmap = Bitmap.createBitmap(100, 80, Bitmap.Config.ARGB_8888)
            bitmap.eraseColor(color)
            return try {
                ByteArrayOutputStream().use { stream ->
                    bitmap.compress(Bitmap.CompressFormat.PNG, 100, stream)
                    stream.toByteArray()
                }
            } finally { bitmap.recycle() }
        }

        private fun <T> await(future: CompletableFuture<T>): T {
            val deadline = System.nanoTime() + 10_000_000_000L
            while (!future.isDone && System.nanoTime() < deadline) {
                shadowOf(Looper.getMainLooper()).idle()
                Thread.sleep(5)
            }
            assertTrue("Native reply timed out", future.isDone)
            return future.get()
        }
    }
}
