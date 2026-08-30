# Runtime log errors to investigate (2026-08-30)

- Status: Todo
- Type: Bug / investigation
- Category: Runtime observability
- Source: session log review 2026-08-30 (not yet analyzed)

Observed errors during the 2026-08-30 runtime session:

1. `TypeLoadException: Could not resolve type with token 0100002e from typeref
   (expected class 'System.ComponentModel.DataAnnotations.RequiredAttribute' in
   assembly 'System.ComponentModel.Annotations, Version=5.0.0.0')`
2. `ArgumentException: Getting control 1's position in a group with only 1
   controls when doing repaint` from `OnlineUiOverlay.Draw`.

Not confirmed related to the ragdoll issue or the duplicate item drop issue.

## Log file locations and last write times

- Host real instance:
  - `<game-dir>\BepInEx\logs\latest.log`
    — last write `2026-08-30 22:50:32`
  - `<game-dir>\BepInEx\LogOutput.log`
    — last write `2026-08-30 22:50:32`
- Sandbox guest instance:
  - `<sandbox-root>\BepInEx\logs\latest.log`
    — last write `2026-08-30 22:29:42`
  - `<sandbox-root>\BepInEx\LogOutput.log`
    — last write `2026-08-30 22:50:32`

No `CUO.log` file was present at either path.
