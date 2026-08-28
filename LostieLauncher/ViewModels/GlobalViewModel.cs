using CommunityToolkit.Mvvm.ComponentModel;

namespace LostieLauncher.ViewModels;

public partial class GlobalViewModel : ObservableObject
{
    private int _activePlaySessions;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBusy))]
    public partial bool IsDownloading { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBusy))]
    public partial bool IsRefreshing { get; set; }

    public bool IsBusy => IsDownloading || IsRefreshing;

    public int ActivePlaySessions => Volatile.Read(ref _activePlaySessions);

    public bool IsGameRunning => ActivePlaySessions > 0;

    public void BeginPlaySession()
    {
        Interlocked.Increment(ref _activePlaySessions);
        RaisePlaySessionsChanged();
    }

    public void EndPlaySession()
    {
        Interlocked.Decrement(ref _activePlaySessions);
        RaisePlaySessionsChanged();
    }

    private void RaisePlaySessionsChanged()
    {
        OnPropertyChanged(nameof(ActivePlaySessions));
        OnPropertyChanged(nameof(IsGameRunning));
    }
}
