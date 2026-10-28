#!/usr/bin/env bash
# Installs Time Capsule for the current user, no root needed.
# Uses a prebuilt folder when present (release tarball), otherwise publishes
# a self-contained build with the .NET SDK.
set -euo pipefail
cd "$(dirname "$0")"

PREFIX=${PREFIX:-$HOME/.local}
APP=$PREFIX/opt/time-capsule
LINKS=$PREFIX/bin/time-capsule

if [ -x time-capsule/time-capsule-gui ]; then
    PUB=$PWD/time-capsule
elif [ -x build/time-capsule/time-capsule-gui ]; then
    PUB=$PWD/build/time-capsule
else
    DOTNET=${DOTNET:-$(command -v dotnet || true)}
    if [ ! -x "$DOTNET" ]; then
        [ -x "$HOME/dotnet10/dotnet" ] && DOTNET=$HOME/dotnet10/dotnet
    fi
    if [ -z "$DOTNET" ] || [ ! -x "$DOTNET" ]; then
        echo "The .NET SDK is needed to build. Install .NET 10 or use the release tarball." >&2
        exit 1
    fi
    echo "Building..."
    "$DOTNET" publish src/TimeCapsule.Gui/TimeCapsule.Gui.csproj -c Release -r linux-x64 \
        --self-contained true -o build/time-capsule -v q --nologo
    PUB=$PWD/build/time-capsule
fi

echo "Installing to $APP..."
rm -rf "$APP"
mkdir -p "$APP" "$PREFIX/bin" "$PREFIX/share/applications" \
         "$PREFIX/share/icons/hicolor/256x256/apps"

cp -a "$PUB/." "$APP/"

cat > "$LINKS" <<EOF
#!/usr/bin/env bash
exec "$APP/time-capsule-gui" "\$@"
EOF
chmod +x "$LINKS"

install -m 644 packaging/time-capsule.png "$PREFIX/share/icons/hicolor/256x256/apps/time-capsule.png"
sed "s|@EXEC@|$LINKS|" packaging/time-capsule.desktop > "$PREFIX/share/applications/time-capsule.desktop"
update-desktop-database "$PREFIX/share/applications" >/dev/null 2>&1 || true

if ! fc-list : family 2>/dev/null | grep -iq 'jetbrains mono'; then
    FONTS=$PWD/src/TimeCapsule.Gui/fonts
    [ -d "$FONTS" ] || FONTS=$PWD/fonts
    if [ -d "$FONTS" ]; then
        echo "Installing the bundled JetBrains Mono font..."
        mkdir -p "$PREFIX/share/fonts/jetbrains-mono"
        cp "$FONTS"/*.ttf "$PREFIX/share/fonts/jetbrains-mono/"
        fc-cache -f >/dev/null 2>&1 || true
    fi
fi

echo "Done. Start it from the app menu (Time Capsule) or run: $LINKS"
