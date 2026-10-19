# Acceptance — S2b, the building-death drop bound on the machine

- Status: Evidence — TWO batches, each on a plugin deployed from its own commit and verified against that
  commit's build output. Batch `20261016-b` run 9 (the refusal shape: this host has no dying building at the
  cell) on artifact `0.1.0+9124a859cbbd31e7ce6a143730ac8557e751f33d`; batch `20261016-c` runs 1 and 2 (the
  population, the capture point, the breaker's own roll judged against the captured table, and one forged
  report split per drop) on artifact `0.1.0+32e4b857cd33b7905313f88269287e5621d59712`. Both
  `tools/verify-deploy.ps1` exit 0, "Deployment matches this tree's build output" (each batch's
  `*-verify-deploy.txt`). The tree has moved since batch `20261016-c` read it — a comment added to the census
  recipe and this record — so a later `verify-deploy.ps1` reports a newer product version; the delta above
  `32e4b857` is a recipe comment and documentation only, with no source change.
- Covers: `docs/backlog/done/validation-and-source-checks.md` S2b — acceptance row 1's building half in BOTH
  shapes (a building-death drop the host's own knowledge cannot account for is refused by name, nothing
  registered or relayed, the breaker answered and its copy destroyed; and a break whose cell really has a
  building losing its support, whose drops are judged against that building's own captured table) and row 6's
  half (an ordinary session meets no bound refusal it did not provoke)
- Artifacts: batch `20261016-b` — `run9.log`, `b9-anchor.json`, `b10-forge-*.json` (twenty forges),
  `b11-host-lines.txt`, `b12-guest-reject-lines.txt`, `run9.ps1`; batch `20261016-c` — `run1.log`/`run2.log`,
  `b10-census-host.json`, `b11-census-guest.json`, `c10-census-guest-early.json`, `c11-census-guest-late.json`,
  `c12-census-host.json`, `c13-real-break.json`, `c14-host-real-break-lines.txt`, `c15-forge-pair.json`,
  `c16-host-forge-lines.txt`, `c17-guest-lines.txt`, `run1.ps1`, `run2.ps1`. Probes:
  `tools/acceptance/recipes/block-drop-forge.cs` (its `building=1` half, and this cycle's `airwrite=0` and
  `item2`/`condition2`) and the new `tools/acceptance/recipes/building-support-census.cs`.

## Batch `20261016-b`, run 9 — the refusal where this host has no dying building

A member, through the product's own senders, reported twenty breaks on an absolute 5×4 grid around its own
cell (anchor `512,512`): each one sent the air write first and then a break report whose payload carried ONE
hand-built BUILDING-DEATH drop (`TrapDropEntryMsg`, item `rawcopper` at condition `0.9`) that no CUO gesture
emits. The host judged each report from its own record of the cell.

### Row 1 (building half) — PASS for the shape the host has no dying building for

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

### Row 6 (the ordinary-session half) — PASS for this run

No bound refusal appeared that the run did not provoke: every `[BlockBreak]` refusal line in the host window
is one of the twenty forged reports, and the member's own breaks are unaffected.

## Batch `20261016-c`, run 1 — the population question, answered the other way round

Run 9 left the captured-rows half owed and inferred its cause from its own grid. Run 1 replaced that
inference with a census of the live world, read on both clients seven seconds after world entry:

    host census:  total=1958 requireGround=1403 marked=0 listed=60
    guest census: total=289  requireGround=0    marked=0 listed=0

Two readings, both worth having. The host's world carries 1403 `requireGround` buildings, so the population
the capture reads is not absent at all — the 5×4 grid simply contained none of it. And the member's world, at
that moment, had 289 building entities and none of them `requireGround`: the two clients did not agree,
because `inWorld` flips long before a scene is materialized. Run 2 settled that second question.

## Batch `20261016-c`, run 2 — the captured rows, and both directions against them

Run 2 waited 25 s and then 50 s before reading, and the member's census agreed with the host's exactly:

    guest (25s) census: total=1904 requireGround=1365 withTable=1262 tableless=103 marked=0 listed=60
    guest (50s) census: total=1904 requireGround=1365 withTable=1262 tableless=103 marked=0 listed=60
    host  (50s) census: total=1904 requireGround=1365 withTable=1262 tableless=103 marked=0 listed=60

The population is shared, cell for cell, and 1262 of the 1365 carry authored drop data. The target was the
nearest unmarked one with a table — `leadbush` standing on cell `396,1002`, 122 blocks from the member's own
cell, with three chance-gated rows naming `blobflesh`.

The member broke that support cell through the game's own roll (`block-break-at x=396 y=1002 dmg=100`; the
block went `2 → 0`), and the host did exactly what S2b built:

    [INF] [CasualtiesUnknownOnline.GameAdapter.World.WorldEventSync] [BuildingSupport] marked remote death
    for requireGround building at (-115.8486,491.6073) after remote air-write.
    [INF] [CasualtiesUnknownOnline.GameAdapter.World.WorldEventSync] [BuildingSupport] captured 3 drop
    row(s) from 1 building(s) losing support at cell (396,1002) — the table a break report for that cell is
    judged against.
    [INF] [CasualtiesUnknownOnline.GameAdapter.World.BlockBreakSync] [BlockBreak] &lt;member&gt;'s break at
    (396,1002) accepted — 0 block drop(s) + 1 building drop(s) registered + relayed.

The two `[BuildingSupport]` lines are quoted as the run printed them. Their wording changed in later cycles
(the capture line now names how many buildings were still unmarked when the table was read); the reading —
three rows captured for that cell, one building drop accepted against them — is what this record is.

The member's own side folded its roll into the same report
(`[ItemTrace] op=24 item=0 origin=FlushPendingBlockBreak result=Committed(0+1) events=[Break, Drop,
BuildingDrop]`), and the host accepted it against the table it had just captured: the captured-rows half of
row 1, on the real path, with a REAL roll rather than a hand-built drop.

One forged report then read BOTH directions of the bound against those same captured rows. It carried two
building drops — `blobflesh@1`, which the three rows can account for, and `rawcopper@0.90`, which no row can
— and no fresh air write (`airwrite=0`: the shape the breaker's own fallback re-sends, zero damage included),
so the host judged it as a repeat against the record the accepted break carries:

    [WRN] [CasualtiesUnknownOnline.GameAdapter.World.BlockDropBoundJudge] [BlockBreak] &lt;member&gt;'s
    break at (396,1002) claimed 1 building drop(s) the 3 row(s) of the building(s) that lost support there
    cannot produce — refused by name (rawcopper@0.90), 1 drop(s) accepted and relayed.
    [INF] [CasualtiesUnknownOnline.GameAdapter.World.BlockBreakSync] [BlockBreak] &lt;member&gt;'s break at
    (396,1002) re-accepted (repeat report) — 0 block drop(s) + 1 building drop(s) registered + relayed.

Per-drop partition, on the real path: the drop the captured rows account for keeps its fate while the drop
they cannot produce is refused by name against rows that EXIST. The member was answered exactly once, for the
illegal drop's instance id (…289, the pair's second id; the legal one, …288, was not refused):

    [WRN] [CasualtiesUnknownOnline.Application.Kernel.KernelProtocolService] Kernel command rejected by host
    for item 910025968289: ItemSpawn refused (BlockAlreadyBroken).

## The correction these runs make

The residual run 9 wrote said "this map carries no `requireGround` building in the loaded area", and the
ticket repeated it. That is wrong as a statement about the map: the population is 1403 (that world) and 1365
(this one) entities, and the grid did not contain one. What run 9 actually established is narrower: no cell
of its 5×4 grid had a building losing support. The captured-rows half never needed a created population — it
needed a census to find the population that was there. `building-template-inject` would still add a
CUO-authored building template, but that belongs to the mod-content path and not to this bound.

## Limits of these readings

- One world per run, one member, one session: the census counts are that world's own (the runs generated
  different worlds, which is why run 1 reads 1403 and run 2 reads 1365), and the census lists at most
  `limit` entities in the order the engine returns them.
- The forged drop is the real client reporting a hand-built list, never the game's own roll for that item:
  what the forge reads is the host's judgement of a named drop against a real captured table. The one REAL
  roll this batch read is the member's own (`blobflesh`, accepted) — the frequency of rolls stays unbounded,
  the S2a limit unchanged.
- The probe's argument contract is strict, and this cycle added `airwrite`, `item2` and `condition2` to
  `block-drop-forge`: batch `20261016-b`'s `run9.ps1` needs those appended to re-run (the probe gained
  `building=` the same way in the previous cycle).
- A census read inside the world's materialization window lists a partial population (run 1). The recipe now
  carries that precondition where its next caller reads it.
- The member's break landed on a cell 122 blocks from its own body: the air write names the cell and the host
  applies it, which is the reach surface filed in `docs/backlog/done/member-report-bounds.md` (finding C of
  this cycle) rather than a property this row tested.
