"""Extract compile-time bridges from the exact published NativeChannels packages."""
from pathlib import Path
from urllib.request import urlretrieve
from zipfile import ZipFile
from io import BytesIO

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "artifacts/dependencies"
VERSION = "0.1.0-preview.2"

def package(name):
    OUT.mkdir(parents=True, exist_ok=True)
    destination = OUT / f"{name}.{VERSION}.nupkg"
    urlretrieve(f"https://api.nuget.org/v3-flatcontainer/{name}/{VERSION}/{name}.{VERSION}.nupkg", destination)
    return ZipFile(destination)

def main():
    with package("nativechannels.native.android") as archive:
        aar = next(n for n in archive.namelist() if n.endswith(".aar"))
        with ZipFile(BytesIO(archive.read(aar))) as library:
            target = OUT / "android/bridge.jar"
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(library.read("classes.jar"))
    with package("nativechannels.native.apple") as archive:
        resource = next(n for n in archive.namelist() if n.endswith(".resources.zip"))
        with ZipFile(BytesIO(archive.read(resource))) as resources:
            resources.extractall(OUT / "apple")
    print(f"Prepared NativeChannels {VERSION} bridges in {OUT}")

if __name__ == "__main__":
    main()
