namespace NativeChannels.ImageCropper.Maui;

/// <summary>A normalized rectangle in the encoded image's pixel coordinate system (EXIF orientation is ignored).</summary>
public readonly record struct CropRegion(double X, double Y, double Width, double Height)
{
    public static CropRegion Full => new(0, 0, 1, 1);

    public void Validate()
    {
        if (!double.IsFinite(X) || !double.IsFinite(Y) || !double.IsFinite(Width) || !double.IsFinite(Height)
            || X < 0 || Y < 0 || Width <= 0 || Height <= 0 || X + Width > 1 || Y + Height > 1)
            throw new ArgumentOutOfRangeException(nameof(CropRegion), "The rectangle must be finite, positive and contained in [0, 1].");
    }
}
