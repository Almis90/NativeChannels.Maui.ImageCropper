using global::Windows.Graphics.Imaging;

namespace NativeChannels.Maui.ImageCropper.Windows;

internal sealed record CropRequest(string Path, double X, double Y, double Width, double Height)
{
    public static CropRequest Parse(object? arguments)
    {
        if (arguments is not IDictionary<string, object?> values || values["path"] is not string path)
            throw new ArgumentException("Expected a path and normalized rectangle.");

        var request = new CropRequest(path, Convert.ToDouble(values["x"]), Convert.ToDouble(values["y"]),
            Convert.ToDouble(values["width"]), Convert.ToDouble(values["height"]));

        if (!double.IsFinite(request.X) || !double.IsFinite(request.Y)
            || !double.IsFinite(request.Width) || !double.IsFinite(request.Height)
            || request.X < 0 || request.Y < 0 || request.Width <= 0 || request.Height <= 0
            || request.X + request.Width > 1 || request.Y + request.Height > 1)
            throw new ArgumentException("The normalized rectangle must be contained in [0, 1].");

        return request;
    }

    public BitmapBounds GetPixelBounds(uint imageWidth, uint imageHeight)
    {
        var left = (uint)Math.Floor(X * imageWidth);
        var top = (uint)Math.Floor(Y * imageHeight);
        var right = Math.Min(imageWidth, (uint)Math.Ceiling((X + Width) * imageWidth));
        var bottom = Math.Min(imageHeight, (uint)Math.Ceiling((Y + Height) * imageHeight));
        return new BitmapBounds { X = left, Y = top, Width = right - left, Height = bottom - top };
    }
}
