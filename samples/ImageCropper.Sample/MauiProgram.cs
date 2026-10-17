using Microsoft.Extensions.Logging;
using NativeChannels;
using NativeChannels.ImageCropper.Maui;

namespace ImageCropper.Sample;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

#if DEBUG
		builder.Logging.AddDebug();
#endif

		builder.UseNativeChannels(options => options.AddImageCropper());
        builder.Services.AddImageCropper();
        return builder.Build();
	}
}
