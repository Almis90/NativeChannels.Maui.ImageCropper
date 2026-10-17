"""Build with Gradle 8.13 (JDK 17 and Android SDK 36 required)."""
from pathlib import Path
import shutil
import subprocess
import os

root = Path(__file__).resolve().parents[1]
if not (root / "artifacts/dependencies/android/bridge.jar").exists():
    raise SystemExit("Run python eng/prepare-native.py first")
gradle = shutil.which("gradle.bat" if os.name == "nt" else "gradle")
if not gradle: raise SystemExit("Install Gradle 8.13 and add it to PATH")
subprocess.run([gradle, "-p", str(root / "native/android"), "--no-daemon", "assembleRelease", "stageCanHub", "testReleaseUnitTest"], check=True)
target = root / "artifacts/native/android/imagecropper.aar"
target.parent.mkdir(parents=True, exist_ok=True)
shutil.copy2(root / "native/android/build/outputs/aar/imagecropper-release.aar", target)
shutil.copy2(root / "native/android/build/native-dependencies/android-image-cropper-4.7.0.aar", target.parent)
shutil.copy2(root / "native/android/CanHub-LICENSE.txt", target.parent)
print(target)
