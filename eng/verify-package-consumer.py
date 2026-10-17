"""Build a consumer from the final NuGet packages without source native imports."""
from pathlib import Path
import subprocess
import sys

root = Path(__file__).resolve().parents[1]
platform = sys.argv[1]
targets = {
    'windows': [('net10.0-windows10.0.19041.0', 'win-x64')],
    'android': [('net10.0-android', 'android-arm64')],
    'apple': [('net10.0-ios', 'iossimulator-arm64'), ('net10.0-maccatalyst', 'maccatalyst-arm64')],
}
for tfm, rid in targets[platform]:
    args = ['dotnet', 'build', str(root / 'samples/ImageCropper.Sample'), '-c', 'Release', '-f', tfm,
            f'-p:TargetFrameworks={tfm}', f'-p:RuntimeIdentifier={rid}', '-p:UseLocalPackage=true',
            '--source', str(root / 'artifacts/packages'), '--source', 'https://api.nuget.org/v3/index.json',
            '--packages', str(root / 'artifacts/consumer-cache' / tfm)]
    subprocess.run(args, check=True)
