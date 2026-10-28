#!/usr/bin/env bash
# Smoke test: capture a snapshot, check the manifest, dry-run it, clean up.
# Requires an X11 session (wmctrl) for the window part; process capture works anywhere.
set -euo pipefail
cd "$(dirname "$0")/.."

DOTNET=${DOTNET:-$HOME/dotnet10/dotnet}
DATA=${XDG_DATA_HOME:-$HOME/.local/share}/time-capsule/sessions
TAG=smoke-$$

"$DOTNET" build -v q --nologo >/dev/null
run() { "$DOTNET" src/TimeCapsule.Cli/bin/Debug/net10.0/time-capsule.dll "$@"; }

trap 'rm -f "$DATA/$TAG"*.json' EXIT

run save "$TAG"
manifest=$(ls "$DATA/$TAG"*.json)
test -f "$manifest"

grep -q '"processes": \[' "$manifest"
grep -q '"windows": \[' "$manifest"
grep -q '"exe": "/' "$manifest"
grep -q '"createdUtc"' "$manifest"

run list | grep -q "$TAG"

# Short tag resolves to the dated file, and dry-run launches nothing.
run restore "$TAG" --dry-run | grep -q 'would run'

echo "smoke test passed"
