# Move the acceptance input channel out of the shipped mod

- Status: Todo — decided by the user on 2026-11-02: the acceptance input channel is split into its own plugin and removed
  from the game's production build. `VirtualInput` is 616 aggregate lines and sits in the shipped adapter today; the debt
  ledger names this ticket until the extraction lands.
- Priority: Medium-High (production cleanliness: release players currently carry a few hundred lines that only a batch uses)
- Category: Build / packaging / acceptance tooling
- Source: user direction, 2026-11-02 — "split it into a separate plugin and remove it from the game's production build".

## Design

Read from `docs/design/cross-cutting/permissions-and-commands.md` (what a shipped verb may be) and `docs/acceptance/AGENTS.md`
(what a batch may do): the shipped mod carries player-facing behaviour; a batch's instruments are not part of that
behaviour and should not ship.

What the acceptance channel actually is, read from the tree: the eval endpoint is **HotRepl's** websocket (a separate
BepInEx plugin), and our side of it is two things — `VirtualInput` (the staged keyboard/pointer: Harmony patches on
`Input.GetMouseButton*`, call-site replacements inside `PlayerCamera.HandleInput`, `MinigameBase.UpdateHandVisuals` and
`SettingsMenu.WaitForKeyPress`) and the acceptance-only console verbs. The declarations and recipes are text the driver
sends, so they are already outside the build.

## Shape of the split

1. A new project `CasualtiesUnknownOnline.Acceptance`, a BepInEx plugin that depends on the adapter the way any mod does
   and owns: `VirtualInput`, the acceptance console verbs, and their registration on load.
2. The adapter loses both, and everything that referenced them (the capability catalogue entry, the console group
   registration, and the install call).
3. `tools/deploy.ps1` deploys the production plugin by default and the acceptance plugin only when the run asks for it;
   `tools/acceptance/launch-clients.ps1` includes it for a batch. A production install must not contain it.
4. The driver's recipes and declarations keep working unchanged, because they reach the client through HotRepl and call
   the same entry points — the acceptance plugin answers them under the same names.

## Acceptance rows

| # | Action | Expectation |
|---|---|---|
| 1 | a production deploy | the acceptance plugin is absent; the game starts and plays normally |
| 2 | an acceptance launch | the batch channel answers: staged keys/pointer drive the game, and one input row (the keybind capture) is judged as before |
| 3 | the console | the acceptance-only verbs are absent in production and present in the acceptance build |
| 4 | the gates | the adapter no longer exceeds its line ceiling; the ledger no longer names this ticket |
