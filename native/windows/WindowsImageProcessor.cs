using global::Windows.Graphics.Imaging;
using global::Windows.Storage.Streams;

namespace NativeChannels.ImageCropper.Maui.Windows;

internal static class WindowsImageProcessor
{
    public static async Task<byte[]> CropAsync(CropRequest request, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        if (new FileInfo(request.Path).Length > ImageStreams.MaximumSourceBytes)
            throw new ArgumentException("Source exceeds 64 MiB.");

        // Decode a memory copy so the OS decoder cannot retain a lock on the source file.
        var source = await File.ReadAllBytesAsync(request.Path, cancellation);
        using var input = new InMemoryRandomAccessStream();
        await ImageStreams.WriteAsync(input, source);
        var decoder = await BitmapDecoder.CreateAsync(input);
        if ((long)decoder.PixelWidth * decoder.PixelHeight > ImageStreams.MaximumPixels)
            throw new ArgumentException("Image exceeds 40 megapixels.");

        var transform = new BitmapTransform { Bounds = request.GetPixelBounds(decoder.PixelWidth, decoder.PixelHeight) };
        using var bitmap = await decoder.GetSoftwareBitmapAsync(
            BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, transform,
            ExifOrientationMode.IgnoreExifOrientation, ColorManagementMode.ColorManageToSRgb);
        cancellation.ThrowIfCancellationRequested();

        using var output = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, output);
        encoder.SetSoftwareBitmap(bitmap);
        await encoder.FlushAsync();
        cancellation.ThrowIfCancellationRequested();
        return await ImageStreams.ReadPngAsync(output);
    }
}
