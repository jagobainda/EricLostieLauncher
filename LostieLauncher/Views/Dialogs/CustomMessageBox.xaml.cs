using MahApps.Metro.IconPacks;
using System.Windows;
using System.Windows.Input;

namespace LostieLauncher.Views.Dialogs;

public enum CustomMessageBoxButton
{
    OK,
    YesNo
}

public enum CustomMessageBoxIcon
{
    Update,
    Information,
    Error
}

public partial class CustomMessageBox : Window
{
    private CustomMessageBox(string title, string description, CustomMessageBoxButton button, CustomMessageBoxIcon icon)
    {
        InitializeComponent();
        TitleText.Text = title;
        DescriptionText.Text = description;

        TitleIcon.Kind = icon switch
        {
            CustomMessageBoxIcon.Information => PackIconMaterialKind.Information,
            CustomMessageBoxIcon.Error => PackIconMaterialKind.Alert,
            _ => PackIconMaterialKind.Update,
        };

        if (button == CustomMessageBoxButton.YesNo)
        {
            OkPanel.Visibility = Visibility.Collapsed;
            YesNoPanel.Visibility = Visibility.Visible;

            KeyboardShortcuts.RegisterDialog(
                this,
                onConfirm: () => DialogResult = true,
                onCancel: () => DialogResult = false,
                onYes: () => DialogResult = true,
                onNo: () => DialogResult = false);
        }
        else
        {
            KeyboardShortcuts.RegisterDialog(
                this,
                onConfirm: () => DialogResult = true,
                onCancel: Close);
        }
    }

    public static bool? Show(string title, string description, CustomMessageBoxButton button = CustomMessageBoxButton.OK, CustomMessageBoxIcon icon = CustomMessageBoxIcon.Update, Window? owner = null)
    {
        var dialog = new CustomMessageBox(title, description, button, icon);

        var candidateOwner = owner ?? Application.Current.MainWindow;

        if (candidateOwner is { IsLoaded: true, IsVisible: true })
        {
            dialog.Owner = candidateOwner;
        }
        else
        {
            dialog.Topmost = true;
            dialog.Loaded += (_, _) => dialog.Activate();
        }

        return dialog.ShowDialog();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1) DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void OkButton_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void YesButton_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void NoButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
