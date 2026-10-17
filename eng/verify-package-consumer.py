"""Build a consumer from the final NuGet packages without source native imports."""
from pathlib import Path
import subprocess
import sys
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parents[1]
platform = sys.argv[1]
targets = {
    'windows': [('net10.0-windows10.0.19041.0', 'win-x64')],
    'android': [('net10.0-android', 'android-arm64')],
    'apple': [('net10.0-ios', 'iossimulator-arm64'), ('net10.0-maccatalyst', 'maccatalyst-arm64')],
}
# A config file preserves URL syntax across dotnet/MSBuild argument parsing.
config = ET.Element('configuration')
sources = ET.SubElement(config, 'packageSources')
ET.SubElement(sources, 'clear')
ET.SubElement(sources, 'add', key='local', value=str(root / 'artifacts/packages'))
ET.SubElement(sources, 'add', key='nuget.org', value='https://api.nuget.org/v3/index.json')
config_path = root / 'artifacts/consumer.NuGet.Config'
config_path.parent.mkdir(parents=True, exist_ok=True)
ET.ElementTree(config).write(config_path, encoding='utf-8', xml_declaration=True)

for tfm, rid in targets[platform]:
    args = ['dotnet', 'build', str(root / 'samples/ImageCropper.Sample'), '-c', 'Release', '-f', tfm,
            f'-p:TargetFrameworks={tfm}', f'-p:RuntimeIdentifier={rid}', '-p:UseLocalPackage=true',
            f'-p:RestoreConfigFile={config_path}',
            '--packages', str(root / 'artifacts/consumer-cache' / tfm)]
    subprocess.run(args, check=True)
