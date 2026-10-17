namespace NativeChannels.ImageCropper.Maui;

public interface IImageCropper
{
    /// <summary>Crops a local image and returns PNG bytes. The source is never modified.</summary>
    Task<byte[]> CropAsync(string sourcePath, CropRegion region, CancellationToken cancellationToken = default);
}
