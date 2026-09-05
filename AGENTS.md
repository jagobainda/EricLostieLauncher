# Agent guidelines

Instructions for AI coding agents (Claude Code, Codex, Copilot, Cursor, …)
working in this repository. This file is the index: it holds the rules that
apply to **every** change, and routes you to a topic file for the rest.

Read the topic file **before** you write the corresponding code. The links are
plain Markdown on purpose — the detail is loaded on demand, not injected into
every session — so following them is your job, not the tool's.

| Read this before…                                     | File                                                                     |
| ----------------------------------------------------- | ------------------------------------------------------------------------ |
| adding a service, ViewModel, DI registration or seam  | [.agents/architecture.md](.agents/architecture.md)                       |
| writing or editing any `.cs` file                     | [.agents/code-style.md](.agents/code-style.md)                           |
| writing or editing any test                           | [.agents/testing.md](.agents/testing.md)                                 |
| adding user-visible text, a theme or a theme key      | [.agents/localization-and-themes.md](.agents/localization-and-themes.md) |
| branching, committing or opening a PR                 | [.agents/workflow.md](.agents/workflow.md)                               |

The human-facing docs are [CONTRIBUTING.md](CONTRIBUTING.md) (workflow and
acceptance criteria) and [README.md](README.md) (features, architecture,
configuration, endpoints). Nothing here may contradict them; if it does, they
win and the drift is a bug worth fixing.

## Non-negotiables

These apply to every change, with no topic file to look up:

1. Run the three CI gates locally before proposing a change. See [Commands](#commands).
2. Warnings are failures. Never leave an unused `using` (`IDE0005` is an error).
3. Tests must never load XAML, instantiate a `Window`, or use a real `Dispatcher`.
4. Never widen visibility to make code testable — `internal` is already visible to the tests.
5. A new user-visible string means **all 8 languages**. A new theme key means **all 10 themes**.
6. Branch off `development`; PRs target `development`, never `main`.
7. Everything you write is in **English**: code, comments, XML docs, commits, branch names, PR text.
8. Stay inside the scope of what was asked. You assist; the developer opening
   the PR is responsible for the result. See
   [Boundaries](.agents/workflow.md#boundaries).

## Project shape

WPF (.NET 10, `net10.0-windows`) desktop launcher for Windows, **MVVM** with a
single centralized DI container. Two projects: `LostieLauncher/` (app) and
`LostieLauncher.Tests/` (unit tests, mirroring the app's folders).

```
LostieLauncher/
├── Core/         # DI container + shared endpoint consts (composition root)
├── Models/       # data models, option records, enums
├── Services/     # service layer + the interfaces that isolate the untestable
├── ViewModels/   # CommunityToolkit.Mvvm ViewModels
├── Views/        # Windows, Partials/, Components/, Dialogs/
├── Converters/   # XAML value converters
├── Content/      # IStrings / IFaqs — localized text, 8 languages
├── Utils/        # pure helpers and *Policy decision functions, logging
├── Styles/ Themes/ Assets/   # resources only (10 themes)
```

Details and the dependency rules between these layers:
[.agents/architecture.md](.agents/architecture.md).

## Commands

Run from the repository root. These are exactly the three jobs in
[`.github/workflows/ci.yml`](.github/workflows/ci.yml), and a PR that fails any
of them will not be merged.

```powershell
# 1. Formatting — CI runs `--verify-no-changes`, so leave nothing pending
dotnet format LostieLauncher.slnx

# 2. Build and test in Release
dotnet restore LostieLauncher.slnx
dotnet build LostieLauncher.slnx --no-restore --configuration Release
dotnet test  LostieLauncher.slnx --no-build --configuration Release

# 3. Vulnerable dependencies — must report none
dotnet list LostieLauncher.slnx package --vulnerable --include-transitive
```

Useful while iterating:

```powershell
dotnet test LostieLauncher.slnx --configuration Release --filter "FullyQualifiedName~UpdateServiceTests"
```

Notes:

- `global.json` opts the repo into the **Microsoft.Testing.Platform** runner
  (xUnit v3 on .NET 10 has no VSTest bridge). `dotnet test` only discovers the
  tests because of it — do not remove it.
- The test project sets `TreatWarningsAsErrors`; the app project escalates
  `IDE0005` to an error via `.editorconfig`. Treat every warning as a build break.
- `scripts/build-release.ps1` and `releases/` are the maintainer's release path.
  **Never** bump `<Version>` / `<FileVersion>` / `<AssemblyVersion>` in
  `LostieLauncher.csproj`, nor the README version badge, as part of a feature or
  fix PR.

## Before you open a PR

- [ ] `dotnet format LostieLauncher.slnx` leaves nothing pending.
- [ ] Release build is clean — no warnings, no unused `using`s.
- [ ] All tests pass in Release, and new behavior is covered.
- [ ] No vulnerable packages reported.
- [ ] New strings exist in all 8 languages; new theme keys in all 10 themes.
- [ ] New services and ViewModels are registered in `Core/DependencyInjection.cs`.
- [ ] No hardcoded URLs, paths or user-visible literals.
- [ ] No version bump, and no unrelated file touched.
- [ ] Everything written in English.
