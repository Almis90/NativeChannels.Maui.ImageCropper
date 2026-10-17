using global::Windows.Storage.Streams;

namespace NativeChannels.ImageCropper.Native.Windows;

internal static class ImageStreams
{
    public const int MaximumSourceBytes = 64 * 1024 * 1024;
    public const int MaximumPngBytes = 8 * 1024 * 1024;
    public const long MaximumPixels = 40_000_000;

    // Streams belong to the caller; helpers dispose only their temporary reader/writer.
    public static async Task WriteAsync(IRandomAccessStream stream, byte[] bytes)
    {
        using var writer = new DataWriter(stream.GetOutputStreamAt(0));
        writer.WriteBytes(bytes);
        await writer.StoreAsync();
        stream.Seek(0);
    }

    public static async Task<byte[]> ReadPngAsync(IRandomAccessStream stream)
    {
        if (stream.Size == 0 || stream.Size > MaximumPngBytes)
            throw new ChannelException("crop_failed", "PNG is empty or exceeds the 8 MiB result limit.", platform: "windows");

        using var reader = new DataReader(stream.GetInputStreamAt(0));
        await reader.LoadAsync((uint)stream.Size);
        var bytes = new byte[(int)stream.Size];
        reader.ReadBytes(bytes);
        return bytes;
    }
}
