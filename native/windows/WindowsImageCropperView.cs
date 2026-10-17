using CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using global::Windows.Storage.Streams;
using ToolkitCropper = CommunityToolkit.WinUI.Controls.ImageCropper;

namespace NativeChannels.Maui.ImageCropper.Windows;

internal sealed class WindowsImageCropperView : IDisposable
{
    private readonly ToolkitCropper control = new()
    {
        MinCroppedPixelLength = 1,
        HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch,
        VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Stretch
    };
    private readonly MethodChannel channel;
    private bool disposed;

    private WindowsImageCropperView(WindowsViewContext context)
    {
        channel = context.Channels.GetMethodChannel(context.ChannelName,
            new() { HandlerDispatcher = MauiChannelDispatchers.MainThread });
        channel.SetMethodCallHandler(HandleAsync);
    }

    internal static WindowsNativeView Create(WindowsViewContext context)
    {
        var adapter = new WindowsImageCropperView(context);
        return new WindowsNativeView(adapter.control, adapter.Dispose);
    }

    private async ValueTask<object?> HandleAsync(MethodCall call, CancellationToken cancellation)
    {
        EnsureActive(cancellation);
        switch (call.Method)
        {
            case "load":
                await LoadAsync(call.Arguments, cancellation);
                return null;
            case "crop":
                return await CropAsync(cancellation);
            case "reset":
                control.Reset();
                return null;
            default:
                throw new MethodNotImplementedException(call.Method);
        }
    }

    private async Task LoadAsync(object? arguments, CancellationToken cancellation)
    {
        if (arguments is not byte[] png || png.Length == 0 || png.Length > ImageStreams.MaximumPngBytes)
            throw new ChannelException("invalid_image", "Expected PNG bytes up to 8 MiB.");

        using var input = new InMemoryRandomAccessStream();
        await ImageStreams.WriteAsync(input, png);
        var bitmap = new WriteableBitmap(1, 1);
        await bitmap.SetSourceAsync(input);
        EnsureActive(cancellation);
        control.Source = bitmap;
    }

    private async Task<byte[]> CropAsync(CancellationToken cancellation)
    {
        if (control.Source is null)
            throw new ChannelException("image_not_loaded", "Load an image first.");

        using var output = new InMemoryRandomAccessStream();
        await control.SaveAsync(output, BitmapFileFormat.Png);
        EnsureActive(cancellation);
        return await ImageStreams.ReadPngAsync(output);
    }

    private void EnsureActive(CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(disposed, this);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        channel.Dispose();
        control.Source = null;
    }
}
