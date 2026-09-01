# Command registration Attribute/reflection refactor

- Status: Review
- Priority: Medium
- Category: Tooling / UI / Mod API

Current command registration is hard-coded in
`CommandConsoleService.RegisterBuiltIns()`; each built-in command is a
`Register(...)` call with metadata plus a private handler method.

Goal:

- Refactor console command registration to discoverable `Attribute` + reflection
  form, matching the existing packet `[Handler(Key)]` pattern and the project
  convention for large registration families.
- Build an immutable command route/registry table once at startup.
- Keep command metadata (name, description, usage, permission, argument kinds)
  attached near the command implementation.
- Where appropriate, expose a public mod-facing registration surface through
  `CasualtiesUnknownOnline.Abstractions` so third-party mods can register custom
  console commands without depending on Runtime internals.

Constraints:

- Preserve existing built-in command behavior and all command-contract tests.
- Do not add wire protocol changes; console commands remain local.
- Keep `CommandConsoleService` under the architecture line-count gate by
  extracting real responsibilities (registry/handler discovery, not cosmetic
  splitting).

Landed:
- Built-ins are discovered via `[ConsoleCommand]`-marked methods into
  `ConsoleCommandRegistry`; the hard-coded `RegisterBuiltIns()` list is gone.
- Abstractions now exposes `IModConsoleCommands` / `ModConsoleCommand` through
  `IModContext.ConsoleCommands` for local mod console commands (no wire relay).
- `CommandConsoleService`, the registry, the mod adapter and the command
  context are separate responsibility units; architecture gates pass.

Selfcheck: `docs/evidence/selfchecks/ui/command-console-selfcheck.md`.
