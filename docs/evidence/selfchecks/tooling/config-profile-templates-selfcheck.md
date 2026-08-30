# Config Profile Templates — Self-Check (2026-08-31)

Delivery fact sheet for the backlog "Custom configuration template system": a
named full-config snapshot/apply store for BepInEx `ConfigFile`, exposed from
the Online UI Preferences page.

## What landed

- `ConfigurationProfileStore` (Runtime) captures every registered BepInEx
  `ConfigEntry` (section, key, serialized value) into a protobuf profile file
  and applies it back through the same entries. Applying writes through
  `ConfigEntryBase.SetSerializedValue`, so every `IOptionsMonitor<T>` sees the
  normal hot-reload path.
- Profiles live beside the live config under
  `BepInEx/config/CasualtiesUnknownOnline.Profiles/*.profile`; save, apply, list
  and delete are supported.
- The Online UI Preferences page has a **Config Profiles** section: a template
  name field, "Save current", and per-template Apply/Delete buttons. New
  English and Chinese strings were added.
- Because the snapshot is generic over all BepInEx entries, future
  display/nameplate/color preferences automatically participate once they are
  bound to the same config file.

## Mechanism inventory

| Mechanism | Change | Evidence |
|---|---|---|
| Snapshot | all bound `ConfigFile.Keys` → serialized values | `ConfigurationProfileStore.TrySaveCurrent` |
| Apply | `ConfigDefinition` lookup + `SetSerializedValue` + `ConfigFile.Save()` | `ConfigurationProfileStore.TryApply` |
| Hot reload | BepInEx entry change → `IOptionsMonitor<T>` notification | `Apply_TriggersBepInExOptionsMonitorHotReload` |
| Persistence | atomic protobuf file replace | `ConfigurationProfileStore.WriteAtomically` |
| UI | Preferences page save/apply/delete | `OnlineUiPreferencesDrawer.DrawProfiles` |
| Localization | en/zh keys | `LocalizationCatalog` |
| DI | plugin registers store with the live `ConfigFile` | `PluginDependencyRegistrar` |

## Verification

- `ConfigurationProfileStoreTests` covers save/apply round-trip, stale-entry
  skipping, hot-reload, sorted list/delete, invalid-name rejection, and corrupt
  profile failure.
- Full suite: 1808 tests green.
- `dotnet format`, `check-architecture`, event/entity gates, and
  `check-delivery` pass (see delivery commit).
