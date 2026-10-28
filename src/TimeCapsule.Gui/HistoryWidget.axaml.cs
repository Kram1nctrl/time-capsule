using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System.Linq;

namespace TimeCapsule.Gui;

/// <summary>Small always-on-top window that only lists the saved sessions.</summary>
public partial class HistoryWidget : Window
{
    public MainWindow? Main { get; set; }

    public HistoryWidget()
    {
        InitializeComponent();
        Reload();
    }

    public void Reload()
    {
        var selected = (SessionList.SelectedItem as SessionItem)?.Path;
        var items = Sessions.Load();
        SessionList.ItemsSource = items;
        SessionList.SelectedItem = items.FirstOrDefault(item => item.Path == selected);
    }

    void DragWidget(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) BeginMoveDrag(e);
    }

    void CloseWidget(object? sender, RoutedEventArgs e) => Close();

    void OpenSession(object? sender, TappedEventArgs e)
    {
        if ((e.Source as StyledElement)?.DataContext is not SessionItem item) return;
        Main?.ShowSession(item.Path);
    }
}
