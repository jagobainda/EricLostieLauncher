# Localization and themes

Part of the agent guidelines — see [AGENTS.md](../AGENTS.md) for the index and
the rules that always apply. Read this before adding any user-visible text, a
theme, or a theme resource key.

Both areas work the same way: a fixed set of parallel implementations that must
stay complete. Half-done work here is the most common reason a PR gets sent back.

## Localization — 8 languages, no exceptions

[`Content/Strings.cs`](../LostieLauncher/Content/Strings.cs) holds `IStrings`
plus one implementation per language: `Esp`, `Eng`, `Cat`, `Eus`, `Gal`, `Por`,
`Val`, `Fra`. [`Content/Faqs.cs`](../LostieLauncher/Content/Faqs.cs) does the
same for `IFaqs`, with `Faqs.For(language)` selecting the set.

- Adding user-visible text means adding the member to the interface **and to all
  eight classes**. A missing implementation is a compile error, so half-done
  work fails the build — but text left in English across every language is a
  review rejection, not a build one. Translate it.
- Views and ViewModels read text from `SettingsViewModel.Strings`, which swaps
  when `AppLanguage` changes. **Never hardcode a user-visible literal** in XAML
  or in a ViewModel.
- Use `string.Format(strings.SomeMessage, arg)` for placeholders, and keep the
  `{0}` ordering and count identical across all eight languages.
- Anything reacting to a language switch subscribes to `PropertyChanged` for
  `nameof(SettingsViewModel.Strings)` (or `.Language`), as `MainViewModel` and
  `FaqsViewModel` do. Do not cache a resolved string in a field.
- Adding a language means a new `AppLanguage` enum member with its
  `[Description("…")]` (that description is what the settings combo box shows),
  plus a full `IStrings` and `IFaqs` implementation.

## Themes — 10 files, identical key sets

[`Themes/`](../LostieLauncher/Themes/) contains one `ResourceDictionary` per
theme — Volcarona, Zoroark, Infernape, Torterra, Empoleon, Mewtwo, Cefireon,
Sylveon, Astrem, Auretoskos — and each defines the same **28** resource keys.

- Adding or renaming a key means editing **all ten** files. A key missing from
  one theme is not a compile error: it is a runtime binding failure that only
  shows up once a user selects that theme.
- Adding a theme means a new `AppTheme` enum member plus a complete dictionary
  with all 28 keys, and a mention in the README theme list.
- Reference brushes and colors by key from XAML. **Never inline a hex color** in
  a View, a component or a style — it will not follow the active theme.
- Themes are applied at runtime through `SettingsViewModel.ApplyTheme`, which
  resolves `pack://application:,,,/Themes/{theme}.xaml`; keep the file name and
  the enum member name in sync.
