# Acceptance — S2b, the building-death drop bound on the machine

- Status: Evidence — batch `20261016-b`, run 9, one host + one sandboxed member, deployed artifact
  `0.1.0+9124a859cbbd31e7ce6a143730ac8557e751f33d` (`tools/verify-deploy.ps1` exit 0, "Deployment matches this
  tree's build output"; the batch's `b1-verify-deploy.txt`)
- Covers: `docs/backlog/todo/validation-and-source-checks.md` S2b — acceptance row 1's building half (a
  building-death drop the host's own knowledge cannot account for is refused by name, nothing registered or
  relayed, the breaker answered and its copy destroyed) and row 6's half (an ordinary session meets no bound
  refusal it did not provoke)
- Artifacts: `.acceptance/20261016-b/run9.log` (the run), `b9-anchor.json`, `b10-forge-*.json` (twenty
  forges), `b11-host-lines.txt`, `b12-guest-reject-lines.txt`, `run9.ps1`; the probe is
  `tools/acceptance/recipes/block-drop-forge.cs` with its new `building=1` half

## What the run did

A member, through the product's own senders, reported twenty breaks on an absolute 5×4 grid around its own
cell (anchor `512,512`): each one sent the air write first and then a break report whose payload carried ONE
hand-built BUILDING-DEATH drop (`TrapDropEntryMsg`, item `rawcopper` at condition `0.9`) that no CUO gesture
emits. The host judged each report from its own record of the cell.

## Row 1 (building half) — PASS for the shape the host has no dying building for

The host refused the drop BY NAME at every cell whose break it accepted, and registered and relayed none:

    [WRN] [CasualtiesUnknownOnline.GameAdapter.World.BlockDropBoundJudge] [BlockBreak]
    &lt;member&gt;'s break at (512,512) claimed 1 building drop(s), but this side marked NO building as
    losing support at that cell — all refused by name (rawcopper@0.90); a member's report cannot introduce a
    building's drops where this host has no dying building.
    [INF] [CasualtiesUnknownOnline.GameAdapter.World.BlockBreakSync] [BlockBreak]
    &lt;member&gt;'s break at (512,512) accepted — 0 block drop(s) + 0 building drop(s) registered + relayed.

The same pair of lines appears for `(513,512)`, `(514,512)`, `(513,511)` and `(514,511)`; the remaining cells
took the other refusal shape (`refused (the cell's break is not this sender's first-writer claim) — 0 block
drop(s) + 1 building drop(s) rejected`), which is the whole-report refusal that already existed and now names
the building half in its count. Either way the registered-and-relayed count is ZERO: nothing of the forged
drop entered the world on any side.

The member was ANSWERED, and its local copy destroyed, once per forged drop — twenty refusals, all naming the
BUILDING half's instance ids (`91…`, the block half's are `90…`):

    [WRN] [CasualtiesUnknownOnline.Application.Kernel.KernelProtocolService] Kernel command rejected by host
    for item 910033562624: ItemSpawn refused (BlockAlreadyBroken).

## Row 6 (the ordinary-session half) — PASS for this run

No bound refusal appeared that the run did not provoke: every `[BlockBreak]` refusal line in the host window
is one of the twenty forged reports, and the member's own breaks are unaffected. The run also produced the
capture evidence for the OTHER direction of this row — see the residual below, which is a property of the map
rather than of the bound.

## The residual this run NAMES (a row it could not judge, now with a reason)

**The "rows exist" shape was not read, and this map cannot produce it near the spawn.** The host's capture
line (`[BuildingSupport] captured N drop row(s) from M building(s) losing support at cell …`) does not appear
anywhere in the window, and neither does any `marked remote death for requireGround building` line: the loaded
area around the member's cell carries NO `requireGround` building, so no support loss was possible there and
no cell could have rows to judge the drop against. Reading that shape needs the case to be CREATED rather
than found — `tools/acceptance/recipes/building-template-inject.cs` injects a custom building template whose
declaration sets `RequireGround` and `DropOnDestroy`, and a placed instance then gives both peers the same
population to lose. Until that run lands, the captured-rows half of the bound is verified by the suite
(`GameBuildingDropTableTests`, the arbitration cases) and NOT on the machine, and the ticket says so.

## Limits of this reading

- One map, one area (a 5×4 grid at `512,512`), one member, one session; a different map or a mod-provided
  building could reach the capture path this run could not.
- The probe forges the REPORT, never the game's own roll: what is read is the host's judgement of a hand-built
  drop, not the frequency of legitimate rolls (the S2a limit, unchanged and named on the ticket).
- The run's own twenty air writes made holes around the member, so the later cells of the grid took the
  first-writer refusal instead of the building bound; both are refusals and the host's line names which ran.
