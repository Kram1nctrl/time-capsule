using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Styling;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TimeCapsule;

namespace TimeCapsule.Gui;

public partial class MainWindow : Window
{
    bool _busy;
    string? _confirming; // session waiting for the second click on Reopen
    string? _deleting; // session waiting for the second click on its delete button
    Manifest? _manifest; // selected snapshot, with any apps the user removed
    string? _manifestPath;
    HistoryWidget? _widget;
    bool _wasShown; // the app window was on screen at least once

    public bool WidgetOn => WidgetSwitch.IsChecked == true;

    public MainWindow()
    {
        InitializeComponent();

        SaveButton.Click += async (_, _) => await SaveSession();
        RestoreButton.Click += async (_, _) => await ReopenSession();
        RefreshButton.Click += (_, _) => LoadSessions();
        WidgetSwitch.IsCheckedChanged += (_, _) => ToggleWidget();
        ThemeButton.Click += (_, _) => SetTheme(Application.Current?.RequestedThemeVariant != ThemeVariant.Dark);
        Opened += (_, _) => _wasShown = true;
        SessionList.SelectionChanged += (_, _) =>
        {
            _deleting = null;
            ResetConfirm();
            ShowDetails();
        };
        Closing += (_, e) =>
        {
            // Like a phone widget: closing the app window leaves the widget running.
            if (_widget is { IsVisible: true })
            {
                e.Cancel = true;
                Hide();
            }
        };

        LoadSessions();

        // The widget was on when the app last ran: put it back, like a phone widget.
        if (Autostart.Enabled) WidgetSwitch.IsChecked = true;
    }

    void SetTheme(bool dark)
    {
        if (Application.Current is { } app)
            app.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;

        MoonIcon.IsVisible = !dark;
        SunIcon.IsVisible = dark;
        ToolTip.SetTip(ThemeButton, dark ? "Switch to light mode" : "Switch to dark mode");
    }

    void ToggleWidget()
    {
        if (WidgetSwitch.IsChecked == true)
        {
            Autostart.Enable(); // bring the widget back after a reboot
            if (_widget is not { IsVisible: true }) ShowWidget();
            return;
        }

        Autostart.Disable();
        _widget?.Close();
    }

    void ShowWidget()
    {
        _widget = new HistoryWidget { Main = this };
        _widget.Closed += (_, _) =>
        {
            _widget = null;
            WidgetSwitch.IsChecked = false; // the widget's ✕ turns the switch off too
            if (!IsVisible && _wasShown) Close(); // the widget was the last window open
        };
        _widget.Reload();
        _widget.Show();
        // The window manager ignores these when they are set before the window is mapped.
        _widget.Topmost = true;
        _widget.ShowInTaskbar = false;
        _widget.Activate();
    }

    public void ShowSession(string path)
    {
        Show(); // no-op when it is already visible; brings it back after closing the app window
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;

        var match = (SessionList.ItemsSource as IEnumerable<SessionItem>)
            ?.FirstOrDefault(item => item.Path == path);
        if (match is not null) SessionList.SelectedItem = match;

        Activate();
    }

    void LoadSessions()
    {
        var selected = (SessionList.SelectedItem as SessionItem)?.Name;
        var items = Sessions.Load();
        SessionList.ItemsSource = items;
        SessionList.SelectedItem = items.FirstOrDefault(item => item.Name == selected) ?? items.FirstOrDefault();

        _widget?.Reload();
        ResetConfirm();
        ShowDetails();
    }

    void ShowDetails()
    {
        LogBox.IsVisible = false;

        if (SessionList.SelectedItem is not SessionItem item)
        {
            EmptyState.IsVisible = true;
            DetailsScroll.IsVisible = false;
            RestoreButton.IsEnabled = false;
            if (!_busy) StatusText.Text = "Type a name above, then press Save this session.";
            return;
        }

        Manifest manifest;
        try
        {
            manifest = Manifest.Load(item.Path);
        }
        catch (Exception ex)
        {
            _manifest = null;
            _manifestPath = null;
            EmptyState.IsVisible = false;
            DetailsScroll.IsVisible = true;
            SnapshotName.Text = item.Name;
            SnapshotInfo.Text = "This file could not be read: " + ex.Message;
            AppsHeader.IsVisible = false;
            AppList.IsVisible = false;
            NoAppsText.IsVisible = false;
            WindowsHeader.IsVisible = false;
            WindowList.IsVisible = false;
            NoWindowsText.IsVisible = false;
            RestoreButton.IsEnabled = false;
            return;
        }

        EmptyState.IsVisible = false;
        DetailsScroll.IsVisible = true;
        AppsHeader.IsVisible = true;
        AppList.IsVisible = true;
        WindowsHeader.IsVisible = true;
        WindowList.IsVisible = true;

        SnapshotName.Text = manifest.Name;
        SnapshotInfo.Text = $"Saved {Sessions.When(manifest.CreatedUtc.LocalDateTime)} on {manifest.Host}.";

        _manifest = manifest;
        _manifestPath = item.Path;
        ShowApps();
        ShowWindows();

        RestoreButton.IsEnabled = !_busy;
        if (!_busy)
            StatusText.Text = $"Press Reopen selected to bring '{manifest.Name}' back.";
    }

    void ShowApps()
    {
        if (_manifest is null) return;

        AppList.ItemsSource = _manifest.Processes.Select(process =>
        {
            var (name, icon) = AppIcons.Describe(process.Exe);
            return new AppRow(name, icon, process);
        }).ToList();
        AppsHeader.Text = $"Apps that will reopen ({_manifest.Processes.Count})";
        NoAppsText.IsVisible = _manifest.Processes.Count == 0;
    }

    void ShowWindows()
    {
        if (_manifest is null) return;

        WindowList.ItemsSource = _manifest.Windows.Select(window => new WindowRow(
            window.Title,
            $"desktop {window.Desktop + 1} · {window.Width}x{window.Height} at {window.X},{window.Y}",
            window)).ToList();
        WindowsHeader.Text = $"Windows that will be put back ({_manifest.Windows.Count})";
        NoWindowsText.IsVisible = _manifest.Windows.Count == 0;
    }

    void RemoveApp(object? sender, RoutedEventArgs e)
    {
        if (_manifest is null || (sender as Button)?.DataContext is not AppRow row) return;

        _manifest.Processes.Remove(row.Process);
        ShowApps();
        StatusText.Text = $"'{row.Name}' will not be reopened. Press Refresh to undo.";
    }

    void RemoveWindow(object? sender, RoutedEventArgs e)
    {
        if (_manifest is null || (sender as Button)?.DataContext is not WindowRow row) return;

        _manifest.Windows.Remove(row.Window);
        ShowWindows();
        StatusText.Text = $"'{row.Title}' will not be moved back. Press Refresh to undo.";
    }

    void DeleteSession(object? sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.DataContext is not SessionItem item) return;

        // Two clicks, so a stray click does not destroy a snapshot.
        if (_deleting != item.Path)
        {
            _deleting = item.Path;
            StatusText.Text = $"Press ✕ again on '{item.Name}' to delete it. This cannot be undone.";
            return;
        }

        _deleting = null;
        try
        {
            File.Delete(item.Path);
        }
        catch (Exception ex)
        {
            StatusText.Text = "Could not delete this session: " + ex.Message;
            return;
        }

        LoadSessions();
        StatusText.Text = $"Deleted '{item.Name}'.";
    }

    async Task SaveSession()
    {
        SetBusy(true, "Saving what is open right now...");
        try
        {
            var tag = Storage.CleanTag(NameBox.Text ?? "");
            var path = Storage.NewPath(tag);
            var manifest = await Task.Run(() => Inspector.Capture(Path.GetFileNameWithoutExtension(path)));
            manifest.Save(path);

            LoadSessions();
            SessionList.SelectedItem = (SessionList.ItemsSource as IEnumerable<SessionItem>)?
                .FirstOrDefault(item => item.Path == path);

            StatusText.Text = $"Saved {Sessions.Apps(manifest.Processes.Count)} and {Sessions.Windows(manifest.Windows.Count)}. "
                + "Pick it on the left and press Reopen selected to bring it back.";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Could not save the session: " + ex.Message;
        }
        finally
        {
            SetBusy(false);
        }
    }

    async Task ReopenSession()
    {
        if (SessionList.SelectedItem is not SessionItem item) return;

        Manifest manifest;
        try
        {
            manifest = _manifestPath == item.Path && _manifest is not null
                ? _manifest
                : Manifest.Load(item.Path);
        }
        catch (Exception ex)
        {
            StatusText.Text = "Could not read this snapshot: " + ex.Message;
            return;
        }

        if (manifest.Processes.Count == 0 && manifest.Windows.Count == 0)
        {
            StatusText.Text = "This snapshot is empty, there is nothing to reopen.";
            return;
        }

        // Two clicks, so a stray click does not launch a second copy of everything.
        if (_confirming != item.Name)
        {
            _confirming = item.Name;
            RestoreButton.Content = "Click again to confirm";
            StatusText.Text = manifest.Processes.Count > 0
                ? $"This will open {Sessions.Apps(manifest.Processes.Count)} next to the ones already running. "
                    + "Click again to confirm."
                : $"This will put back {Sessions.Windows(manifest.Windows.Count)} only. Click again to confirm.";
            return;
        }

        ResetConfirm();
        SetBusy(true, manifest.Windows.Count > 0
            ? $"Opening {Sessions.Apps(manifest.Processes.Count)}, then putting {Sessions.Windows(manifest.Windows.Count)} back..."
            : $"Opening {Sessions.Apps(manifest.Processes.Count)}...");

        var log = new List<string>();
        var failed = await Task.Run(() => TimeCapsule.Launcher.Restore(manifest, false, line =>
        {
            lock (log) log.Add(line);
        }));

        LogBox.Text = string.Join(Environment.NewLine, log);
        LogBox.IsVisible = true;
        StatusText.Text = failed == 0
            ? "Done, your session is back."
            : $"Done with {failed} problem(s). Details are shown above.";
        SetBusy(false);
    }

    void ResetConfirm()
    {
        _confirming = null;
        RestoreButton.Content = "Reopen selected";
    }

    void SetBusy(bool busy, string? status = null)
    {
        _busy = busy;
        SaveButton.IsEnabled = !busy;
        RefreshButton.IsEnabled = !busy;
        AppList.IsEnabled = !busy;
        WindowList.IsEnabled = !busy;
        SessionList.IsEnabled = !busy;
        RestoreButton.IsEnabled = !busy && SessionList.SelectedItem is SessionItem;
        if (busy) ResetConfirm();
        if (status != null) StatusText.Text = status;
    }
}
