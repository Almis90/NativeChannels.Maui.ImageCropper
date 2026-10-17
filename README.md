# NativeChannels.Maui.ImageCropper

A .NET 10 MAUI image cropper built on **NativeChannels.Maui 0.1.0-preview.2**. Includes a typed service, an optional MAUI selection page, native adapters, a sample app, and NuGet packaging configuration.

| Platform | Image implementation | Minimum OS |
| --- | --- | --- |
| Android | Kotlin / BitmapFactory + CanHub CropImageView | Android 7.0 / API 24 |
| iOS | Swift / ImageIO + Mantis native crop UI | iOS 15 |
| macOS through Mac Catalyst | Same Swift adapter and Mantis UI | Mac Catalyst 15 |
| Windows | C# / Windows.Graphics.Imaging | Windows 10 build 17763 |

These are all four platforms supported by NativeChannels.Maui. Tizen, Linux, browser, and AppKit macOS are not supported by that dependency. Version `0.1.0-preview` was published with the original two-package layout. Version `0.1.0-preview.2` introduces the native package layout below.

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

The sample defaults to project references with explicit native imports for development. For a separate package-consumer build, place all four packages in `artifacts/packages` and run `python eng/verify-package-consumer.py windows` (or `android` / `apple`). The script generates a NuGet configuration with the local feed and NuGet.org, uses a fresh package cache, and disables source native imports.

## Packages

Install **NativeChannels.Maui.ImageCropper** in a MAUI app. NuGet selects the native dependency for each target:

| Package | Contents |
| --- | --- |
| `NativeChannels.Maui.ImageCropper` | MAUI registration, typed crop service and selection page |
| `NativeChannels.ImageCropper.Native.Android` | Kotlin adapter, CanHub AAR, AndroidX dependencies and native imports |
| `NativeChannels.ImageCropper.Native.Apple` | Swift adapter, Mantis XCFramework, resources and native imports for iOS/Mac Catalyst |
| `NativeChannels.ImageCropper.Native.Windows` | WinUI adapter and toolkit dependency; references NativeChannels.Core, without MAUI |

All four packages use version `0.1.0-preview.2`. Native packages have no dependency on the MAUI facade. Another UI integration can reuse their channel contracts and native assets; a host still needs to provide platform-view hosting. On Windows, create the native view on the WinUI thread with `WindowsImageCropperAdapter.CreateView(channels, channelName)`, embed its `View`, and dispose it when removed. The native view dispatches channel handlers through its WinUI DispatcherQueue. The MAUI facade supplies the MAUI-specific module and view registration wrappers.

The old `NativeChannels.Maui.ImageCropper.Windows` package remains available for consumers of `0.1.0-preview`; it is not a dependency of the new layout.

The Build and verify workflow builds and packs each native adapter on its platform, merges the four managed facade slices, audits all four final packages, then builds package consumers for every platform. The publish workflow pushes the three native dependencies before the MAUI package. Source builds use project references and explicit native imports; package consumers use only NuGet references.

## Verification

Portable channel tests cover validation, serialization, cancellation and disposal. Windows tests additionally exercise real decoding, cropping and PNG output. Android native tests run through Robolectric. CI builds iOS and Mac Catalyst samples, and all platforms are checked again using the final NuGet packages. These build checks do not establish device UI behavior; use the Apple smoke check below on a Mac.

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

Run the **Publish NuGet preview** GitHub Actions workflow on `main`. It publishes `0.1.0-preview.2` only after every build and package-consumer job succeeds. NuGet trusts `Almis90/image-cropper` and `publish.yml` for the four package IDs above; no long-lived API key is stored. Platform-only facade slices are intermediate artifacts and must never be published individually.
