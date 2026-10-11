# Acceptance record — Mod-authored item actions: the use and limb functions, and the cross-player chain

- Ticket: `mod-item-authored-actions` — verdict: **moved to `done/`**, with one finding filed separately
  (`docs/backlog/todo/mod-item-with-no-declared-tags-throws.md`)
- Batch: `20261011-k` — tickets: `mod-authored-effects`, `mod-item-authored-actions`,
  `mod-declared-behaviour-with-no-function` (plan: `20261011-k-scope.md`)
- Commit: `f9507771` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+f950777142ed1e1177c48bee349a5e9b3040f4ec`
- Run: 2026-10-11 22:25 → 22:35 · Host: physical machine · Guest: primary sandbox · one world per session
- Dependencies: the eleven ids `tools/acceptance/preflight.ps1` reports present; `input` (the in-process
  driver and its recipes) and `hotrepl` carried every setup
- Fixture: the in-repo example mod's DLL from this tree's build output, placed in the install's `plugins`
  tree for the run and removed at its close (`fixture-placement.txt`)
- Artifacts: the batch directory under the local artifact area, cited by file name only

## What the rows read

`example.dressing` is the fixture item: it declares `Usable` and `UsableOnLimb`, and the mod registers both
halves through `IModItemActions`; `example.placebo` is the sibling shape — declared usable with nothing
registered. The item is created through the game's own grant path (`item-provide mode=create`, which runs
`Utils.Create` + `Body.PickUpItem`); the limb halves are driven through the game's own wound-view release
(`remote-gesture mode=medical` locally, and the batch's own probe for the REMOTE target, now committed as
`tools/acceptance/recipes/remote-medical-release.cs`).

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | All three registrations are accepted by the rails on both clients (`use=True, limb=True, drink=True`) | `machine` | **pass** | `f1-host-fixture-and-storm.txt`, `f2-guest-session1.txt`: the mod's own verdict line on the host (22:25:22.715) and the guest (22:26:05.205) |
| 2 | Both item definitions materialize in the live game on BOTH clients, and the declared-usable item with no registered action still carries a delegate (`useAction` non-null) — the build site never leaves the game's own unguarded call with a null | `machine` | **pass** | `b1-host-defs.json`, `c1-guest-defs.json`: `example.dressing` `"usable":true,"usableOnLimb":true,"hasUseAction":true,"hasLimbAction":true`; `example.placebo` `"usable":true,"hasUseAction":true,"hasLimbAction":false`; the vanilla-shaped control `example.trophy` all false |
| 3 | A live `example.dressing` instance is created through the game's own creation path | `machine` | **pass** | `h1-host-create-dressing.json`: `"instanceId":"1112717510595","slot":0,"picked":true` |
| 4 | The registered USE action runs through the game's own `Body.UseItem` and writes the game's own fields | `machine` | **pass** | `h4-host-use-dressing.json` (`usable: true`, `heldAfter: true`) and `[Example] example.dressing used: condition 1 -> 0.9.` (p1, 22:31:24.975); the live reads `h3-host-defs-before-use.json` → `h5-host-defs-after-use.json`: slot 0 condition `1` → `0.9`, body happiness `-3.622` → `-2.612` (the action's own `AddCondition(-0.1)` and `AddVital(Happiness, 1)`) |
| 5 | The registered LIMB action runs through the game's own wound view on the acting client's own limb | `machine` | **pass** | `h6-host-wound-view.json` (own view, `limbLookingAt: 2`, `selectedIsLocal: true`) then `h7-host-medical.json` (`castIsWoundViewLimb: true`, `calls: "none"`, `dragAfter: "none"`): `[Example] example.dressing applied to 腹部 (#2): bleed 0 -> 0, condition 0.8.` (p1, 22:31:31.367); `h8-host-limbs-after.json` reads limb 2's `bandageSlow` as `0.242` seconds after the write of `0.5` (the game's own decay), where every limb read `0` before it (`h2-host-limbs-before.json`) |
| 6 | The registered LIMB action runs on the TREATED player's own client through CUO's cross-player limb-tool chain | `machine` | **pass** | Operator (host): `l1-host-remote-medical-open.json` (`opened: true`, `displayBodyName: MedicalDisplay_…`, `isRemoteInWorld: true`), `l2-host-wound-view-display.json` (`limbLookingAt: 2`, `selectedIsLocal: false`), `l3-host-remote-release.json` (`item: example.dressing`, `calls: "none"`); operator's log: `[ItemUse] <operator> uses example.dressing (id 1112717510595) on <treated> — the item's own limb action runs on the affected side.` and `[MedicalView] requested use of <treated> limb 2 with example.dressing`. **Treated (guest) client's OWN log**: `[Example] example.dressing applied to 腹部 (#2): bleed 0 -> 0, condition 0.7.` (p2, 22:32:25.814) and `[ItemUse] ran the game's own limb action of example.dressing (id 1112717510595) on the local limb 2; item condition left at 0.70.`; the host then committed the outcome: `[ItemUse] <treated> used on the affected side … condition 0.70.` and `[ItemUse] local item 1112717510595 condition set to 0.70 by cross-player use.` (p1) — the item's condition moved `0.8` → `0.7` on the strength of the treated client's own run |
| 7 | The invoker's guard: a throwing registered action costs the action and not the gesture | `machine` | **pass** | Forced setup `n1-host-plant-item-guard.json` (`registered: true`, through `IModItemActions.TryRegisterUse` for `example.placebo`); `n3-host-use-placebo.json` (`usable: true`, `heldAfter: true` — the use completed) then `[ItemContent] cuo.example's use action for example.placebo threw — the gesture completed without it.` (p1, 22:33:21.132) |
| 8 | The paths this change touches logged no unintended error; every error-level line is attributed | `machine` | **pass** | p1/p2 as in the sibling record: the two planted guard throws are row 7's own evidence; the `ItemInfo.HasTag` exceptions are the separate finding below, not this surface |

## The finding this run turned up, and where it went

Creating `example.placebo` threw `ArgumentNullException` from `ItemInfo.HasTag` on BOTH clients — the
definition declares no `Tags`, so the derived `actualTags` array stays null and the game's own
`Item.Start` / `PlayerCamera.HandleGunMenu` dereference it. It is **not** this ticket's change: the guard was
written by `a981a93d` (2026-09-03, the advanced item behaviour slice) in the item content provider, and
`8de7f5ed` (2026-10-08, the declared-behaviour cycle) moved it verbatim into the new `ModItemInfoFactory`;
this cycle's own commit never touched those lines, and the definition WITH tags (`example.dressing`, rows 4–6)
threw in neither session. It is filed as
`docs/backlog/todo/mod-item-with-no-declared-tags-throws.md` with the run's own measurements, including the
`CallContext` scope leak that turns the per-frame half into a process-wide error storm.

## Residuals for the user

None. Every row is a machine read this run produced.

## Limits

- **Two clients, one world.** The cross-player row is judged from the operator's and the treated client's
  own logs and readings in one session; a third-party view of the treatment was not taken and no row claims
  one.
- **The guards' throwing action is a run-planted function** (the fixture ships none), registered through the
  product's own `IModItemActions` rails; the registration verdict is part of the evidence, and the shape was
  promoted into `tools/acceptance/recipes/plant-throwing-effect.cs`.
- **The treated limb's own field was read after the game had decayed it.** `bandageSlow` writes `0.5` and
  the run's read of the treated client's limb came ~40 s later, when it had decayed to 0; row 5 reads the
  same field within seconds on the acting client (`0.242`) and names the decay, and row 6's evidence is the
  treated client's own log line plus the item condition the chain committed. A limb-field read of the
  treated body would need an immediate probe, which is one recipe away and not this ticket's row.
- **The injection call site was not driven** (it needs the syringe minigame, which no committed recipe
  stages); the fixture's `Injectable` declaration reaches the topical half only in this run.
- The `example.placebo` shape is judged by the sibling ticket's row (a declared flag with no function); this
  record's row 2 reads its delegate, and its tag defect is the finding above.

## Closing state

Both clients quit through the game (0 game processes left), the fixture DLL was removed and the removal
re-read, and the run's artifacts stayed in the batch directory. The remote-target release the cross-player
row needed, which no committed recipe covered, moved into
`tools/acceptance/recipes/remote-medical-release.cs`.
