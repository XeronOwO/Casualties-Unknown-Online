# Acceptance record — A mod item that declares no tags throws inside the game's own frames

- Ticket: `mod-item-with-no-declared-tags-throws` — verdict: **moved to `done/`**
- Batch: `20261012-b` — tickets: `call-context-scope-leak`, `mod-item-with-no-declared-tags-throws`
  (plan: `20261012-b-scope.md`)
- Commit: `ab246540` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+ab2465409c69fe7a88a03a789def56506e34980d` (`b4-verify-deploy.txt`: "Deployment matches this tree's
  build output.")
- Run: 2026-10-12 22:33:55 → 22:38:18, read from each client's own log's first and last line
  (`i6-run-window.txt`) · Host: physical machine, launched through Steam · Guest: primary sandbox
- Dependencies: the eleven ids `tools/acceptance/preflight.ps1` reports present — its own last line reads
  `RESULT: OK - a full two-client run is possible` (`b0-preflight.txt`)
- Artifacts: the batch directory under the local artifact area, cited by file name only

## What the rows read

The fixture is the in-repo example mod's `example.placebo`, which declares `Usable` and **no** tags — the
DTO's own default and the shape that used to leave `ItemInfo.actualTags` at the CLR default. The run placed
THIS tree's build output, `CasualtiesUnknownOnline.ModExample.dll` (28,672 bytes, SHA-256
`59AD72F87A2B5A5EDCEF861115C2AB6C372B6FCBE03A8F48E956920F04A5C5F2` — both read back from the install's
`plugins` tree and recorded in `b5-fixture-identity.txt`) into the install's `plugins` tree for the session
and removed it at the close; it is not part of the product's deployment (`b3-deploy.txt` ships none of it).

Each client's own log was marked before the scenario (host 22:36:40.278, guest 22:36:40.636), so the absence
rows below are counted over a window that covers creation, both hand-slot holds, both drops, both
replication hops and both pickups.

| Step | Artifact | Reading |
|---|---|---|
| host creates it in the hand slot | `f4-host-create-placebo.json`, `f5-host-body-after-create.json` | `mode create`, `slot 0`, `instanceId 983868491715`, `onBody true`; the body census then reads `example.placebo` at `origin slot0` |
| host holds it (frames of `HasTag` asks) | `f6-host-hastag-held.json` | the definition answers `arrayPresent true, arrayLength 1, tags "", probeTag "", hasCarried true, hasAbsent false` |
| host drops it | `f7-host-drop-placebo.json` | `dropped true`, `onBody false` |
| the peer materializes the replicated item | `i2-guest-materialize-lines.txt` | `[ItemSpawn] materializing example.placebo (id 983868491715)` — the host's own instance id, twice (22:36:56.108 and 22:37:04.983) |
| guest takes it into its hand slot | `g1-guest-pickup-placebo.json`, `g2-guest-body-after-pickup.json` | same `instanceId 983868491715`, `slot 0`, `onBody true`; the census reads it at `origin slot0` |
| guest holds it, then drops it | `g3-guest-hastag.json`, `g4-guest-drop-placebo.json` | the same definition answers identically on the guest; `dropped true` |
| host takes it back | `g5-host-pickup-placebo.json`, `i3-host-materialize-lines.txt` | the same `instanceId` again, `slot 0` on the host, after the host's own `materializing example.placebo` line at 22:37:29.673 |

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | A mod item whose definition declares no tags is created, dropped, replicated to the peer and held in the hand slot with no `ArgumentNullException` in either client's log over the run | `machine` | **pass** | The four halves are the table above, in both directions and with one instance id throughout. Over the mod-item window (`i1-window-counts.txt`) each client reads `ArgumentNullException 0` and `ItemInfo.HasTag 0` — host window 639,141 → 847,661 bytes, guest 1,215,220 → 1,519,652 — across the frames in which the item sat in a hand slot and the game's own per-frame path asked `HasTag` of it. Observed against the row's literal wording: the run's log as a whole DOES carry `ArgumentNullException` lines, 267 on the host and 384 on the guest, and every one of them belongs to the sibling ticket's probe injection, which nulls an existing item's array on purpose to measure the amplifier — they lie outside this window and are that record's evidence, so no line in the run is attributable to this item's own path |
| 2 | The same item's `Item.GlobalItems` entry answers `HasTag` for a tag it does not carry (false) and for one it does (true) — the derived array is present and correct | `machine` | **pass** | `f1-host-hastag-example-placebo.json` and `g3-guest-hastag.json`, both from the promoted `item-has-tag` probe: `arrayPresent true`, `arrayLength 1`, `tags ""`, `probeTag ""`, `hasCarried true` (the empty tag IS answered true — with no tags declared, the empty element `SetTags()` splits `""` into is the only tag the definition carries), `hasAbsent false` (the `gun` tag the game's own per-frame ask uses). Control on the declared definition: `f1-host-hastag-example-dressing.json` reads `arrayLength 2`, `tags dressing,medicine`, `probeTag dressing`, `hasCarried true`, `hasAbsent false` — the same probe, a different declaration, the answer following the declaration |
| 3 | The amplifier half's own row, once decided | `machine` | **pass** | The decision is the sibling ticket's rows, judged in this same session against this same artifact: `docs/evidence/acceptance/call-context-scope-leak-20261012-b.md`. That record's rows 1–4 all pass on both peers, and its own injected state is exactly this ticket's defect — the derived array absent again |

## Residuals for the user

None. Every row is a machine read this run produced, and no row asks for a person's judgment.

## Limits

- **The creation half is driven on the host only.** The item is created through the game's own grant path on
  the host, dropped, replicated to the guest, held there, dropped again and taken back on the host — so both
  directions of the replication hop are read, but a guest-side `Utils.Create` of a tagless definition was not
  driven. The defect was in the definition build site, which is per client, and the guest's own
  `Item.GlobalItems` entry is read directly, which is what the guest-side half would exercise.
- **The counted window is the scenario, not the whole run**: it opens at each client's mark (host 22:36:40,
  guest 22:36:41) and is read through the client's last line at 22:38:16/22:38:18, about 96 seconds; the
  scenario itself (creation to the last read) is the 54 seconds inside it. The run's other
  `ArgumentNullException` lines are the sibling ticket's injection, named in row 1.
- **One world, two clients, one session.** No third-peer view was taken (no row claims one), and every row
  here is a state rather than a timing, so repetition adds nothing.
- **The fixture keeps its empty `Tags` deliberately**, so the live regression case this ticket created stays
  live; nothing in this run needed the example mod changed.

## Closing state

Both clients quit through the game, and no game process was left behind. The fixture DLL was removed from the
install's `plugins` tree and the removal re-read (the plugins tree now holds only the pinyin mod's two
assemblies). The reusable half of this run moved into `tools/acceptance/recipes/item-has-tag.cs`, which reads
one definition out of `Item.GlobalItems` and asks it both questions the game's own callers ask; it is
committed with this record.
