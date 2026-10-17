"""Fail if an all-platform package is missing managed or native assets."""
import sys
import xml.etree.ElementTree as ET
from zipfile import ZipFile

with ZipFile(sys.argv[1]) as package:
    names = package.namelist()
    for platform in ("android", "ios", "maccatalyst", "windows"):
        assert any(n.startswith(f"lib/net10.0-{platform}") and n.endswith("/NativeChannels.Maui.ImageCropper.dll") for n in names), platform
    for required in ("README.md", "native/android/imagecropper.aar", "native/android/android-image-cropper-4.7.0.aar",
                     "native/android/CanHub-LICENSE.txt", "native/apple/MauiImageCropperNative.xcframework/Info.plist",
                     "native/apple/Mantis.xcframework/Info.plist", "native/apple/Mantis-LICENSE.txt",
                     "buildTransitive/NativeChannels.Maui.ImageCropper.targets"):
        assert required in names, required
    for platform in ("ios-arm64", "ios-arm64_x86_64-simulator", "ios-arm64_x86_64-maccatalyst"):
        assert any(f"/{platform}/" in n and n.endswith("/MauiImageCropperNative") for n in names), platform
        prefix = f"native/apple/Mantis.xcframework/{platform}/Mantis.framework/"
        for asset in ("Mantis", "PrivacyInfo.xcprivacy", "LICENSE.txt", "MantisResources.bundle/Info.plist"):
            assert prefix + asset in names, prefix + asset
        assert any(n.startswith(prefix + "MantisResources.bundle/") and n.endswith(".strings") for n in names), "Missing Mantis localizations"
    spec = ET.fromstring(package.read(next(n for n in names if n.endswith(".nuspec"))))
    assert any(e.attrib.get("id") == "NativeChannels.Maui" for e in spec.iter()), "Missing NativeChannels dependency"
    for group in (e for e in spec.iter() if e.tag.endswith("group")):
        dependencies = [e.attrib.get("id") for e in group]
        if "windows" in group.attrib.get("targetFramework", "").lower():
            assert "NativeChannels.Maui.ImageCropper.Windows" in dependencies, "Missing Windows adapter dependency"
        else:
            assert "NativeChannels.Maui.ImageCropper.Windows" not in dependencies, "Windows adapter leaked into another platform"
        if "android" in group.attrib.get("targetFramework", "").lower():
            for dependency in ("Xamarin.AndroidX.DataBinding.ViewBinding", "Xamarin.AndroidX.ExifInterface",
                               "Xamarin.Kotlin.Parcelize.Runtime", "Xamarin.KotlinX.Coroutines.Android"):
                assert dependency in dependencies, f"Missing CanHub runtime dependency: {dependency}"
print("All-platform package assets verified.")
