# Acceptance record — the entity-event drop lands whole, and the fragile crystal does not exist

- Ticket: `member-report-bounds` — row `NetMsg.EntityEvent` (the drops half) rerun after the kernel
  floor: **row 2 PASSES**; row 3 is **unproven, now for a build-level reason**: the seven crystal
  prefabs the generator loads carry no `CrystalBehaviour` component at all, so no world can generate a
  fragile crystal. The ticket stayed in `todo/` on row 3 until the follow-up below withdrew the row and
  closed it.
- Batch: `20261019-b` — the rerun of batch `20261019-a`'s failed row against the fix commit. One
  ticket, two rows; rows 1 and 4 were judged in `20261019-a` and are not re-judged here.
- Commit: `dd1fd79e` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+dd1fd79ead2a76599bf72d364b70f4adc3a6dbeb`, checked with `tools/verify-deploy.ps1` (exit 0;
  `verify-deploy.log`)
- Run: 2026-10-19 19:20 → 19:25 (+08:00) · Host: the physical machine · Member: the `Steam1` sandbox ·
  No third client
- Dependencies: `dotnet`, `game`, `deploy`, `steam`, `sandboxie`, `hotrepl`, `input`, `logs`,
  `artifacts` — the preflight reported eleven present (`prelude-preflight.txt`); the active BepInEx
  tree was CUO's (`prelude-session-environment.txt`)
- Artifacts: the `20261019-b` subdirectory of the directory named by `acceptance-artifacts-dir`

## Why this batch

Batch `20261019-a` judged this row's four rows: the forged refusal and the unmodified session passed,
the fragile-crystal row was unproven, and **row 2 failed** — the member's real drop folded correctly,
but the whole authoritative composite was rejected by the kernel invariant
`building entity … has invalid health -751.9034`, so the accepted drop never reached the kernel. The
fix (`dd1fd79e`) floors a physical building health at the one boundary where it becomes a kernel fact
(`WorldEntityDomainModule.DecideRecordHealth`) and hardens the fact invariant against `±∞`. This run
reruns row 2 against the deployed fix and answers row 3's "find a world that generates one" with a
scan that includes INACTIVE objects (the `20261019-a` probe did not) and with the prefabs themselves.

## Method

The box's Steam client was started first: the `20261019-a` session had left it resident, it was not
running here, and the first `up.ps1` attempt waited on the script's hidden start without the process
appearing (recorded as a method note, not a row; the separate visible start was up in ten seconds,
`steam1-start.log`). `.acceptance/live/up.ps1` then brought the host and the member up, created lobby
`109775241475828703` and entered one run; both bodies stood at (0, 496.6) (`s1-*.json`).

- **Fixture**: host `/hostrules {"AllowMemberCheatVerbs":true}` and `/op <member>`
  (`s3-host-rules.json`, `s4-host-op.json`); member `/summon landmine 10,486` — the host created it
  (`[Summon] … summoned Entity landmine at (10.0,486.0)`, creation `<host>:3322485732`) and
  the member replicated it (`s5-member-summon.json`, `ev-*.txt`).
- **Trigger**: a local eval wrote `pressed = true`, `timeSincePressed = 0.9` on the member's own mine
  copy (`force-mine-press.cs`, `s12-member-force-press.json`), so the game's own mine `Update` ran the
  explosion; the recipe only supplies the press a body would make.
- **The roll**: the member logged `[TrapDropPending] kind=MineExploded … held for drop collection`,
  `[ItemTrapDrop] local scrapmetal (id 9492956821) … captured`, and
  `[TrapDropFlush] … drops=1` (`ev-guest-trap.txt`).
- **The host**: `[TrapEvent] kind=MineExploded … origin=HostApply from <member>`,
  `[TrapEvent] host applied mine explosion at (10.00, 486.00)`,
  `[ItemSpawn] materializing scrapmetal (id 9492956821)`, `[TrapDropPresentation] …`
  (`ev-host-trap.txt`), and **no** `[TrapDrop]`, `InvariantViolation` or `rejected` line since the
  run mark (`ev-host-refusals.txt`: `NO MATCHING LINES`) — the composite that failed in `20261019-a`
  committed.
- **The kernel**: `container-read mode=host,guest=auto` read the host's `world` list carrying
  `{"id":"9492956821","type":"scrapmetal"}` (`s19-host-kernel-after-mine.json`): the drop is an
  authoritative kernel row, not only the host's presentation. On the member the same id reports
  `[ItemSpawn] … already present locally — skipping duplicate materialization`, and its physics
  follows the simulated row; both sides carry the same drop.
- **The crystal scan**: `crystal-scan.cs` (`FindObjectsOfType(..., true)`, so inactive objects count)
  ran on the member: layer 0 — 2003 buildings, 14 crystal-prefab buildings, **0 with a
  `CrystalBehaviour`, 0 behaviours, 0 `CrystalFragile`** (`probe-crystals-guest.json`); after one
  `skiplayer` descent (`s21-host-skiplayer.json`) layer 1 — 2199 buildings, 14 crystal-prefab
  buildings, **0 with the component, 0 behaviours, 0 fragile** (`probe-crystals-layer1.json`).
- **The prefab probe**: `crystal-prefab-probe.cs` loaded the seven prefabs `WorldGeneration.PlaceCrystals`
  instantiates (`BloodCrystal`, `SoothingCrystal`, `ReliefCrystal`, `TurbulentCrystal`, `OxygenCrystal`,
  `EmissiveCrystal`, `DigestionCrystal`): every one `loaded=true`, `hasBehaviour=false`
  (`probe-crystal-prefabs.json`).

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | A member's destructive event with a drop its tables cannot produce is refused by name on the host and destroyed on the reporter | machine | pass — judged in `20261019-a`, not re-run | `member-report-bounds-entity-event-drops-20261019-a.md`; the deployed fix does not touch the judge |
| 2 | An ordinary event's drops land whole | machine | **pass** | the member rolled `scrapmetal` id `9492956821` into the event (`[ItemTrapDrop]`, `[TrapDropFlush] drops=1`), the host applied the event and materialized the drop with **no rejection line** (`ev-host-trap.txt`, `ev-host-refusals.txt`), and the host kernel's `world` table holds `{"id":"9492956821","type":"scrapmetal"}` (`s19-host-kernel-after-mine.json`); the member's copy is the same id and skips duplicate materialization |
| 3 | A fragile-crystal break, read on the machine | machine | **unproven — no such entity exists in this build** (the row is withdrawn by the follow-up below) | two layers scanned with inactive objects included carry 28 crystal-prefab buildings and ZERO `CrystalBehaviour` components (`probe-crystals-guest.json`, `probe-crystals-layer1.json`); the seven prefabs the generator loads all answer `hasBehaviour=false` (`probe-crystal-prefabs.json`); the decompiled assembly has no `AddComponent<CrystalBehaviour>` anywhere, and `WorldGeneration.PlaceCrystals` instantiates only those prefabs — so `CrystalFragile` can never be rolled, and no world can prove this row |
| 4 | An unmodified session meets no refusal | machine | pass — judged in `20261019-a`, not re-run | `member-report-bounds-entity-event-drops-20261019-a.md`; this run's single `[TrapDrop]`-family line is the accepted event, not a refusal (`ev-host-refusals.txt`) |

## The fragile crystal is not generated in this build

The failure of `20261019-a`'s row 3 was read as "this world carries no fragile crystal". This run
shows the stronger fact: **no world can carry one**. The generator's crystal step loads seven prefabs
(`PlaceCrystals`), and none of them carries the `CrystalBehaviour` component that owns the effect
list — the runtime prefab probe answers `hasBehaviour=false` for all seven, and two layers of the
world carry 28 crystal-prefab buildings with zero components between them. The class's
`GenerationFinished`/`SetUpEffects` path exists in the assembly, but nothing ever adds the component
(`AddComponent<CrystalBehaviour>` appears nowhere), so the component is inert and the effect list it
would roll is never created.

That makes the row's premise — a generated fragile crystal — impossible in this build. The row stayed
`unproven` rather than `pass`/`fail` in this run: the run could not put the world into the state the row
needs. The decision it asked for was taken afterwards and is recorded in the follow-up below.

## Follow-up — the row is withdrawn (2026-10-19)

The ticket needed a decision after this run: drop the row as an entity the shipped build does not have, or
define a fixture that injects the component and name it as such. The decision (recorded in the ticket,
`docs/backlog/done/member-report-bounds.md`) is to **withdraw the row**: a fixture would test CUO against an
object CUO made, not the game's own generated crystal, and no report from the shipped game's own world can
carry `CrystalFragileBroken`. The run's verdict stands as observed; the row is no longer owed, and the
ticket closes with rows 1, 2 and 4 passing. The kind and its handling stay in the code (the wire vocabulary
and the replay tables), and a future build whose prefabs carry the component brings the row back.

## Limits

- **The destructive carrier was summoned, not generated**, exactly as in `20261019-a`: the layer
  carries no mine, turret or effect-carrying crystal, so `/summon` created the landmine and a local
  eval supplied the press. The event chain from the press on is the game's own, but the row does not
  claim a naturally generated mine behaves identically at every trap density.
- **One member, no third client.** The relayed view of the drop on a third peer is not claimed; the
  member's own copy plus the host's log and kernel are the evidence.
- **The member's body stood ~14 units from the mine.** The explosion may have touched the body; the
  drop's capture and the host's application are independent of the body's damage state, and the
  member's log shows no intervening state change.
- **The crystal scan is a scene scan.** `FindObjectsOfType(..., true)` reaches inactive objects, but a
  crystal the layer never loaded at all would not be seen; the prefab probe is the build-level answer
  and does not depend on the scene.
- **The clamped Debug line was not read.** The host's log level is Information; the new
  `[TrapHealth]` line is Debug, so this run proves the clamp by its consequence (the composite
  commits and the drop lands), not by that line.
