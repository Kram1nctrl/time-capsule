#!/usr/bin/env bash
# Firefox tab support: decode a canned session store, and reopen a snapshot's
# browser windows through a fake browser so no real Firefox is touched.
set -euo pipefail
cd "$(dirname "$0")/.."

DOTNET=${DOTNET:-$HOME/dotnet10/dotnet}
"$DOTNET" build -v q --nologo >/dev/null
run() { "$DOTNET" src/TimeCapsule.Cli/bin/Debug/net10.0/time-capsule.dll "$@"; }

TMP=$(mktemp -d)
trap 'rm -rf "$TMP"' EXIT

# 1. Decoding: a literals-only mozLz4 session store in a throwaway profile.
PROFILE=$TMP/config/mozilla/firefox/test.default/sessionstore-backups
mkdir -p "$PROFILE"
python3 - "$PROFILE/recovery.jsonlz4" <<'PY'
import json, struct, sys

payload = json.dumps({
    "windows": [{
        "screenX": 10, "screenY": 20, "width": 800, "height": 600, "selected": 1,
        "tabs": [
            {"entries": [{"url": "https://example.com/one", "title": "One"}]},
            {"entries": [{"url": "https://example.com/two", "title": "Two"}]},
        ],
    }]
}).encode()

# LZ4 block with one literals-only sequence, which is what the tail of a
# Firefox session store looks like.
size = len(payload)
if size < 15:
    block = bytes([size << 4]) + payload
else:
    rest, extra = size - 15, bytearray()
    while rest >= 255:
        extra.append(255)
        rest -= 255
    extra.append(rest)
    block = b"\xf0" + bytes(extra) + payload

with open(sys.argv[1], "wb") as f:
    f.write(b"mozLz40\0" + struct.pack("<I", size) + block)
PY

tabs=$(XDG_CONFIG_HOME=$TMP/config run tabs)
echo "$tabs" | grep -q '800x600 at 10,20'
echo "$tabs" | grep -q 'https://example.com/one'
echo "$tabs" | grep -q 'https://example.com/two'
echo "$tabs" | grep -q '\* Two'

# 2. Reopening: each saved browser window becomes one --new-window command.
cat > "$TMP/fakebrowser" <<'SH'
#!/bin/sh
printf '%s\n' "$@" >> "$LOG"
SH
chmod +x "$TMP/fakebrowser"

cat > "$TMP/snapshot.json" <<JSON
{
  "name": "tabs-test",
  "createdUtc": "2026-01-01T00:00:00+00:00",
  "host": "test",
  "processes": [
    { "pid": 999999, "exe": "$TMP/fakebrowser", "args": [], "cwd": "$TMP" }
  ],
  "windows": [
    {
      "pid": 999999, "title": "Two — Mozilla Firefox", "class": "FakeBrowser.firefox",
      "desktop": 0, "x": 10, "y": 20, "width": 800, "height": 600,
      "tabs": [
        { "title": "One", "url": "https://example.com/one" },
        { "title": "Two", "url": "https://example.com/two", "active": true }
      ]
    },
    {
      "pid": 999999, "title": "Three — Mozilla Firefox", "class": "FakeBrowser.firefox",
      "desktop": 0, "x": 10, "y": 20, "width": 800, "height": 600,
      "tabs": [
        { "title": "Three", "url": "https://example.com/three" }
      ]
    }
  ]
}
JSON

set +e
out=$(LOG=$TMP/args run restore "$TMP/snapshot.json")
set -e

echo "$out" | grep -q '3 tabs in 2 windows'
test "$(grep -c -- '--new-window' "$TMP/args")" -eq 2
grep -q 'https://example.com/one' "$TMP/args"
grep -q 'https://example.com/three' "$TMP/args"
# The tab that was in front is passed last, so Firefox selects it.
test "$(grep -n 'https://example.com/two' "$TMP/args" | cut -d: -f1)" \
   -gt "$(grep -n 'https://example.com/one' "$TMP/args" | cut -d: -f1)"

echo "tabs test passed"
