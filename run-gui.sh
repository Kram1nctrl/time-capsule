#!/usr/bin/env bash
# Starts the Time Capsule window. Double-click or run ./run-gui.sh.
# Finds the .NET SDK even when it is not on PATH.
set -euo pipefail
cd "$(dirname "$0")"

DOTNET=${DOTNET:-$HOME/dotnet10/dotnet}
if [ ! -x "$DOTNET" ]; then
    DOTNET=$(command -v dotnet || true)
fi
if [ -z "$DOTNET" ] || [ ! -x "$DOTNET" ]; then
    echo "No .NET SDK found. Install .NET 10 or run: DOTNET=/path/to/dotnet ./run-gui.sh"
    exit 1
fi

if [ ! -f src/TimeCapsule.Gui/bin/Debug/net10.0/time-capsule-gui.dll ]; then
    echo "First run, building..."
    "$DOTNET" build -v q --nologo >/dev/null
fi

# The UI uses JetBrains Mono; install the bundled copy for this user if the system lacks it.
if [ "$(fc-list : family 2>/dev/null | grep -ic 'jetbrains mono')" -eq 0 ]; then
    echo "Installing the bundled JetBrains Mono font for this user..."
    mkdir -p "$HOME/.local/share/fonts/jetbrains-mono"
    cp src/TimeCapsule.Gui/fonts/*.ttf "$HOME/.local/share/fonts/jetbrains-mono/"
    fc-cache -f >/dev/null 2>&1 || true
fi

exec "$DOTNET" src/TimeCapsule.Gui/bin/Debug/net10.0/time-capsule-gui.dll
