using NativeChannels;
using NativeChannels.ImageCropper.Maui;

int assertions = 0;
void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
    assertions++;
}
async Task Throws<T>(Func<Task> work) where T : Exception
{
    try { await work(); } catch (T) { assertions++; return; }
    throw new Exception($"Expected {typeof(T).Name}");
}

var (managed, native) = InMemoryTransport.CreatePair();
using var client = new ChannelService(new BinaryMessenger(managed));
using var server = new ChannelService(new BinaryMessenger(native));
using var endpoint = server.GetMethodChannel("nativechannels.maui.imagecropper");
using var cropper = new ImageCropper(client);
string file = Path.GetTempFileName();
try
{
    foreach (var invalid in new[] { new CropRegion(-.1, 0, 1, 1), new CropRegion(0, 0, 0, 1),
        new CropRegion(0, 0, double.NaN, 1), new CropRegion(0, double.PositiveInfinity, 1, 1), new CropRegion(.5, 0, .6, 1) })
        await Throws<ArgumentOutOfRangeException>(() => cropper.CropAsync(file, invalid));
    await Throws<FileNotFoundException>(() => cropper.CropAsync(file + "missing", CropRegion.Full));
    endpoint.RemoveMethodCallHandler();
    endpoint.SetMethodCallHandler((call, _) =>
    {
        Check(call.Method == "crop", "Method contract");
        var arguments = (IDictionary<string, object?>)call.Arguments!;
        Check((string)arguments["path"]! == file, "Path contract");
        Check(Convert.ToDouble(arguments["width"]) == .5, "Rectangle contract");
        return ValueTask.FromResult<object?>(new byte[] { 1, 2, 3 });
    });
    Check((await cropper.CropAsync(file, new(0, 0, .5, 1))).SequenceEqual(new byte[] { 1, 2, 3 }), "Binary result");
    endpoint.RemoveMethodCallHandler();
    endpoint.SetMethodCallHandler((_, _) => ValueTask.FromResult<object?>(Array.Empty<byte>()));
    await Throws<ChannelSerializationException>(() => cropper.CropAsync(file, CropRegion.Full));
    endpoint.RemoveMethodCallHandler();
    endpoint.SetMethodCallHandler((_, _) => throw new ChannelException("crop_failed", "Bad image"));
    await Throws<ChannelException>(() => cropper.CropAsync(file, CropRegion.Full));
    endpoint.RemoveMethodCallHandler();
    endpoint.SetMethodCallHandler(async (_, token) => { await Task.Delay(10_000, token); return null; });
    using var cancellation = new CancellationTokenSource(100);
    await Throws<OperationCanceledException>(() => cropper.CropAsync(file, CropRegion.Full, cancellation.Token));
    endpoint.RemoveMethodCallHandler();
#if WINDOWS
    using var module = NativeChannels.ImageCropper.Maui.Windows.WindowsImageCropperAdapter.CreateModule();
    module.Register(server);
    // A 2x2 RGBA PNG with red/green on the top row and blue/white on the bottom row.
    await File.WriteAllBytesAsync(file, Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAIAAAACCAYAAABytg0kAAAAEklEQVR4nGP4z8DwHwyBNBgAAEnICff5q7YNAAAAAElFTkSuQmCC"));
    var bytes = await cropper.CropAsync(file, new(.5, 0, .5, 1));
    Check(System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4)) == 1, "Native crop width");
    Check(System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20, 4)) == 2, "Native crop height");
    using var stream = new Windows.Storage.Streams.InMemoryRandomAccessStream();
    using (var writer = new Windows.Storage.Streams.DataWriter(stream.GetOutputStreamAt(0)))
    { writer.WriteBytes(bytes); await writer.StoreAsync(); }
    var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(stream);
    var pixels = (await decoder.GetPixelDataAsync()).DetachPixelData();
    Check(pixels[0] == 0 && pixels[1] == 255 && pixels[2] == 0, "Correct green pixel selected");
    var fractional = await cropper.CropAsync(file, new(.1, .1, .5, .5));
    Check(System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(fractional.AsSpan(16, 4)) == 2
        && System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(fractional.AsSpan(20, 4)) == 2,
        "Fractional pixel bounds round outward");
    using (var direct = client.GetMethodChannel("nativechannels.maui.imagecropper"))
    {
        try
        {
            await direct.InvokeAsync<byte[]>("crop", new Dictionary<string, object?>
            {
                ["path"] = file, ["x"] = .75, ["y"] = 0d, ["width"] = .5, ["height"] = 1d
            });
            throw new Exception("The native adapter accepted an out-of-bounds region.");
        }
        catch (ChannelException error)
        {
            Check(error.Code == "crop_failed", "Native validation preserves the crop_failed error contract");
        }
    }
    await File.WriteAllTextAsync(file, "not an image");
    await Throws<ChannelException>(() => cropper.CropAsync(file, CropRegion.Full));
#endif
    cropper.Dispose();
    await Throws<ObjectDisposedException>(() => cropper.CropAsync(file, CropRegion.Full));
    Console.WriteLine($"Passed {assertions} assertions.");
}
finally { File.Delete(file); }
