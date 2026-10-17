using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
#if WINDOWS
using NativeChannels.ImageCropper.Maui.Windows;
#endif

namespace NativeChannels.ImageCropper.Maui;

public static class ImageCropperExtensions
{
    internal const string ViewFactoryName = "nativechannels.maui.imagecropper.view";
    public static NativeChannelsOptions AddImageCropper(this NativeChannelsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var module = new NativeModuleRegistration("dev.nativechannels.imagecropper.ImageCropperModule",
            "MCImageCropperModule", "MauiImageCropperNative")
        {
#if WINDOWS
            WindowsFactory = CreateWindowsModule,
#endif
        };
        if (!options.Modules.Contains(module)) options.Modules.Add(module);
        var view = new NativeViewFactoryRegistration(ViewFactoryName,
            "dev.nativechannels.imagecropper.ImageCropperViewFactory", "MCImageCropperViewFactory", "MauiImageCropperNative")
        {
#if WINDOWS
            WindowsFactory = WindowsImageCropperAdapter.CreateView
#endif
        };
        if (!options.ViewFactories.Contains(view)) options.ViewFactories.Add(view);
        return options;
    }

    public static IServiceCollection AddImageCropper(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IImageCropper, ImageCropper>();
        return services;
    }
#if WINDOWS
    private static IWindowsChannelModule CreateWindowsModule() => WindowsImageCropperAdapter.CreateModule();
#endif
}
