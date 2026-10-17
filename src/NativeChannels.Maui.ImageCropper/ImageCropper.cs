namespace NativeChannels.Maui.ImageCropper;

public sealed class ImageCropper(IChannelService channels) : IImageCropper, IDisposable
{
    internal const string ChannelName = "nativechannels.maui.imagecropper";
    private readonly MethodChannel channel = channels.GetMethodChannel(ChannelName);

    public async Task<byte[]> CropAsync(string sourcePath, CropRegion region, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        region.Validate();
        cancellationToken.ThrowIfCancellationRequested();
        var path = Path.GetFullPath(sourcePath);
        if (!File.Exists(path)) throw new FileNotFoundException("Source image was not found.", path);
        var result = await channel.InvokeAsync<byte[]>("crop", new Dictionary<string, object?>
        {
            ["path"] = path, ["x"] = region.X, ["y"] = region.Y,
            ["width"] = region.Width, ["height"] = region.Height
        }, cancellationToken).ConfigureAwait(false);
        if (result is not { Length: > 0 }) throw new ChannelSerializationException("The crop adapter returned no image.");
        return result;
    }

    public void Dispose() => channel.Dispose();
}
