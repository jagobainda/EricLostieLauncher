# Testing

Part of the agent guidelines — see [AGENTS.md](../AGENTS.md) for the index and
the rules that always apply. Read this before writing or editing any test.

`LostieLauncher.Tests/` mirrors the production folders (`Services/`,
`ViewModels/`, `Utils/`, `Models/`, `Helpers/`, `Integration/`), one
`<Type>Tests.cs` per production type. Stack: **xUnit v3**, **NSubstitute**,
**Shouldly**. `InternalsVisibleTo` exposes `internal` types to the test project,
so **never widen visibility to make something testable**.

## Hard rules

- **Headless.** No XAML, no `Window`, no real `Dispatcher`, and no
  `new Application()` of your own. CI is a headless Windows agent with no
  desktop session: a test that needs one hangs the pipeline.
- A ViewModel test that needs `Application.Current.Resources` uses
  `[Collection(WpfCollection.Name)]` and the shared `WpfApplicationFixture`.
- For async ViewModel commands: inherit `ViewModelTestBase`, trigger the
  command, `await PumpAsync()`, then assert.
- No real network, no filesystem access outside `TempDirectoryFixture`, no
  registry writes, no `Thread.Sleep`, and no dependency on wall-clock time or
  machine locale.
- Add or update tests for **every** behavior change, including the failure path
  you just fixed. A bug fix without a regression test is incomplete.

## Shape of a test class

```csharp
public class UpdateServiceTests
{
    private readonly IUpdateGateway _gateway = Substitute.For<IUpdateGateway>();
    private readonly IUpdateNotifier _notifier = Substitute.For<IUpdateNotifier>();

    private UpdateService CreateSut() => new(_gateway, _notifier);

    [Fact]
    public async Task CheckForUpdates_WhenUpToDateAndNotifyRequested_NotifiesUpToDate()
    {
        // Arrange — gateway reports no pending update.
        _gateway.CheckForUpdatesAsync().Returns((IUpdatePackage?)null);
        var sut = CreateSut();

        // Act — a user-initiated check asks to be told when already current.
        await sut.CheckForUpdatesAsync(notifyWhenUpToDate: true);

        // Assert
        _notifier.Received(1).NotifyUpToDate();
    }
}
```

- Names are `Method_Scenario_ExpectedResult`.
- Dependencies are `readonly` NSubstitute fields; the system under test comes
  from a `CreateSut()` factory so each test can arrange first.
- Keep the `// Arrange` / `// Act` / `// Assert` comments, and use them to say
  *why*, not to restate the code.
- Assert with Shouldly (`result.ShouldBe(…)`, `Should.Throw<T>(…)`); verify
  interactions with `Received(1)` / `DidNotReceive()`.
- Use `[Theory]` + `[InlineData]` for table-driven cases instead of copy-pasting
  a `[Fact]`, and group related cases behind a `// ---- section ----` comment as
  the existing files do.

## Reuse the helpers

Everything in `LostieLauncher.Tests/Helpers/` already exists — use it instead of
inventing new infrastructure:

| Helper                       | Use for                                                                  |
| ---------------------------- | ------------------------------------------------------------------------ |
| `FakeHttpMessageHandler`     | canned HTTP responses plus request inspection                            |
| `HttpClientFactoryStub`      | `IHttpClientFactory` keyed by the production client name                 |
| `TestServiceProviderBuilder` | a container mirroring production with doubles; override via `.With<T>(…)` |
| `TempDirectoryFixture`       | an isolated, self-deleting scratch directory                             |
| `PropertyChangedRecorder`    | asserting `PropertyChanged` / `[NotifyPropertyChangedFor]` chains        |
| `ViewModelTestBase`          | deterministic `SynchronizationContext` plus `await PumpAsync()`          |
| `WpfApplicationFixture`      | the single `Application` instance, via `[Collection(WpfCollection.Name)]` |

`GlobalUsings.cs` already imports `Xunit`, `NSubstitute`, `Shouldly`,
`LostieLauncher.Tests.Helpers` and the common BCL namespaces — do not re-add
those `using`s, they would break the build as `IDE0005`.

## Running them

```powershell
dotnet test LostieLauncher.slnx --no-build --configuration Release
dotnet test LostieLauncher.slnx --configuration Release --filter "FullyQualifiedName~UpdateServiceTests"
```

`global.json` opts the repo into the **Microsoft.Testing.Platform** runner
(xUnit v3 on .NET 10 has no VSTest bridge); `dotnet test` only discovers the
tests because of it. The test project also sets `TreatWarningsAsErrors`, with
only `xUnit1031` and `xUnit1051` suppressed — do not add suppressions to make a
test compile.
