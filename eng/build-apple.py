"""Build Mantis and the NativeChannels adapter XCFrameworks on macOS/Xcode."""
from pathlib import Path
from urllib.request import urlretrieve
from zipfile import ZipFile
import hashlib
import plistlib
import shutil
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "artifacts/native/apple"
BRIDGE = ROOT / "artifacts/dependencies/apple/NativeChannelsNative.xcframework"
# Mantis-spm v3.1.0, pinned to the peeled tag commit and archive checksum.
MANTIS_COMMIT = "114b303e2ed4c5de37712922216f7b9263d11534"
MANTIS_SHA256 = "5bac9df8d9c8ee7db50e6b9403fcdec645c94b3b8fd6f30baf0fc21bbcd3608f"
SLICES = [
    ("iphoneos", "", "ios-arm64", ["arm64"]),
    ("iphonesimulator", "-simulator", "ios-arm64_x86_64-simulator", ["arm64", "x86_64"]),
    ("macosx", "-macabi", "ios-arm64_x86_64-maccatalyst", ["arm64", "x86_64"]),
]


def run(*args):
    subprocess.run([str(a) for a in args], check=True)


def prepare_mantis():
    dependencies = ROOT / "artifacts/dependencies"
    dependencies.mkdir(parents=True, exist_ok=True)
    archive = dependencies / "mantis-3.1.0.zip"
    if not archive.exists():
        urlretrieve(f"https://codeload.github.com/guoyingtao/Mantis-spm/zip/{MANTIS_COMMIT}", archive)
    if hashlib.sha256(archive.read_bytes()).hexdigest() != MANTIS_SHA256:
        raise SystemExit(f"Mantis checksum mismatch: {archive}")
    with ZipFile(archive) as package:
        package.extractall(dependencies / "mantis-pinned")
    return dependencies / "mantis-pinned" / f"Mantis-spm-{MANTIS_COMMIT}"


def write_plist(path, values):
    with path.open("wb") as stream:
        plistlib.dump(values, stream)


def compile_framework(name, sources, sdk, sdk_path, suffix, arch, references):
    framework = OUT / f"{sdk}-{arch}" / f"{name}.framework"
    modules = framework / "Modules" / f"{name}.swiftmodule"
    modules.mkdir(parents=True, exist_ok=True)
    (framework / "Headers").mkdir(exist_ok=True)
    triple = f"{arch}-apple-ios{suffix}"
    command = ["xcrun", "--sdk", sdk, "swiftc", "-swift-version", "5", "-parse-as-library",
        "-emit-library", "-emit-module", "-enable-library-evolution", "-module-name", name,
        "-target", f"{arch}-apple-ios15.0{suffix}", "-sdk", sdk_path,
        "-emit-module-path", modules / f"{triple}.swiftmodule",
        "-emit-module-interface-path", modules / f"{triple}.swiftinterface",
        "-emit-objc-header-path", framework / "Headers" / f"{name}-Swift.h",
        "-Xlinker", "-install_name", "-Xlinker", f"@rpath/{name}.framework/{name}",
        "-o", framework / name, *sources]
    for directory, dependency in references:
        command += ["-F", directory, "-framework", dependency]
    if suffix == "-macabi":
        support = sdk_path / "System/iOSSupport"
        command += ["-F", support / "System/Library/Frameworks", "-I", support / "usr/include", "-L", support / "usr/lib"]
    run(*command)
    (framework / "Modules/module.modulemap").write_text(f'framework module {name} {{ header "{name}-Swift.h" export * }}\n')
    write_plist(framework / "Info.plist", {
        "CFBundleIdentifier": "com.echo.framework.Mantis" if name == "Mantis" else f"dev.nativechannels.{name}",
        "CFBundleName": name, "CFBundleExecutable": name, "CFBundlePackageType": "FMWK",
        "CFBundleVersion": "1", "CFBundleShortVersionString": "3.1.0" if name == "Mantis" else "0.1.0",
        "CFBundleDevelopmentRegion": "en", "MinimumOSVersion": "15.0",
        "CFBundleSupportedPlatforms": ["MacOSX" if suffix == "-macabi" else "iPhoneSimulator" if suffix else "iPhoneOS"],
    })
    return framework


def merge(name, sdk, frameworks):
    merged = OUT / sdk / f"{name}.framework"
    if merged.exists():
        shutil.rmtree(merged)
    shutil.copytree(frameworks[0], merged)
    if len(frameworks) > 1:
        run("xcrun", "lipo", "-create", *[f / name for f in frameworks], "-output", merged / name)
        for framework in frameworks[1:]:
            shutil.copytree(framework / "Modules", merged / "Modules", dirs_exist_ok=True)
    return merged


def main():
    if sys.platform != "darwin":
        raise SystemExit("Apple framework builds require macOS and Xcode.")
    if not BRIDGE.exists():
        raise SystemExit("Run python eng/prepare-native.py first")
    mantis = prepare_mantis()
    mantis_sources = mantis / "Sources/Mantis"
    slices = {"Mantis": [], "MauiImageCropperNative": []}
    for sdk, suffix, bridge_slice, arches in SLICES:
        sdk_path = Path(subprocess.check_output(["xcrun", "--sdk", sdk, "--show-sdk-path"], text=True).strip())
        builds = {name: [] for name in slices}
        for arch in arches:
            dependency = compile_framework("Mantis", sorted(mantis_sources.rglob("*.swift")), sdk, sdk_path, suffix, arch, [])
            # Use upstream's non-SPM resource lookup with its expected bundle identifier.
            resources = dependency / "MantisResources.bundle"
            shutil.copytree(mantis_sources / "Resources", resources, dirs_exist_ok=True)
            write_plist(resources / "Info.plist", {
                "CFBundleIdentifier": "com.echo.framework.Mantis.resources", "CFBundleName": "MantisResources",
                "CFBundlePackageType": "BNDL", "CFBundleDevelopmentRegion": "en",
                "CFBundleVersion": "1", "CFBundleShortVersionString": "3.1.0",
            })
            shutil.copy2(mantis_sources / "PrivacyInfo.xcprivacy", dependency)
            shutil.copy2(mantis / "LICENSE", dependency / "LICENSE.txt")
            builds["Mantis"].append(dependency)
            adapter = compile_framework("MauiImageCropperNative", sorted((ROOT / "native/apple/Sources").glob("*.swift")),
                sdk, sdk_path, suffix, arch, [(BRIDGE / bridge_slice, "NativeChannelsNative"), (dependency.parent, "Mantis")])
            builds["MauiImageCropperNative"].append(adapter)
        for name in slices:
            slices[name].append(merge(name, sdk, builds[name]))
    for name, frameworks in slices.items():
        destination = OUT / f"{name}.xcframework"
        if destination.exists():
            shutil.rmtree(destination)
        command = ["xcodebuild", "-create-xcframework"]
        for framework in frameworks:
            command += ["-framework", framework]
        run(*command, "-output", destination)


if __name__ == "__main__":
    main()
