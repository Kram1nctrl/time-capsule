#!/usr/bin/env bash
# Builds the release tarball from the last publish in build/time-capsule.
# Usage: packaging/make-tarball.sh 1.1.1
set -euo pipefail
cd "$(dirname "$0")/.."

VERSION=${1:?usage: packaging/make-tarball.sh <version>}
[ -x build/time-capsule/time-capsule-gui ] || {
    echo "Publish first: build/time-capsule/time-capsule-gui is missing." >&2
    exit 1
}

NAME=time-capsule-$VERSION-linux-x64
STAGE=$(mktemp -d)
mkdir -p "$STAGE/$NAME"
cp -a build/time-capsule "$STAGE/$NAME/time-capsule"
cp -a src/TimeCapsule.Gui/fonts "$STAGE/$NAME/fonts"
cp -a packaging install.sh README.md "$STAGE/$NAME/"
cp "packaging/Time Capsule" "$STAGE/$NAME/Time Capsule"
chmod +x "$STAGE/$NAME/Time Capsule" "$STAGE/$NAME/install.sh"
mkdir -p dist
tar -C "$STAGE" -czf "dist/$NAME.tar.gz" "$NAME"
rm -rf "$STAGE"
echo "dist/$NAME.tar.gz"
