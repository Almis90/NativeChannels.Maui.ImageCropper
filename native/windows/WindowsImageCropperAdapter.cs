namespace NativeChannels.Maui.ImageCropper.Windows;

/// <summary>Entry points used by the MAUI facade to register the independent Windows adapter.</summary>
public static class WindowsImageCropperAdapter
{
    public const string ChannelName = "nativechannels.maui.imagecropper";
    public const string ViewFactoryName = "nativechannels.maui.imagecropper.view";

    public static IWindowsChannelModule CreateModule() => new WindowsImageCropperModule();

    public static WindowsNativeView CreateView(WindowsViewContext context) => WindowsImageCropperView.Create(context);
}
