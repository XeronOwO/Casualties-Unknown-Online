# Move the acceptance input channel out of the shipped mod

- Status: Done — the extraction and the type split both landed, and every row was read on the deployed `0.1.0+e37af116`
  (batches `20261104-a/b/c`, 2026-11-04): the production install carries no channel, the batch install's report is
  byte-identical to the pre-change one (`identical=True`, 532 characters) with the two types at 290 + 394 lines, and
  `docs/architecture-debt.json` no longer names this ticket. Decided by the user on 2026-11-02.
- Priority: Medium-High (production cleanliness: release players currently carry a few hundred lines that only a batch uses)
- Category: Build / packaging / acceptance tooling
- Source: user direction, 2026-11-02 — "split it into a separate plugin and remove it from the game's production build".

## Run sheet (cycle 2026-11-04: split the staged-input type so its ceiling needs no exemption)

The remaining work is a line-count question, not a packaging one: the extraction landed, and the type it moved is still
over the 600-line aggregate ceiling. The order is the gate's: prove the debt is real, split the type, then prove the
split on the install.

- [x] **Step 1 — the debt is real, and the gate can see it.** Remove this ticket's entry from
  `docs/architecture-debt.json` and run
  `dotnet test tests/CasualtiesUnknownOnline.NormativeGates.Tests/CasualtiesUnknownOnline.NormativeGates.Tests.csproj --filter Architecture_OneTopLevelTypePerFileAndAggregateLimits`.
  Evidence: the failure line `… : <n> aggregate lines (max 600; …)`. If the gate stays green, the entry is not what holds
  it and the split's premise is re-read before any code moves. — read 2026-11-04: `CasualtiesUnknownOnline.GameAdapter.
  Acceptance.VirtualInput : 604 aggregate lines (max 600; either split or record in docs/architecture-debt.json)`, exit 1.
- [x] **Step 2 — the split, with the facade intact.** `VirtualInput` keeps the staged state, the frame-stamped answers,
  the install/uninstall lifecycle and every member a recipe resolves through HotRepl; `InputQueryPatches` takes the
  Harmony entry points on `UnityEngine.Input`, the call-site replacement and their counters. Evidence: both types'
  aggregate line counts, and the install report's text compared with a pre-change run on the deployed artifact. —
  `VirtualInput.cs` 290 lines, `InputQueryPatches.cs` 394; the pre-change report (2026-11-04, deployed `94345286`) reads
  `mousePosition: patched (managed body) | GetMouseButton: patched (no IL body — native backend) | GetMouseButtonDown: … |
  GetMouseButtonUp: … | PlayerCamera.HandleInput: patched, 26 call(s) replaced, … applied yes | MinigameBase.
  UpdateHandVisuals: patched, 3 call(s) replaced | PlayerCamera.HandleDragging: patched, 2 call(s) replaced |
  SettingsMenu.WaitForKeyPress: patched, 0 call(s) replaced in the builder, 1 in the state machine`.
- [x] **Step 3 — the gate green without the exemption**: `docs/architecture-debt.json` no longer names this ticket, and
  the sweep table's block count for this project moves with the new file. Fails = fix the counts in the same change,
  never the gate. — the focused gate answers 0 failed / 1 passed, the ledger holds only the heal service, and
  `docs/design/audit/sweep.md` reads 8 files for `CasualtiesUnknownOnline.Acceptance`.
- [x] **Step 4 — build and the full suite.** Evidence: the build's summary and the suite's `Passed!` counts. — build
  0 warnings / 0 errors; `CasualtiesUnknownOnline.Tests` 6596 passed / 0 failed; the gates project 811 passed / 0 failed.
- [x] **Step 5 — the production install carries no channel.** `tools/deploy.ps1 -GameDir <dir>` prints its removal line
  and the installed plugin folder answers `Test-Path` False for the channel's DLL. — `removed the batch channel
  CasualtiesUnknownOnline.Acceptance.dll (production install)` and `Test-Path` False; the production client answered
  `ping` (`plugin=true, overlay=true`) and `state`.
- [x] **Step 6 — the batch install answers the same report.** `tools/deploy.ps1 -Acceptance`, one client launched, and
  `drive-in-process -Action recipe -Recipe virtual-input -RecipeArg mode=install` printing the same segments with the
  same replacement counts as the pre-change run. — byte-identical, 532 characters, `identical=True`; the pointer staged
  at `(640,360)` read back as `(640,360)`.
- [x] **Step 7 — one input row, judged the same way.** A staged key drives the game's own key-bind capture
  (`keybind-capture-state` / `keybind-read`), the row `done/enter-opener-keybind-capture.md` used. — a staged click left
  `captureWaiting=true` with the throw row reading `...`; a staged `F5` turned it `false` and the game's own table answered
  `throw → F5`; the cropped frames carry both halves.
- [x] **Step 8 — the record.** Rows 1-4 below filled with those readings, the ticket moved to `done/`, its index row
  moved, and the sweep and triage pages updated in the same commit. Fails = the ticket stays in `todo/` with the failing
  row named. — done in this commit; the triage page lists `review/` tickets only and never carried this one.

## Design

Read from `docs/design/cross-cutting/permissions-and-commands.md` (what a shipped verb may be) and `docs/acceptance/AGENTS.md`
(what a batch may do): the shipped mod carries player-facing behaviour; a batch's instruments are not part of that
behaviour and should not ship.

What the acceptance channel actually is, read from the tree: the eval endpoint is **HotRepl's** websocket (a separate
BepInEx plugin), and our side of it is two things — `VirtualInput` (the staged keyboard/pointer: Harmony patches on
`Input.GetMouseButton*`, call-site replacements inside `PlayerCamera.HandleInput`, `MinigameBase.UpdateHandVisuals` and
`SettingsMenu.WaitForKeyPress`) and the acceptance-only console verbs. The declarations and recipes are text the driver
sends, so they are already outside the build.

## Design comprehension and parity (cycle 2026-11-04: the type split)

This cycle changes where the answering code LIVES, not what the channel does. Its three parity lines are the unchanged
ones, and they are the check the split is held to:

1. **The player's action and expectation — none.** No player reaches this type: the channel is the acceptance agent's
   instrument (`docs/acceptance/AGENTS.md` rule 2), a production install does not carry the assembly, and a client that
   never installs it keeps the real keyboard, mouse and pointer.
2. **What the game itself runs — unchanged.** `UnityEngine.Input.mousePosition` (a managed getter), the three
   `GetMouseButton*` internals, and `Input.GetKey/GetKeyDown/GetKeyUp` at the call sites inside
   `PlayerCamera.HandleInput`, `MinigameBase.UpdateHandVisuals`, `PlayerCamera.HandleDragging` and
   `SettingsMenu.WaitForKeyPress`; the game's own branch still decides what a query means. The split moves the Harmony
   methods that answer those queries into `InputQueryPatches` and leaves every patched target, its prefix/postfix and its
   report line as they were.
3. **How the remote case reaches the same chain — per client, unchanged.** Each client's own channel answers its own
   client's queries; nothing here crosses players and no wire fact is involved.

**What the change must NOT alter**, and what the batch reads instead of trusting the refactor: the install report's eight
segments (`mousePosition`, the three button methods, `PlayerCamera.HandleInput` with its replaced call-site count,
`MinigameBase.UpdateHandVisuals`, `PlayerCamera.HandleDragging`, `SettingsMenu.WaitForKeyPress`), every member a recipe
resolves on `VirtualInput` by name through HotRepl, and the frame-stamped press/release semantics. The pre-change report
was captured on the deployed artifact before the type was touched and is compared with the post-change one in row 2 below.

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

### Self-check table (cycle 2026-11-04)

| Row | The instrument that judges it | What makes it fail |
|---|---|---|
| 1 | `deploy.ps1` (no switch) printing its removal line, `Test-Path` on the installed DLL, and one production client reaching `ping`/`state` | the DLL present, the removal line absent, or the production client not answering |
| 2 | `virtual-input mode=install` compared with the pre-change report, `virtual-input mode=move`/`virtual-key-stats` for the staged channels, and the key-bind capture row under a staged key | any missing segment, a zero replacement count, different text, or the capture not binding |
| 3 | `console-registry` on both installs, compared verb by verb | a verb present only in the acceptance build, or a verb lost in production |
| 4 | the focused architecture gate with the ledger entry removed, and the gates project | the gate naming the type, or `docs/architecture-debt.json` still naming this ticket |

## Where it stands (2026-11-02, 14:45)

The split landed and its first three rows read on the real install:

- `src/CasualtiesUnknownOnline.Acceptance` holds `AcceptancePlugin` and `VirtualInput` (617 lines together) and the shipped mod no longer references it. The types keep their namespace, so every recipe and declaration that reaches them through HotRepl is unchanged.
- **Production deploy:** `tools/deploy.ps1` without `-Acceptance` prints `removed the batch channel ... (production install)` and the plugin folder has no `CasualtiesUnknownOnline.Acceptance.dll` (read: `Test-Path` False).
- **Batch deploy:** with `-Acceptance` the same file is present (`Test-Path` True), and the channel installs in a running client: `virtual-input mode=install` reports `PlayerCamera.HandleInput: patched, 26 call(s) replaced`, `MinigameBase.UpdateHandVisuals: patched, 3 call(s)`, the three mouse internals on the native backend and `mousePosition` on its managed body.
- The stale-DLL sweep had to learn the new file name, or a batch deploy would copy it and then delete it in the same run (the first attempt did exactly that).

One row is open, and it is a line-count question rather than a packaging one: the moved type is still 616 aggregate lines, so the debt ledger has to keep naming it until the type itself is split. That is this ticket's remaining work.

## Where it stands (2026-11-04): the type is split, and every row read on the deployed artifact

Batches `20261104-a` (pre-change baseline), `20261104-b` (production install) and `20261104-c` (acceptance install), on
`0.1.0+e37af116`; `verify-deploy.ps1` confirmed the artifact before any row ran (`delivered artifact: 0.1.0+e37af116…`,
exit 0).

| # | Row | Verdict | The reading |
|---|---|---|---|
| 1 | a production deploy | pass | removal line printed, `Test-Path` False, the production client answered `ping` and `state` |
| 2 | an acceptance launch | pass | install report byte-identical (532 characters), pointer `(640,360)` read back, capture `true` -> `false` on a staged `F5` |
| 3 | the console | pass | `commands=14` and the same eight names on both installs |
| 4 | the gates | pass | `604` lines before the split, `290 + 394` after; 811 gates and 6596 tests pass |

### Row 2, in full

The post-change install report is byte-identical to the pre-change one captured on `94345286`: 532 characters each,
`identical=True`, and every segment present with its own count —

    mousePosition: patched (managed body)
    GetMouseButton / GetMouseButtonDown / GetMouseButtonUp: patched (no IL body — native backend)
    PlayerCamera.HandleInput: patched, 26 call(s) replaced, target PlayerCamera#100665115, applied yes
    MinigameBase.UpdateHandVisuals: patched, 3 call(s) replaced
    PlayerCamera.HandleDragging: patched, 2 call(s) replaced
    SettingsMenu.WaitForKeyPress: patched, 0 call(s) replaced in the builder, 1 in the state machine

with `installed=True, buttonPatches=3`. The staged pointer at `(640,360)` read back as `Input.mousePosition (640,360)`
(before and after the split), and the answering census after the run read `keyQueries=4668237, keyDownAnswers=2,
handleInputReplacements=1, buttonQueries=1636599, buttonDownAnswers=16` — the counters a recipe reads still measure the
game's own asks from the split's type.

### The key-bind row, judged as before

`settings-open` put the game's own `SettingsMenu` on its Controls tab (`parent="Canvas"`, `active=true`), a staged press
inside the throw row's rect started the capture (`captureWaiting=true`, the row reading `...`), and a staged `F5` turned
it `false` with the game's own binding table answering `throw -> F5`. The cropped frames carry the pair
(`20261104-c/row-waiting-wide.png`, `20261104-c/row-f5-wide.png`), and no CUO console panel appears on either.

**One side effect, checked rather than assumed.** The capture writes the bound key into the game's live settings, so the
batch's two gestures changed the throw bind in that client's memory (first `Return`, then `F5`). The game's own
`settings.json` was read afterwards: mtime still 2026-10-23 22:33 and `throw` still `116`, so nothing the batch staged
persisted and the user's configuration is as it was.

### Limits this stage leaves

- The first `settings-open` on a client that has no menu throws (the working order is `tab=-1` first, then the tab call);
  it is recorded in `docs/acceptance/lessons.md` rather than left for the next batch to rediscover.
- `PlayerCamera.HandleInput` was entered 0 times in this batch, because it ran before any world: the call-site
  replacement is proven by its own count (`26 call(s) replaced`) and by the key-bind capture, not by an in-world key.