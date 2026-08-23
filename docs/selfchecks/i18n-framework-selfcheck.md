# I18n framework — en/zh localization for the CUO UI (no protocol bump)

Date: 2026-08-23
Scope: add a small key-based localization framework and migrate the CUO Online
UI user-facing strings to it, with English and Simplified Chinese support.

## What landed

- **LocalizationService** (Runtime): reads `LocalizationOptions.Language`
  through `IOptionsMonitor`, gives key-based `T`/`Format` lookup with
  English fallback, and fires `LanguageChanged` on config hot-reload.
- **LocalizationCatalog** (Runtime): built-in `en` and `zh` string tables for
  the Online UI. Unknown keys return the key, unknown languages fall back to
  English, `zh-*` normalizes to `zh`.
- **BepInEx config**: new `[UI] Language` entry (`en` / `zh`) in
  `PluginDependencyRegistrar`, backed by the existing
  `BepInExOptionsMonitor` pattern — editing the config hot-reloads the UI
  language.
- **Online UI migration**: all page/tab/button/status strings in
  `OnlineUi*Drawer` now go through `OnlineUiContext.T` / `F`; the old
  hardcoded English strings are removed (the close `✕` glyph and persona/Steam
  data remain non-translated by design).
- No protocol/wire change and no Mod API change.

## Mechanism inventory

| Mechanism | Change | Evidence |
|---|---|---|
| Config | `[UI] Language` bound to `LocalizationOptions` | `PluginDependencyRegistrar` |
| Localization | `LocalizationService` + `ILocalizationService` + `LocalizationCatalog` | Runtime `Localization/` files |
| UI rendering | `OnlineUiContext.T/F` used by every drawer | `OnlineUi*Drawer.cs` |
| Hot reload | `IOptionsMonitor` change → `LanguageChanged` | `LocalizationServiceTests` |

## Verification design

- **L0 tests**: 7 new `LocalizationServiceTests` cover English default,
  Chinese lookup, `zh-CN` normalization, unknown-language fallback,
  missing-key fallback, format and language-changed event.
- **Build + gates**: `dotnet build` 0 warnings/errors; `dotnet format`;
  `check-architecture.ps1` pass; full suite expected green.
- Static evidence: no wire/protocol surface touched; localization is a pure
  .NET service in the Runtime and the Plugin only calls `T`/`F`.
- No manual dual-side acceptance: per the development-period rule this is
  verified with L0 tests + static evidence.

## Accepted limitations

- Only the CUO Online UI is localized so far; other CUO surfaces (log lines,
  game-native menus) are not in scope.
- The source of truth for the current language is the local BepInEx config;
  there is no per-server language broadcast.
