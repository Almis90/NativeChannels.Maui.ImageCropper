"""Audit the MAUI facade and independently reusable native packages."""
import sys
from pathlib import Path
import xml.etree.ElementTree as ET
from zipfile import ZipFile

PREFIX = "NativeChannels.ImageCropper.Native."

def inspect(path):
    with ZipFile(path) as package:
        names = package.namelist()
        spec = ET.fromstring(package.read(next(n for n in names if n.endswith(".nuspec"))))
        metadata = next(e for e in spec if e.tag.endswith("metadata"))
        identity = next(e.text for e in metadata if e.tag.endswith("}id"))
        dependencies = {e.attrib["id"] for e in spec.iter() if e.tag.endswith("}dependency")}
        assert "README.md" in names
        if identity == "NativeChannels.Maui.ImageCropper":
            assert not any(n.startswith("native/") for n in names), "Native assets belong in adapter packages"
            for platform, adapter in [("android", "Android"), ("ios", "Apple"), ("maccatalyst", "Apple"), ("windows", "Windows")]:
                assert any(n.startswith(f"lib/net10.0-{platform}") and n.endswith("/NativeChannels.Maui.ImageCropper.dll") for n in names), platform
                groups = [e for e in spec.iter() if e.tag.endswith("}group") and platform in e.get("targetFramework", "").lower()]
                assert len(groups) == 1, platform
                actual = {e.get("id") for e in groups[0] if e.get("id", "").startswith(PREFIX)}
                assert actual == {PREFIX + adapter}, (platform, actual)
        else:
            assert "NativeChannels.Maui" not in dependencies
            assert not any(d.startswith("Microsoft.Maui") for d in dependencies)
            platform = identity.removeprefix(PREFIX)
            if platform == "Windows":
                assert "NativeChannels.Core" in dependencies
                assert "CommunityToolkit.WinUI.Controls.ImageCropper" in dependencies
                assert any(n.endswith("/" + identity + ".dll") for n in names)
            else:
                assert "buildTransitive/" + identity + ".targets" in names
                assert "NativeChannels.Native." + platform in dependencies
            if platform == "Android":
                for asset in ("imagecropper.aar", "android-image-cropper-4.7.0.aar", "CanHub-LICENSE.txt"):
                    assert "native/android/" + asset in names, asset
                for dependency in ("Xamarin.AndroidX.DataBinding.ViewBinding", "Xamarin.AndroidX.ExifInterface", "Xamarin.Kotlin.Parcelize.Runtime", "Xamarin.KotlinX.Coroutines.Android"):
                    assert dependency in dependencies, dependency
            if platform == "Apple":
                assert "native/apple/Mantis-LICENSE.txt" in names
                for framework in ("Mantis", "MauiImageCropperNative"):
                    base = "native/apple/" + framework + ".xcframework/"
                    assert base + "Info.plist" in names
                    for slice in ("ios-arm64", "ios-arm64_x86_64-simulator", "ios-arm64_x86_64-maccatalyst"):
                        prefix = base + slice + "/" + framework + ".framework/"
                        assert prefix + framework in names, prefix + framework
                        if framework == "Mantis":
                            for asset in ("PrivacyInfo.xcprivacy", "LICENSE.txt", "MantisResources.bundle/Info.plist"):
                                assert prefix + asset in names, prefix + asset
                            assert any(n.startswith(prefix + "MantisResources.bundle/") and n.endswith(".strings") for n in names)
    print("Verified " + identity)
    return identity

if __name__ == "__main__":
    path = Path(sys.argv[1])
    if path.is_dir():
        identities = [inspect(p) for p in path.glob("*.nupkg")]
        assert len(identities) == 4
        assert set(identities) == {"NativeChannels.Maui.ImageCropper", *(PREFIX + p for p in ("Android", "Apple", "Windows"))}
    else:
        inspect(path)
