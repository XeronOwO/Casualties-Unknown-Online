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
- Extended selector resolution with bracketed filters: `type`, `name`,
  `distance` (including ranges), `limit`, and `sort` (nearest/furthest/random/
  arbitrary). Unknown keys and malformed selectors fail closed.
- Added bracket-aware selector completion (`@a[type=`, `@a[type=player`, ...)
  so the console can guide filter entry.
- No wire protocol change; selectors still resolve over the local player
  entity table.

Selfcheck: `docs/evidence/selfchecks/ui/command-console-selfcheck.md`.
