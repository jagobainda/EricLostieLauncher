# Architecture

Part of the agent guidelines — see [AGENTS.md](../AGENTS.md) for the index and
the rules that always apply. Read this before adding a service, a ViewModel, a
DI registration, or anything that needs a testability seam.

WPF (.NET 10, `net10.0-windows`) desktop launcher, **MVVM** with a single
centralized DI container.

## Layer rules

| Layer      | Lives in      | May depend on                        | Must never                                                   |
| ---------- | ------------- | ------------------------------------ | ------------------------------------------------------------ |
| Views      | `Views/`      | its own ViewModel (as `DataContext`) | contain business logic or call services directly             |
| ViewModels | `ViewModels/` | Services, Utils, Content, Models     | reference a concrete `Window` or dialog type                 |
| Services   | `Services/`   | Models, Utils                        | know about ViewModels or Views (see the seam exception below) |
| Utils      | `Utils/`      | Models, ideally nothing              | depend on Services or ViewModels                             |
| Models     | `Models/`     | nothing                              | carry behavior beyond simple derived members                 |
| Core       | `Core/`       | everything (composition root)        | be bypassed by an ad-hoc `new` of a service                  |
| Content    | `Content/`    | Models                               | be bypassed by hardcoded UI text                             |
| Converters | `Converters/` | Models                               | reach into services or ViewModels                            |

`Styles/`, `Themes/` and `Assets/` are resources only.

## Dependency injection

- Every service, ViewModel and `MainWindow` is registered in
  [`Core/DependencyInjection.cs`](../LostieLauncher/Core/DependencyInjection.cs),
  as a **singleton**, and resolved by **constructor injection**. Register new
  types there; never `new` a service at a call site.
- `App.Services` is the only service locator, and only at the composition root.
  `SettingsViewModel.Instance` is a legacy static accessor used by
  `WpfUpdateNotifier` — do not add new statics of that shape.
- `HttpClient` always comes from `IHttpClientFactory` through a **named client**
  (`"Content"`, `"SecurityFlag"`, `"Download"`), each with its own timeout.
  Never construct an `HttpClient` yourself.
- **No hardcoded URLs, paths or magic numbers inside a service.** Endpoints
  arrive as an options `record` in `Models/` (`ContentOptions`,
  `DownloadOptions`, `UpdateOptions`) wired up in `DependencyInjection`; the
  shared base URL is a `const` in `Core/Endpoints.cs`. That indirection is what
  makes the services testable — keep it.

## Seams: how untestable things become testable

Anything that touches the network, the Windows registry, the Velopack runtime or
a WPF dialog goes behind a **narrow interface plus a thin adapter** in
`Services/`, and the interesting logic moves into a pure static class in `Utils/`.

- `IUpdateGateway` / `VelopackUpdateGateway` — wraps Velopack's `UpdateManager`.
- `IUpdateNotifier` / `WpfUpdateNotifier` — wraps the dialogs. This is the one
  place a service may touch the UI, and it stays behind the interface precisely
  so the orchestration in `UpdateService` remains headless-testable.
- `*Policy` / `*Utils` types (`ShutdownWarningPolicy`, `StartupWindowPolicy`,
  `UnhandledExceptionPolicy`, `DownloadPathUtils`, `VersionUtils`,
  `SearchMatcher`, …) are **pure decision functions**: values in, value out, no
  I/O, no mutable statics. Prefer extracting a branchy decision into one of
  these and unit testing it directly over testing it through a ViewModel.

When you add something hard to test, add the seam. Do not write a test that
needs a desktop session.

## MVVM idioms (CommunityToolkit.Mvvm 8.4)

Match the existing code exactly:

```csharp
public partial class FaqsViewModel : ObservableObject
{
    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;   // partial property, NOT a private backing field

    [ObservableProperty]
    public partial bool HasNoResults { get; set; }

    public ObservableCollection<FaqItem> FilteredFaqs { get; } = [];

    partial void OnSearchTextChanged(string value) => ApplyFilter();  // generated change hook

    [RelayCommand]                                                    // generates ClearSearchCommand
    private void ClearSearch() => SearchText = string.Empty;
}
```

- Use the **partial property** form of `[ObservableProperty]`. The older
  `private string _searchText;` field form appears nowhere in this codebase.
- Declare derived state as a plain computed property and mark its source with
  `[NotifyPropertyChangedFor(nameof(Derived))]`, as `MainViewModel` does for
  `CurrentViewModel` → `IsHomeActive` and friends. Do not raise
  `OnPropertyChanged` by hand when the attribute can do it.
- Use `[RelayCommand]` / `[RelayCommand(CanExecute = ...)]`, and call
  `XCommand.NotifyCanExecuteChanged()` when the guard's inputs change.
- ViewModels talk to each other by subscribing to `PropertyChanged` or to an
  explicit event (`GamesViewModel.NavigateToLibraryRequested`), never by
  reaching into a View.
- View code-behind handles **purely visual** concerns only:
  `InitializeComponent()`, `DataContext = viewModel`, keyboard bindings, window
  chrome, `DependencyProperty` declarations on reusable components, and things
  like bringing an item into view. When a ViewModel needs such an effect it
  raises an event the View subscribes to (`LibraryViewModel.ScrollToGameRequested`)
  — and unsubscribes on `DataContextChanged`. Anything with a business decision
  in it belongs in the ViewModel or in a `Utils` policy.

## Threading and async

- Every `await` inside a service or util carries `.ConfigureAwait(false)`.
- Never `.Result`, `.Wait()`, or `Task.Run` to fake synchrony. Accept a
  `CancellationToken` when an operation can run long.
- `async void` is for event handlers only, and then the whole body is wrapped in
  `try { … } catch (Exception ex) { Logs.ErrorLogManager(ex); }`.
- To mutate bound state from a background continuation, use the guarded
  dispatcher pattern. The `null` check is exactly what keeps the ViewModel
  testable without a desktop session:

  ```csharp
  var app = Application.Current;
  if (app is null) return;
  app.Dispatcher.Invoke(() => { /* touch ObservableCollection / bound properties */ });
  ```

  `GlobalViewModel` shows the other accepted variant: `_dispatcher.CheckAccess()`
  and then `BeginInvoke`. Use `Interlocked` / `Volatile` for counters shared
  across threads.

## Logging and failure handling

- Log through `Logs` (`Utils/LogUtils.cs`): `Logs.DebugLogManager`,
  `Logs.InfoLogManager`, `Logs.ErrorLogManager(ex)` / `ErrorLogManager(string)`.
- Never swallow an exception silently. Log it, then **degrade gracefully**:
  return `[]`, keep the UI usable, surface a localized message when the user
  needs to know. The launcher must not crash because the content server is down.
- Log messages are English and state what happened, not that a method ran.
