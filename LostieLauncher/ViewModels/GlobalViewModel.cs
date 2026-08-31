using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Threading;

namespace LostieLauncher.ViewModels;

public partial class GlobalViewModel : ObservableObject
{
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;

    private int _activePlaySessions;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBusy))]
    public partial bool IsDownloading { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBusy))]
    public partial bool IsRefreshing { get; set; }

    public bool IsBusy => IsDownloading || IsRefreshing;

    internal int ActivePlaySessions => Volatile.Read(ref _activePlaySessions);

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
        if (_dispatcher.CheckAccess())
        {
            RaisePlaySessionsChangedCore();
            return;
        }

        _dispatcher.BeginInvoke(RaisePlaySessionsChangedCore);
    }

    private void RaisePlaySessionsChangedCore()
    {
        OnPropertyChanged(nameof(ActivePlaySessions));
        OnPropertyChanged(nameof(IsGameRunning));
    }
}
