using NativeChannels.Maui.ImageCropper;

namespace ImageCropper.Sample;

public partial class MainPage : ContentPage
{
    public MainPage() => InitializeComponent();

    private async void TrySampleImage(object? sender, EventArgs e)
    {
        var button = (Button)sender!;
        button.IsEnabled = false;
        var local = Path.Combine(FileSystem.CacheDirectory, Guid.NewGuid() + ".png");
        try
        {
            await using (var input = await FileSystem.OpenAppPackageFileAsync("sample.png"))
            await using (var output = File.Create(local)) await input.CopyToAsync(output);
            await CropImageAsync(local);
        }
        catch (Exception error) { Status.Text = error.Message; }
        finally { File.Delete(local); button.IsEnabled = true; }
    }

    private async void ChooseImage(object? sender, EventArgs e)
    {
        var button = (Button)sender!;
        button.IsEnabled = false;
        string? local = null;
        try
        {
            var file = await FilePicker.Default.PickAsync(new PickOptions { FileTypes = FilePickerFileType.Images });
            if (file is null) return;
            // Picker results can be content URIs: use OpenReadAsync instead of FullPath.
            local = Path.Combine(FileSystem.CacheDirectory, Guid.NewGuid() + ".image");
            await using (var input = await file.OpenReadAsync())
            await using (var output = File.Create(local)) await input.CopyToAsync(output);
            await CropImageAsync(local);
        }
        catch (Exception error) { Status.Text = error.Message; }
        finally
        {
            if (local is not null) File.Delete(local);
            button.IsEnabled = true;
        }
    }

    private async Task CropImageAsync(string local)
    {
        var cropper = Handler!.MauiContext!.Services.GetRequiredService<IImageCropper>();
        var page = new ImageCropperPage(cropper, local);
        await Navigation.PushModalAsync(page);
        var png = await page.Result;
        if (png is null) { Status.Text = "Cancelled"; return; }
        var destination = Path.Combine(FileSystem.AppDataDirectory, "cropped.png");
        await File.WriteAllBytesAsync(destination, png);
        Preview.Source = ImageSource.FromStream(() => new MemoryStream(png, writable: false));
        Status.Text = $"Saved to {destination}";
    }
}
