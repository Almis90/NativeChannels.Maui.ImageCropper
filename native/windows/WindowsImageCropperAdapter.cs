namespace NativeChannels.ImageCropper.Native.Windows;

/// <summary>Entry points used by the MAUI facade to register the independent Windows adapter.</summary>
public static class WindowsImageCropperAdapter
{
    public const string ChannelName = "nativechannels.maui.imagecropper";
    public const string ViewFactoryName = "nativechannels.maui.imagecropper.view";

    public static WindowsImageCropperModule CreateModule() => new WindowsImageCropperModule();

    public static WindowsImageCropperView CreateView(IChannelService channels, string channelName) => new(channels, channelName);
}
