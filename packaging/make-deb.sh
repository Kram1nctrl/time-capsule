#!/usr/bin/env bash
# Builds the .deb from the last publish in build/time-capsule.
# Usage: packaging/make-deb.sh 1.1.1
set -euo pipefail
cd "$(dirname "$0")/.."

VERSION=${1:?usage: packaging/make-deb.sh <version>}
APP=build/time-capsule
[ -x "$APP/time-capsule-gui" ] || {
    echo "Publish first: $APP/time-capsule-gui is missing." >&2
    exit 1
}

STAGE=$(mktemp -d)
trap 'rm -rf "$STAGE"' EXIT
mkdir -p "$STAGE/DEBIAN" "$STAGE/opt/time-capsule" \
    "$STAGE/usr/bin" "$STAGE/usr/share/applications" \
    "$STAGE/usr/share/icons/hicolor/256x256/apps" \
    "$STAGE/usr/share/fonts/truetype/jetbrains-mono" \
    "$STAGE/usr/share/doc/time-capsule"

cp -a "$APP/." "$STAGE/opt/time-capsule/"
printf '#!/bin/sh\nexec /opt/time-capsule/time-capsule-gui "$@"\n' > "$STAGE/usr/bin/time-capsule"
chmod 755 "$STAGE/usr/bin/time-capsule"
sed 's|@EXEC@|/usr/bin/time-capsule|' packaging/time-capsule.desktop > "$STAGE/usr/share/applications/time-capsule.desktop"
install -m 644 packaging/time-capsule.png "$STAGE/usr/share/icons/hicolor/256x256/apps/time-capsule.png"
install -m 644 src/TimeCapsule.Gui/fonts/*.ttf src/TimeCapsule.Gui/fonts/OFL.txt "$STAGE/usr/share/fonts/truetype/jetbrains-mono/"
install -m 644 README.md "$STAGE/usr/share/doc/time-capsule/README.md"

cat > "$STAGE/DEBIAN/postinst" <<'EOF'
#!/bin/sh
set -e
command -v update-desktop-database >/dev/null && update-desktop-database -q /usr/share/applications || true
command -v gtk-update-icon-cache >/dev/null && gtk-update-icon-cache -f -t /usr/share/icons/hicolor >/dev/null 2>&1 || true
exit 0
EOF
cat > "$STAGE/DEBIAN/postrm" <<'EOF'
#!/bin/sh
set -e
command -v update-desktop-database >/dev/null && update-desktop-database -q /usr/share/applications || true
command -v gtk-update-icon-cache >/dev/null && gtk-update-icon-cache -f -t /usr/share/icons/hicolor >/dev/null 2>&1 || true
exit 0
EOF
chmod 755 "$STAGE/DEBIAN/postinst" "$STAGE/DEBIAN/postrm"

cat > "$STAGE/DEBIAN/control" <<EOF
Package: time-capsule
Version: $VERSION
Section: utils
Priority: optional
Architecture: amd64
Depends: libx11-6, libxrandr2, libxi6, libxcursor1, libxext6, libxrender1, libsm6, libice6, libfontconfig1, libfreetype6
Installed-Size: $(du -sk "$STAGE" | cut -f1)
Maintainer: Kram1nctrl <kram1nctrl@users.noreply.github.com>
Description: Save your open apps and windows, and reopen them later
 Time Capsule captures the programs and window positions of your desktop
 session and puts them back when you need them.
EOF

mkdir -p dist
dpkg-deb --build --root-owner-group "$STAGE" "dist/time-capsule_${VERSION}_amd64.deb"
echo "dist/time-capsule_${VERSION}_amd64.deb"
