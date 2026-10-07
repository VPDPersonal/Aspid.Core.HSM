#!/usr/bin/env bash
# Creates a throwaway Unity project that compiles the package and its Game Loop sample, for checking the package on
# the minimum Unity that package.json promises (the dev project itself runs a newer one). CI (tests.yml) and local
# runs use it.
#
# Usage: scripts/make-unity-test-project.sh <project-dir> <unity-version>
# Prints the Unity version to stdout.
#
# Local run:
#   v=$(scripts/make-unity-test-project.sh /tmp/hsm-ci 6000.0.64f1)
#   "/Applications/Unity/Hub/Editor/$v/Unity.app/Contents/MacOS/Unity" -batchmode -nographics -projectPath /tmp/hsm-ci \
#     -runTests -testPlatform EditMode -testResults /tmp/hsm-ci/results.xml
set -euo pipefail

if [ $# -ne 2 ]; then
  echo "usage: $0 <project-dir> <unity-version>" >&2
  exit 2
fi

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PACKAGE="$ROOT/Aspid.Core.HSM/Assets/Plugins/Aspid/Core/HSM"
PROJECT="$1"
VERSION="$2"

if [ -e "$PROJECT" ] && [ -n "$(ls -A "$PROJECT")" ]; then
  echo "error: $PROJECT exists and is not empty" >&2
  exit 1
fi

mkdir -p "$PROJECT/Assets" "$PROJECT/Packages" "$PROJECT/ProjectSettings"
PROJECT="$(cd "$PROJECT" && pwd)"

# Relative, so the path still resolves when the workspace is mounted elsewhere (the GameCI container).
PACKAGE_REF="$(python3 -c 'import os, sys; print(os.path.relpath(sys.argv[1], sys.argv[2]))' "$PACKAGE" "$PROJECT/Packages")"

# UniTask is not in the Unity registry, so package.json cannot declare it: users add it from git, and so does this
# project. The Input System is what the Game Loop sample uses.
cat > "$PROJECT/Packages/manifest.json" <<JSON
{
  "dependencies": {
    "com.aspid.core.hsm": "file:$PACKAGE_REF",
    "com.cysharp.unitask": "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#2.5.10",
    "com.unity.inputsystem": "1.14.2",
    "com.unity.test-framework": "1.6.0",
    "com.unity.modules.imgui": "1.0.0",
    "com.unity.modules.jsonserialize": "1.0.0",
    "com.unity.modules.ui": "1.0.0",
    "com.unity.modules.uielements": "1.0.0"
  },
  "testables": [
    "com.aspid.core.hsm"
  ]
}
JSON

printf 'm_EditorVersion: %s\n' "$VERSION" > "$PROJECT/ProjectSettings/ProjectVersion.txt"

# The sample is imported the way Package Manager imports it: copied into Assets/.
mkdir -p "$PROJECT/Assets/Samples/GameLoop"
cp -R "$PACKAGE/Samples~/GameLoop/." "$PROJECT/Assets/Samples/GameLoop/"

echo "$VERSION"
