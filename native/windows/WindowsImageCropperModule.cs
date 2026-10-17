namespace NativeChannels.ImageCropper.Maui.Windows;

internal sealed class WindowsImageCropperModule : IWindowsChannelModule
{
    private MethodChannel? channel;

    public void Register(IChannelService channels)
    {
        channel = channels.GetMethodChannel(WindowsImageCropperAdapter.ChannelName);
        channel.SetMethodCallHandler(HandleAsync);
    }

    private static async ValueTask<object?> HandleAsync(MethodCall call, CancellationToken cancellation)
    {
        if (call.Method != "crop") throw new MethodNotImplementedException(call.Method);

        try
        {
            return await WindowsImageProcessor.CropAsync(CropRequest.Parse(call.Arguments), cancellation);
        }
        catch (OperationCanceledException) { throw; }
        catch (ChannelException) { throw; }
        catch (Exception error)
        {
            throw new ChannelException("crop_failed", error.Message, platform: "windows");
        }
    }

    public void Dispose() => Interlocked.Exchange(ref channel, null)?.Dispose();
}
