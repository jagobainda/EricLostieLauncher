# Code style

Part of the agent guidelines — see [AGENTS.md](../AGENTS.md) for the index and
the rules that always apply. Read this before writing or editing any `.cs` file.

[`.editorconfig`](../.editorconfig) is authoritative and `dotnet format`
enforces it. Listed below are the parts an agent is likely to get wrong; for
anything not listed, match the surrounding code.

## Formatting

- File-scoped namespaces (`namespace LostieLauncher.Services;`).
- Allman braces — opening brace on its own line, including before `else`,
  `catch` and `finally`.
- 4 spaces, CRLF, UTF-8, trailing whitespace trimmed, final newline.
- `using` directives are **not** System-first and **not** group-separated. An
  unused one breaks the build (`IDE0005` is an error).

## The unusual rules

These two differ from common C# defaults, and reformatting them "correctly"
will fail CI:

- **Explicit accessibility modifiers everywhere, including interface members:**

  ```csharp
  public interface IContentService
  {
      public Task<List<GameInfo>> GetGamesAsync();   // yes, `public` on an interface member
  }
  ```

- **No braces around single-statement bodies** (`csharp_prefer_braces = false`):

  ```csharp
  if (version is null) return "unknown";
  if (e.PropertyName == nameof(SettingsViewModel.Language)) LoadFaqs();
  ```

  Keep it on one line. Do not add braces back.

## Language usage

- `var` everywhere; no `this.` qualification.
- Expression-bodied members wherever they fit (`=> …`) — methods, constructors,
  properties, accessors, operators.
- Nullable reference types are on. Model the null (`is null`, `?.`, pattern
  matching) instead of reaching for `!` to silence the compiler.
- Modern C# matching the surrounding code: collection expressions (`[]`,
  `[.. items]`), target-typed `new`, `switch` expressions, `record` for data,
  primary constructors on services, `sealed` on new classes not designed for
  inheritance.
- Guard constructor arguments with `ArgumentNullException.ThrowIfNull(x)` when
  the constructor is written out explicitly.

## Comments and docs

- Comments explain **why**, not what. See the annotated
  `LostieLauncher.Tests.csproj` and the test helpers in
  `LostieLauncher.Tests/Helpers/` for the expected register: a comment earns its
  place by recording a constraint or a decision that the code cannot show.
- Put an XML doc on every new interface or seam stating what it exists to
  isolate, and on anything whose correct use is not obvious from the signature.
- Do not narrate obvious code, and do not leave commented-out code behind.
- Comments and XML docs are written in English.
