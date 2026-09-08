# Command tree, resource-location completion, and selector filters

- Status: Review
- Priority: Medium
- Category: Tooling / UI / Mod API

Landed the second console completion slice:

- Added `CommandArgumentKind.ResourceLocation` and a `ConsoleCommandTree` /
  `CommandNode` model that drives argument-position completion.
- Added `ConsoleResourceLocationCatalog` for namespaced completion candidates
  (`cuo:player`, `cuo:bandage`, ...); mods can declare `ResourceLocation`
  arguments and receive them through the existing Abstractions console API.
  *(Superseded: the phase-18 `cuo:` placeholder vocabulary and its static
  catalog were replaced by the canonical content-id vocabulary — `cu:player`,
  `cu:<item id>`, `<mod namespace>:<id>` — see
  [Namespaced ID system](id-system-namespaced-ids.md) and
  `docs/evidence/selfchecks/tooling/content-id-selfcheck.md`.)*
- Extended selector resolution with bracketed filters: `type`, `name`,
  `distance` (including ranges), `limit`, and `sort` (nearest/furthest/random/
  arbitrary). Unknown keys and malformed selectors fail closed.
- Added bracket-aware selector completion (`@a[type=`, `@a[type=player`, ...)
  so the console can guide filter entry.
- No wire protocol change; selectors still resolve over the local player
  entity table.

Selfcheck: `docs/evidence/selfchecks/ui/command-console-selfcheck.md`.
