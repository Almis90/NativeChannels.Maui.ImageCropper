# NativeChannels.Maui.ImageCropper

A .NET 10 MAUI image cropper built on **NativeChannels.Maui 0.1.0-preview.2**. Includes a typed service, an optional MAUI selection page, native adapters, a sample app, and NuGet packaging configuration.

| Platform | Image implementation | Minimum OS |
| --- | --- | --- |
| Android | Kotlin / BitmapFactory + CanHub CropImageView | Android 7.0 / API 24 |
| iOS | Swift / ImageIO + Mantis native crop UI | iOS 15 |
| macOS through Mac Catalyst | Same Swift adapter and Mantis UI | Mac Catalyst 15 |
| Windows | C# / Windows.Graphics.Imaging | Windows 10 build 17763 |

These are all four platforms supported by NativeChannels.Maui. Tizen, Linux, browser, and AppKit macOS are not supported by that dependency. This repository has not been published to NuGet.

## Register

After building/installing the plugin package, register the module inside your application's **single** `UseNativeChannels` call and register the service with DI:

```csharp
using NativeChannels;
using NativeChannels.Maui.ImageCropper;

builder.UseNativeChannels(options => options.AddImageCropper());
builder.Services.AddImageCropper();
```

Inject `IImageCropper` and crop a local file:

```csharp
byte[] png = await cropper.CropAsync(
    sourcePath,
    new CropRegion(0.25, 0.25, 0.5, 0.5), // center half of the image
    cancellationToken);
await File.WriteAllBytesAsync(outputPath, png, cancellationToken);
```

To show the optional selection screen from a MAUI page on the UI thread:

```csharp
var page = new ImageCropperPage(cropper, sourcePath);
await Navigation.PushModalAsync(page);
byte[]? png = await page.Result; // null on Cancel or dismissal
```

On Windows the screen embeds CommunityToolkit.WinUI.Controls.ImageCropper 8.2.251219 through a NativeChannels PlatformChannelView, with draggable handles and a Reset selection button. The toolkit is a Windows-only transitive NuGet dependency; consuming apps need no extra package reference. On Android it embeds CanHub Android-Image-Cropper 4.7.0 through the same NativeChannels view contract, with draggable selection and zoom. iOS and Mac Catalyst embed Mantis 3.1.0 using UIKit controller containment through NativeChannels. Mantis provides zoom, rotation and aspect-ratio tools; the MAUI Crop, Reset selection and Cancel buttons use the same channel commands on every platform. The Android view exposes rectangular cropping without rotation or flip commands. It is a one-use modal page. Source and result ownership belong to the application; the service never overwrites a file. DI disposes the singleton's channel handle. If constructing `ImageCropper` yourself, dispose it yourself; pages do not dispose an injected service.

## Image contract and limitations

- The rectangle is normalized `[0,1]`, measured from the top left of the **stored pixel grid**. EXIF rotation is ignored, including mirrored orientations. The selection screen previews a metadata-free PNG of the same pixels. Camera photos may therefore appear sideways; normalize them beforehand if upright orientation is needed.
- Pixel bounds round outward: floor left/top, ceil right/bottom. The result is always PNG, with alpha where supported. Input metadata is not copied; animated images use their first frame. PNG and JPEG are the common cross-platform formats; other formats depend on OS decoders.
- Limits: 64 MiB source file, 40 million decoded pixels, 8 MiB encoded result. The result cap leaves room for base64 within NativeChannels' default 16 MiB message limit. Full-image preview also uses this cap, so large PNG results require preprocessing before opening the page.
- The source must be an app-readable local path. For a picker result, copy `FileResult.OpenReadAsync()` to app cache first: `FullPath` may be a content URI. See the sample. No storage permissions are needed for the plugin's app-owned files; the host owns picker/camera permissions and platform entitlements.
- Invalid managed inputs throw argument/file exceptions before dispatch. Native decode, bounds, size, and encoding failures use `ChannelException` with code `crop_failed`. Missing module registration produces a NativeChannels channel error.
- Calls use the NativeChannels default timeout (30 seconds unless configured by the host). Cancellation stops awaiting a result and is checked around native work. An in-progress OS decode/encode is not forcibly interrupted. There are no native output-file side effects. Avoid many simultaneous large-image calls because decoding uses memory proportional to pixel count.

Channel: `nativechannels.maui.imagecropper`; method: `crop`; arguments: a map of `path`, `x`, `y`, `width`, `height`; result: bytes. Maps and primitives avoid reflection-based DTO serialization in trimmed builds.

## Build and run

Requirements: .NET 10 SDK, relevant MAUI workloads, Android SDK 36 + JDK 17 + Gradle 8.13 for Android, and macOS + Xcode 26 with the relevant Apple .NET workloads for Apple targets. The published NativeChannels package requires Microsoft.Maui.Controls 10.0.110 or newer; this project pins 10.0.110.

Portable contract tests (no MAUI workload needed):

```sh
dotnet run --project tests/ImageCropper.Tests -c Release
```

Windows:

```sh
dotnet workload install maui-windows
dotnet run --project tests/ImageCropper.Tests -c Release -p:TestWindows=true
dotnet build samples/ImageCropper.Sample -c Release -p:TargetFrameworks=net10.0-windows10.0.19041.0
dotnet build samples/ImageCropper.Sample -t:Run -f net10.0-windows10.0.19041.0 -p:TargetFrameworks=net10.0-windows10.0.19041.0
```

Android adapter, then managed sample:

```sh
python eng/prepare-native.py
python eng/build-android.py
dotnet workload install maui-android
dotnet build samples/ImageCropper.Sample -c Release -f net10.0-android -p:TargetFrameworks=net10.0-android -p:RuntimeIdentifier=android-arm64
```

On a Mac, build the Apple adapter and managed samples:

```sh
python3 eng/prepare-native.py
python3 eng/build-apple.py
dotnet workload install maui
dotnet build samples/ImageCropper.Sample -c Release -p:TargetFrameworks=net10.0-ios -p:RuntimeIdentifier=iossimulator-arm64
dotnet build samples/ImageCropper.Sample -c Release -p:TargetFrameworks=net10.0-maccatalyst
```

The prepare script downloads the pinned NuGet bridge packages and extracts compile-time inputs. It does not build or redistribute a second bridge. NativeChannels supplies the bridge and Kotlin runtime transitively at application build time. The Android build also runs Robolectric channel/view tests and stages CanHub's AAR alongside the adapter. CanHub is packaged without a C# binding; Android-only NuGet references supply its AndroidX and Kotlin dependencies. The Apple build downloads checksum-verified Mantis 3.1.0 source at commit `114b303e2ed4c5de37712922216f7b9263d11534` from Mantis-spm and compiles a separate `Mantis.xcframework` alongside the adapter, including localizations, its privacy manifest, and MIT license. The package imports both frameworks only on iOS/Mac Catalyst; consumers need no Swift or C# binding project. Native outputs live in `artifacts/native/android` and `artifacts/native/apple`.

The sample defaults to a project reference with explicit native imports for development. For a **separate package-consumer check**, build the package, then restore/build the sample with `-p:UseLocalPackage=true`, `--source <absolute-path-to-package-feed> --source https://api.nuget.org/v3/index.json`, and a fresh `--packages <empty-cache-directory>`. Package mode disables all source-tree native imports.

## Package

The SDK-style library follows Microsoft's MAUI class-library and multi-targeting structure. Its `buildTransitive` targets bring the AAR/XCFramework into consuming applications. Windows adapters live in the independent `native/windows/NativeChannels.Maui.ImageCropper.Windows.csproj` project. It owns the WinUI toolkit reference and builds its own assembly and NuGet package. The main plugin references it only for Windows; there is no reverse project dependency. SDK packing records the adapter package as a Windows-only dependency. Pack validates that both mobile adapters exist; it will not silently produce a managed-only package.

Build all four managed targets in Release with the appropriate host toolchains, retaining the same source revision and configuration. For a full package, collect their `bin/Release/<TFM>` outputs and native artifacts on a packaging host with the required .NET workload packs installed, then run:

```sh
dotnet pack native/windows/NativeChannels.Maui.ImageCropper.Windows.csproj -c Release -o artifacts/packages
dotnet pack src/NativeChannels.Maui.ImageCropper -c Release --no-build -o artifacts/packages
python eng/inspect-package.py artifacts/packages/NativeChannels.Maui.ImageCropper.0.1.0-preview.nupkg
```

Keep both packages in the same feed and publish the Windows adapter package alongside the main plugin. Consumers still install only the main package; NuGet restores the adapter and toolkit transitively for Windows.

Cross-host builds must preserve each platform's assemblies; do not publish a package made with a single-target `TargetFrameworks` override as the all-platform release. Run the package-consumer checks on each platform before publishing. The Build and verify workflow compiles each platform, combines four SDK-produced package slices, audits the final package, and builds separate NuGet consumers on Windows, Android, iOS and Mac Catalyst. The manually dispatched Publish NuGet preview workflow runs those checks before publishing both packages through NuGet trusted publishing as Almis. It does not claim device execution.

## Verification in this workspace

- Portable tests: 14 assertions passed, exercising the real NativeChannels in-memory transport, argument serialization, binary replies, invalid rectangles, missing files, native errors, cancellation, and disposal.
- Windows tests: 20 assertions passed, including a real Windows decoder/encoder crop, dimensions, selected pixel color, outward rounding of fractional bounds, native bounds validation, and corrupt-image errors.
- Windows plugin and sample: Release builds passed with no warnings or errors. The toolkit control was exercised in the running sample: loaded the bundled image, dragged a corner handle, and saved the cropped PNG.
- Android: CanHub adapter Release AAR built; four Robolectric tests passed through the real NativeChannels bridge, covering crop output, reset, instance isolation, invalid inputs, cancellation, and disposal. The Android ARM64 sample Release APK built with zero warnings/errors, and its DEX files contain the adapter factory, crop module, and CanHub view. Device touch interaction remains unverified.
- iOS and Mac Catalyst: Mantis adapter, native view registration, and framework packaging implemented. Apple compilation and simulator/device execution remain unverified on this Windows machine; the macOS CI job builds both frameworks and both managed Apple targets. A successful Apple CI build and UI smoke test are required before publishing.
- The separate Windows adapter `.nupkg` has been built locally. No all-platform `.nupkg` has been produced and nothing has been published.

## References

- [Microsoft: MAUI class-library project structure](https://learn.microsoft.com/dotnet/maui/migration/multi-project-to-multi-project)
- [Microsoft: MAUI multi-targeting](https://learn.microsoft.com/dotnet/maui/platform-integration/configure-multi-targeting)
- [NativeChannels plugin-authoring guide](https://github.com/Almis90/native-channels/blob/main/docs/plugin-authoring.md)
- [Mantis](https://github.com/guoyingtao/Mantis)
- [CanHub Android-Image-Cropper](https://github.com/CanHub/Android-Image-Cropper)
- [NativeChannels.Maui package](https://www.nuget.org/packages/NativeChannels.Maui/0.1.0-preview.2)





### Apple UI smoke check

On a Mac, build both frameworks and run the sample on an iOS simulator and Mac Catalyst. Open the bundled image, drag and zoom the selection, rotate it, crop, and check the saved PNG. Also test Reset selection, Cancel/back, reopening the page, and rotating/resizing the host window. Reset recreates the native editor from the original preview, clearing all transforms. Each page owns its controller and channel state; closing removes UIKit containment and releases the original image.

## Publishing

Run the **Publish NuGet preview** GitHub Actions workflow on `main`. It publishes version `0.1.0-preview` only after every build and package-consumer job succeeds. NuGet trusts `Almis90/image-cropper` and `publish.yml` for exactly `NativeChannels.Maui.ImageCropper` and `NativeChannels.Maui.ImageCropper.Windows`; no long-lived API key is stored. Platform-only package slices are intermediate build artifacts and must never be published individually.
