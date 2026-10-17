using Microsoft.Maui.Controls;

namespace NativeChannels.Maui.ImageCropper;

/// <summary>A modal crop selection page. Push with PushModalAsync, then await Result.</summary>
public sealed class ImageCropperPage : ContentPage
{
    private readonly IImageCropper cropper;
    private readonly string path;
    private readonly TaskCompletionSource<byte[]?> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly CancellationTokenSource lifetime = new();
    private readonly Label status = new();
    private readonly Button save = new() { Text = "Crop", IsEnabled = false };
    private readonly PlatformChannelView nativeView = new() { FactoryName = ImageCropperExtensions.ViewFactoryName };

    public Task<byte[]?> Result => completion.Task;

    public ImageCropperPage(IImageCropper cropper, string sourcePath)
    {
        this.cropper = cropper ?? throw new ArgumentNullException(nameof(cropper));
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        path = sourcePath;
        Title = "Crop image";
        var cancel = new Button { Text = "Cancel" };
        cancel.Clicked += async (_, _) => await CloseAsync(null);
        save.Clicked += SaveClicked;
        nativeView.Connected += async (_, _) => await LoadPreviewAsync();
        var reset = new Button { Text = "Reset selection" };
        reset.Clicked += async (_, _) =>
        {
            try
            {
                if (nativeView.Channel is { } channel) await channel.InvokeAsync("reset", cancellationToken: lifetime.Token);
            }
            catch (OperationCanceledException) { }
            catch (Exception error) { status.Text = error.Message; }
        };
        var layout = new Grid
        {
            Padding = 20, RowSpacing = 12,
            RowDefinitions = { new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto) }
        };
        layout.Add(nativeView);
        layout.Add(status, 0, 1);
        var actions = new HorizontalStackLayout { Spacing = 12 };
        actions.Add(reset); actions.Add(save); actions.Add(cancel);
        layout.Add(actions, 0, 2);
        Content = layout;
    }

    private async Task LoadPreviewAsync()
    {
        try
        {
            // Strip orientation metadata so the preview matches the encoded-coordinate crop contract.
            var png = await cropper.CropAsync(path, CropRegion.Full, lifetime.Token);
            if (lifetime.IsCancellationRequested) return;
            var channel = nativeView.Channel ?? throw new InvalidOperationException("Crop view is disconnected.");
            await channel.InvokeAsync("load", png, lifetime.Token);
            save.IsEnabled = true;
        }
        catch (OperationCanceledException) { }
        catch (Exception error) { status.Text = error.Message; }
    }


    private async void SaveClicked(object? sender, EventArgs e)
    {
        save.IsEnabled = false;
        try
        {
            var channel = nativeView.Channel ?? throw new InvalidOperationException("Crop view is disconnected.");
            var png = await channel.InvokeAsync<byte[]>("crop", cancellationToken: lifetime.Token);
            if (png is not { Length: > 0 }) throw new InvalidOperationException("The cropper returned no image.");
            await CloseAsync(png);
        }
        catch (OperationCanceledException) { }
        catch (Exception error) { status.Text = error.Message; }
        finally { if (!lifetime.IsCancellationRequested) save.IsEnabled = true; }
    }

    private async Task CloseAsync(byte[]? result)
    {
        if (completion.Task.IsCompleted) return;
        completion.TrySetResult(result);
        lifetime.Cancel();
        if (Navigation.ModalStack.LastOrDefault() == this) await Navigation.PopModalAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        lifetime.Cancel();
        completion.TrySetResult(null);
    }

}
