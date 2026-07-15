using LostieLauncher.Models;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace LostieLauncher.Views.Components;

public partial class GameCardComponent : UserControl
{
    public GameCardComponent()
    {
        InitializeComponent();
        UpdateDownloadArgs();
    }

    public static readonly DependencyProperty GameLogoProperty = DependencyProperty.Register(nameof(GameLogo), typeof(ImageSource), typeof(GameCardComponent));

    public ImageSource? GameLogo
    {
        get => (ImageSource?)GetValue(GameLogoProperty);
        set => SetValue(GameLogoProperty, value);
    }

    public static readonly DependencyProperty GameTitleProperty = DependencyProperty.Register(nameof(GameTitle), typeof(string), typeof(GameCardComponent), new PropertyMetadata(string.Empty));

    public string GameTitle
    {
        get => (string)GetValue(GameTitleProperty);
        set => SetValue(GameTitleProperty, value);
    }

    public static readonly DependencyProperty CardModeProperty = DependencyProperty.Register(nameof(CardMode), typeof(GameCardMode), typeof(GameCardComponent), new PropertyMetadata(GameCardMode.Library));

    public GameCardMode CardMode
    {
        get => (GameCardMode)GetValue(CardModeProperty);
        set => SetValue(CardModeProperty, value);
    }

    public static readonly DependencyProperty GameIdProperty = DependencyProperty.Register(nameof(GameId), typeof(string), typeof(GameCardComponent), new PropertyMetadata(string.Empty, OnDownloadArgsSourceChanged));

    public string GameId
    {
        get => (string)GetValue(GameIdProperty);
        set => SetValue(GameIdProperty, value);
    }

    public static readonly DependencyProperty GameSizeProperty = DependencyProperty.Register(nameof(GameSize), typeof(string), typeof(GameCardComponent), new PropertyMetadata(string.Empty));

    public string GameSize
    {
        get => (string)GetValue(GameSizeProperty);
        set => SetValue(GameSizeProperty, value);
    }

    public static readonly DependencyProperty LatestVersionProperty = DependencyProperty.Register(nameof(LatestVersion), typeof(string), typeof(GameCardComponent), new PropertyMetadata(string.Empty, OnDownloadArgsSourceChanged));

    public string LatestVersion
    {
        get => (string)GetValue(LatestVersionProperty);
        set => SetValue(LatestVersionProperty, value);
    }

    public static readonly DependencyProperty RutaRelativaProperty = DependencyProperty.Register(nameof(RutaRelativa), typeof(string), typeof(GameCardComponent), new PropertyMetadata(string.Empty, OnDownloadArgsSourceChanged));

    public string RutaRelativa
    {
        get => (string)GetValue(RutaRelativaProperty);
        set => SetValue(RutaRelativaProperty, value);
    }

    private static readonly DependencyPropertyKey DownloadArgsPropertyKey = DependencyProperty.RegisterReadOnly(nameof(DownloadArgs), typeof(GameDownloadArgs), typeof(GameCardComponent), new PropertyMetadata(null));

    public static readonly DependencyProperty DownloadArgsProperty = DownloadArgsPropertyKey.DependencyProperty;

    public GameDownloadArgs? DownloadArgs
    {
        get => (GameDownloadArgs?)GetValue(DownloadArgsProperty);
        private set => SetValue(DownloadArgsPropertyKey, value);
    }

    private static void OnDownloadArgsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((GameCardComponent)d).UpdateDownloadArgs();

    private void UpdateDownloadArgs() => DownloadArgs = new GameDownloadArgs(GameId, LatestVersion, RutaRelativa);

    public static readonly DependencyProperty DownloadStatusProperty = DependencyProperty.Register(nameof(DownloadStatus), typeof(GameDownloadStatus), typeof(GameCardComponent), new PropertyMetadata(GameDownloadStatus.Available));

    public GameDownloadStatus DownloadStatus
    {
        get => (GameDownloadStatus)GetValue(DownloadStatusProperty);
        set => SetValue(DownloadStatusProperty, value);
    }

    public static readonly DependencyProperty DownloadProgressProperty = DependencyProperty.Register(nameof(DownloadProgress), typeof(double), typeof(GameCardComponent), new PropertyMetadata(0.0));

    public double DownloadProgress
    {
        get => (double)GetValue(DownloadProgressProperty);
        set => SetValue(DownloadProgressProperty, value);
    }

    public static readonly DependencyProperty DownloadSpeedTextProperty = DependencyProperty.Register(nameof(DownloadSpeedText), typeof(string), typeof(GameCardComponent), new PropertyMetadata(string.Empty));

    public string DownloadSpeedText
    {
        get => (string)GetValue(DownloadSpeedTextProperty);
        set => SetValue(DownloadSpeedTextProperty, value);
    }

    public static readonly DependencyProperty DownloadRemainingTextProperty = DependencyProperty.Register(nameof(DownloadRemainingText), typeof(string), typeof(GameCardComponent), new PropertyMetadata(string.Empty));

    public string DownloadRemainingText
    {
        get => (string)GetValue(DownloadRemainingTextProperty);
        set => SetValue(DownloadRemainingTextProperty, value);
    }

    public static readonly DependencyProperty DownloadCommandProperty = DependencyProperty.Register(nameof(DownloadCommand), typeof(ICommand), typeof(GameCardComponent));

    public ICommand? DownloadCommand
    {
        get => (ICommand?)GetValue(DownloadCommandProperty);
        set => SetValue(DownloadCommandProperty, value);
    }

    public static readonly DependencyProperty PauseDownloadCommandProperty = DependencyProperty.Register(nameof(PauseDownloadCommand), typeof(ICommand), typeof(GameCardComponent));

    public ICommand? PauseDownloadCommand
    {
        get => (ICommand?)GetValue(PauseDownloadCommandProperty);
        set => SetValue(PauseDownloadCommandProperty, value);
    }

    public static readonly DependencyProperty CancelDownloadCommandProperty = DependencyProperty.Register(nameof(CancelDownloadCommand), typeof(ICommand), typeof(GameCardComponent));

    public ICommand? CancelDownloadCommand
    {
        get => (ICommand?)GetValue(CancelDownloadCommandProperty);
        set => SetValue(CancelDownloadCommandProperty, value);
    }

    public static readonly DependencyProperty InstalledVersionProperty = DependencyProperty.Register(nameof(InstalledVersion), typeof(string), typeof(GameCardComponent), new PropertyMetadata(string.Empty));

    public string InstalledVersion
    {
        get => (string)GetValue(InstalledVersionProperty);
        set => SetValue(InstalledVersionProperty, value);
    }

    public static readonly DependencyProperty UpdateVersionProperty = DependencyProperty.Register(nameof(UpdateVersion), typeof(string), typeof(GameCardComponent), new PropertyMetadata(string.Empty));

    public string UpdateVersion
    {
        get => (string)GetValue(UpdateVersionProperty);
        set => SetValue(UpdateVersionProperty, value);
    }

    public static readonly DependencyProperty HasUpdateProperty = DependencyProperty.Register(nameof(HasUpdate), typeof(bool), typeof(GameCardComponent), new PropertyMetadata(false));

    public bool HasUpdate
    {
        get => (bool)GetValue(HasUpdateProperty);
        set => SetValue(HasUpdateProperty, value);
    }

    public static readonly DependencyProperty IsUpdatingProperty = DependencyProperty.Register(nameof(IsUpdating), typeof(bool), typeof(GameCardComponent), new PropertyMetadata(false));

    public bool IsUpdating
    {
        get => (bool)GetValue(IsUpdatingProperty);
        set => SetValue(IsUpdatingProperty, value);
    }

    public static readonly DependencyProperty IsUninstallingProperty = DependencyProperty.Register(nameof(IsUninstalling), typeof(bool), typeof(GameCardComponent), new PropertyMetadata(false));

    public bool IsUninstalling
    {
        get => (bool)GetValue(IsUninstallingProperty);
        set => SetValue(IsUninstallingProperty, value);
    }

    public static readonly DependencyProperty PlayCommandProperty = DependencyProperty.Register(nameof(PlayCommand), typeof(ICommand), typeof(GameCardComponent));

    public ICommand? PlayCommand
    {
        get => (ICommand?)GetValue(PlayCommandProperty);
        set => SetValue(PlayCommandProperty, value);
    }

    public static readonly DependencyProperty UpdateCommandProperty = DependencyProperty.Register(nameof(UpdateCommand), typeof(ICommand), typeof(GameCardComponent));

    public ICommand? UpdateCommand
    {
        get => (ICommand?)GetValue(UpdateCommandProperty);
        set => SetValue(UpdateCommandProperty, value);
    }

    public static readonly DependencyProperty LibraryUpdateCommandProperty = DependencyProperty.Register(nameof(LibraryUpdateCommand), typeof(ICommand), typeof(GameCardComponent));

    public ICommand? LibraryUpdateCommand
    {
        get => (ICommand?)GetValue(LibraryUpdateCommandProperty);
        set => SetValue(LibraryUpdateCommandProperty, value);
    }

    public static readonly DependencyProperty OpenFolderCommandProperty = DependencyProperty.Register(nameof(OpenFolderCommand), typeof(ICommand), typeof(GameCardComponent));

    public ICommand? OpenFolderCommand
    {
        get => (ICommand?)GetValue(OpenFolderCommandProperty);
        set => SetValue(OpenFolderCommandProperty, value);
    }

    public static readonly DependencyProperty OpenHelpFolderCommandProperty = DependencyProperty.Register(nameof(OpenHelpFolderCommand), typeof(ICommand), typeof(GameCardComponent));

    public ICommand? OpenHelpFolderCommand
    {
        get => (ICommand?)GetValue(OpenHelpFolderCommandProperty);
        set => SetValue(OpenHelpFolderCommandProperty, value);
    }

    public static readonly DependencyProperty HasHelpFolderProperty = DependencyProperty.Register(nameof(HasHelpFolder), typeof(bool), typeof(GameCardComponent), new PropertyMetadata(false));

    public bool HasHelpFolder
    {
        get => (bool)GetValue(HasHelpFolderProperty);
        set => SetValue(HasHelpFolderProperty, value);
    }

    public static readonly DependencyProperty UninstallCommandProperty = DependencyProperty.Register(nameof(UninstallCommand), typeof(ICommand), typeof(GameCardComponent));

    public ICommand? UninstallCommand
    {
        get => (ICommand?)GetValue(UninstallCommandProperty);
        set => SetValue(UninstallCommandProperty, value);
    }

    public static readonly DependencyProperty PlaytimeTextProperty = DependencyProperty.Register(nameof(PlaytimeText), typeof(string), typeof(GameCardComponent), new PropertyMetadata(string.Empty));

    public string PlaytimeText
    {
        get => (string)GetValue(PlaytimeTextProperty);
        set => SetValue(PlaytimeTextProperty, value);
    }

    public static readonly DependencyProperty TipoProperty = DependencyProperty.Register(nameof(Tipo), typeof(string), typeof(GameCardComponent), new PropertyMetadata(string.Empty));

    public string Tipo
    {
        get => (string)GetValue(TipoProperty);
        set => SetValue(TipoProperty, value);
    }

    public static readonly DependencyProperty IsSpecialVersionProperty = DependencyProperty.Register(nameof(IsSpecialVersion), typeof(bool), typeof(GameCardComponent), new PropertyMetadata(false));

    public bool IsSpecialVersion
    {
        get => (bool)GetValue(IsSpecialVersionProperty);
        set => SetValue(IsSpecialVersionProperty, value);
    }

    public static readonly DependencyProperty SwitchToSpecialVersionCommandProperty = DependencyProperty.Register(nameof(SwitchToSpecialVersionCommand), typeof(ICommand), typeof(GameCardComponent));

    public ICommand? SwitchToSpecialVersionCommand
    {
        get => (ICommand?)GetValue(SwitchToSpecialVersionCommandProperty);
        set => SetValue(SwitchToSpecialVersionCommandProperty, value);
    }
}
