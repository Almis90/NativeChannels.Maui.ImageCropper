"""Merge four same-commit SDK package slices; audit the all-platform package before use."""
from pathlib import Path
from zipfile import ZipFile, ZIP_DEFLATED
from copy import deepcopy
import argparse
import subprocess
import sys
import xml.etree.ElementTree as ET

NS = 'http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd'
ET.register_namespace('', NS)


def merge(inputs, destination):
    files, groups = {}, {}
    root = metadata = identity = content_types = None
    for package in inputs:
        with ZipFile(package) as archive:
            spec_name = next(n for n in archive.namelist() if n.endswith('.nuspec'))
            spec = ET.fromstring(archive.read(spec_name))
            meta = spec.find(f'{{{NS}}}metadata')
            key = tuple(meta.findtext(f'{{{NS}}}{field}') for field in ('id', 'version'))
            if identity is None:
                identity, root, metadata = key, spec, meta
                for section in ('dependencies', 'frameworkReferences', 'references'):
                    element = metadata.find(f'{{{NS}}}{section}')
                    if element is not None:
                        metadata.remove(element)
            if key != identity:
                raise ValueError(f'Package identity mismatch: {package}')
            # Re-read because the first metadata element was stripped above.
            meta = ET.fromstring(archive.read(spec_name)).find(f'{{{NS}}}metadata')
            repo = meta.find(f'{{{NS}}}repository')
            expected_repo = metadata.find(f'{{{NS}}}repository')
            if repo is None or expected_repo is None or repo.attrib != expected_repo.attrib:
                raise ValueError('Slices must have identical source repository/commit metadata')
            for section in ('dependencies', 'frameworkReferences', 'references'):
                element = meta.find(f'{{{NS}}}{section}')
                if element is None:
                    continue
                for group in element:
                    group_key = (section, group.attrib.get('targetFramework', ''))
                    if group_key in groups and ET.tostring(groups[group_key]) != ET.tostring(group):
                        raise ValueError(f'Conflicting framework group: {group_key}')
                    groups[group_key] = deepcopy(group)
            types = ET.fromstring(archive.read('[Content_Types].xml'))
            if content_types is None:
                content_types = types
            else:
                known = {(e.tag, tuple(sorted(e.attrib.items()))) for e in content_types}
                for e in types:
                    if (e.tag, tuple(sorted(e.attrib.items()))) not in known:
                        content_types.append(deepcopy(e))
            for name in archive.namelist():
                if name.endswith('/') or name.endswith('.nuspec') or name == '[Content_Types].xml':
                    continue
                if name == '.signature.p7s':
                    raise ValueError('Merge unsigned build slices only')
                if name.startswith('package/') or name == '_rels/.rels':
                    if package != inputs[0]:
                        continue
                data = archive.read(name)
                if name in files and files[name] != data:
                    if files[name].replace(b'\r\n', b'\n') != data.replace(b'\r\n', b'\n'):
                        raise ValueError(f'Conflicting asset: {name}')
                else:
                    files[name] = data
    for section in ('dependencies', 'frameworkReferences', 'references'):
        selected = [group for (kind, _), group in sorted(groups.items()) if kind == section]
        if selected:
            element = ET.SubElement(metadata, f'{{{NS}}}{section}')
            element.extend(selected)
    files[f'{identity[0]}.nuspec'] = ET.tostring(root, encoding='utf-8', xml_declaration=True)
    files['[Content_Types].xml'] = ET.tostring(content_types, encoding='utf-8', xml_declaration=True)
    destination.mkdir(parents=True, exist_ok=True)
    output = destination / f'{identity[0]}.{identity[1]}.nupkg'
    with ZipFile(output, 'w', ZIP_DEFLATED) as archive:
        for name, data in sorted(files.items()):
            archive.writestr(name, data)
    subprocess.run([sys.executable, str(Path(__file__).with_name('inspect-package.py')), str(output)], check=True)
    return output


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('input', type=Path)
    parser.add_argument('output', type=Path)
    args = parser.parse_args()
    packages = sorted(args.input.rglob('NativeChannels.Maui.ImageCropper.0.1.0-preview.nupkg'))
    if len(packages) != 4:
        raise SystemExit(f'Expected exactly four platform slices; found {len(packages)}')
    print(merge(packages, args.output))
