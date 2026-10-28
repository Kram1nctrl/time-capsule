# time-capsule

Snapshot your working session on Linux and bring it back later — from a small
desktop app or the command line.

A snapshot saves, for every interactive process (one with a terminal or a window):
the executable, its arguments, and its working directory — plus every X11 window's
title, virtual desktop and geometry. Restoring relaunches each app with the same
arguments and `cwd`, then puts the windows back where they were.

## Build

```bash
dotnet build            # builds core + CLI + GUI
```

This machine keeps the SDK in `~/dotnet10`, so either put it on your `PATH` or run
`dotnet` as `~/dotnet10/dotnet`. `run-gui.sh` handles this for the GUI. After
editing code, run `dotnet build` once; the launcher reuses the last build.

## GUI

```bash
./run-gui.sh
```

Or double-click `run-gui.sh` in your file manager. It finds the SDK itself and
installs the bundled JetBrains Mono font for your user on first run.

The window works in three steps:

1. Type a name (**Session name**) and press **Save this session**.
2. Pick a saved session on the left: you see each app with its icon, plus the
   windows that will be moved back. Click the small **✕** on an app or a window
   to leave it out of the restore; **Refresh** brings it back.
3. Press **Reopen selected**, then click once more to confirm. Progress and any
   problems appear in the log under the lists.

The **✕** on a saved session deletes that snapshot file; click it twice to
confirm.

The **moon / sun** button at the top right switches between the light and dark
palettes. The choice is not remembered after closing the window.

**Widget** (the switch under the saved sessions) opens a small always-on-top
window with the same saved sessions. Click a session in the widget to bring the
app back with that session selected. It works like a phone widget: closing the
main window keeps the widget running; switch it off, or click the widget's
**✕**, to turn it off (this also ends the app if the main window is closed).
Drag the widget by its "Saved sessions" title.

**Refresh** reloads the list after you add or delete snapshot files by hand.

## CLI

```bash
dotnet src/TimeCapsule.Cli/bin/Debug/net10.0/time-capsule.dll save embedded-lab
dotnet src/TimeCapsule.Cli/bin/Debug/net10.0/time-capsule.dll list
dotnet src/TimeCapsule.Cli/bin/Debug/net10.0/time-capsule.dll restore embedded-lab
dotnet src/TimeCapsule.Cli/bin/Debug/net10.0/time-capsule.dll restore embedded-lab --dry-run
```

`restore` accepts the full name, a path to a JSON file, or a unique prefix of a
saved name.

Snapshots are plain JSON in `~/.local/share/time-capsule/sessions/`
(`$XDG_DATA_HOME` is honoured), named `<tag>-<yyyy-MM-dd>.json`. A second snapshot
with the same tag on the same day gets a `-HHmmss` suffix. GUI and CLI share the
same folder.

## Layout

```
src/TimeCapsule.Core/   capture, manifest, restore, storage   (no UI)
src/TimeCapsule.Cli/    command line front end
src/TimeCapsule.Gui/    Avalonia desktop front end, bundled JetBrains Mono in fonts/
tests/smoke.sh          save -> manifest checks -> dry-run
run-gui.sh              launcher: builds if needed, installs the font, starts the GUI
```

## Manifest

```json
{
  "name": "embedded-lab-2026-10-28",
  "createdUtc": "2026-10-28T04:04:27.10+00:00",
  "host": "kram-X455LF",
  "processes": [
    { "pid": 4711, "exe": "/usr/bin/gdb", "args": ["build/kernel.elf"], "cwd": "/home/kram/kernel" }
  ],
  "windows": [
    { "pid": 1505, "title": "kernel.c — gedit", "class": "gedit.Gedit", "desktop": 0,
      "x": 0, "y": 72, "width": 1366, "height": 699 }
  ]
}
```

## How it works

- **Capture** walks `/proc`: `/proc/<pid>/exe` (resolved symlink), `cmdline`
  (NUL-separated argv), `cwd` (resolved symlink). Only your own processes with a
  controlling terminal or a window are kept, so relaunching does not duplicate
  the live desktop session (pipewire, xfce4-session, …).
- **Windows** come from `wmctrl -lpGx`. The tool is optional; without it snapshots
  simply contain no windows.
- **Restore** checks every `exe` still exists, launches with
  `ProcessStartInfo` + `ArgumentList` (no shell quoting bugs) and the saved
  `WorkingDirectory`, then waits 6s and arranges windows with `wmctrl -F -r`.

## Known limits

- Windows are matched by title, because ids change on relaunch: two windows with
  the same title get the same geometry.
- Sticky windows (panels, the desktop) are not captured, so they are never duplicated.
- Multi-process apps are only relaunched from their window-owning process; helper
  processes are skipped and reappear on their own.
- If an app's working directory no longer exists, it is launched from the
  current directory and the mismatch is reported.
- Window positions are applied as requested; some window managers offset them by
  the decoration size.

Run `tests/smoke.sh` to check the round trip.
