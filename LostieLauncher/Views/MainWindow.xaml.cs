using LostieLauncher.ViewModels;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace LostieLauncher.Views;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        RegisterShortcuts(viewModel);
        Logs.DebugLogManager("MainWindow initialized and shortcuts registered.");
    }

    private void RegisterShortcuts(MainViewModel vm)
    {
        KeyboardShortcuts.BindCommand(this, Key.D1, ModifierKeys.Alt, vm.NavigateToHomeCommand);
        KeyboardShortcuts.BindCommand(this, Key.D2, ModifierKeys.Alt, vm.NavigateToGamesCommand);
        KeyboardShortcuts.BindCommand(this, Key.D3, ModifierKeys.Alt, vm.NavigateToLibraryCommand);
        KeyboardShortcuts.BindCommand(this, Key.D4, ModifierKeys.Alt, vm.NavigateToSettingsCommand);

        KeyboardShortcuts.BindCommand(this, Key.F5, ModifierKeys.None, vm.RefreshDataCommand);
        KeyboardShortcuts.BindCommand(this, Key.G, ModifierKeys.Control, vm.OpenSavedGamesCommand);
        KeyboardShortcuts.Bind(this, Key.Escape, ModifierKeys.None, MinimizeToTray);

        KeyboardShortcuts.Bind(this, Key.M, ModifierKeys.Control, () => WindowState = WindowState.Minimized);
        KeyboardShortcuts.Bind(this, Key.Q, ModifierKeys.Control | ModifierKeys.Shift, RequestShutdown);
    }

    private void MinimizeToTray() => Hide();

    private static void RequestShutdown()
    {
        if (Application.Current is App app) app.RequestShutdown();
        else Application.Current?.Shutdown();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        Logs.DebugLogManager("MainWindow closing — minimizing to tray.");
        e.Cancel = true;
        Hide();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1) DragMove();
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Hide();
}
