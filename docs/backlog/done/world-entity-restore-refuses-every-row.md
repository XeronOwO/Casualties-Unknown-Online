# A continue can refuse every world-entity row, losing those facts at error level

- Status: Done — the destroyed-fact family is fixed and read on the deployed build (batch `20261108-s3`, `0.1.0+7b77a39a`): the blast's cut carried 10 world-entity rows, the seam armed 7 health rows, and the restore refused 2 instead of 6 — the four that flipped are destroyed buildings the regenerated layer does not have (satisfied now), the two still refused are live-health facts whose buildings the layer lacks (real divergence, refused on purpose). The earlier reading is batch `20261108-s2`. Readings: `docs/evidence/acceptance/world-entity-restore-20261108-s2.md`.
- Priority: Medium — the rows are world state a player sees (a consumed trap, an opened container, a damaged building);
  losing them silently changes the world after a continue, and the restore's own report calls it INCOMPLETE at error
  level.
- Category: Save / world-entity restore
- Source: the row-2 craft reading (2026-11-08), whose evidence file names the refused rows as a separate finding:
  `docs/evidence/acceptance/save-native-run-field-parity-20261108-row2.md` §Limits.
- Related: `done/save-mid-run-consistent-cut.md` (the cut family this state belongs to),
  `done/save-layer-end-save-and-restore.md` (the layer-end cut's entity rows), `docs/design/…` entity pages once the
  mechanism is read.

## Design

Read before opening this ticket: `docs/design/cross-cutting/saves-and-recovery.md` (what a continue carries: the world
facts a player saw — consumed traps, opened containers, damaged buildings — are part of the cut, and the restore's own
report is the record of what the live world took).

## The reading

Batch `20261108-s`, host continue of `w-20261108-d1cf` (the row-2 craft cut): the restore's account line read
`restore INCOMPLETE (… 7 applied / 7 refused world-entity row(s), 7 row(s) not taken)`; the recipe, block, keypad,
geyser and character halves of the same restore were complete.

Same evening, a later continue on the live session (world `w-20261108-8cbc`, revision 3624): the cut carried
`2 consumption(s), 0 opened entit(ies), 0 health row(s)`; the world-entry seam armed them and the live world took them —
so the path is not broken for consumption rows with no opened/health rows in the cut.

## The chain to read before any fix

- `src/CasualtiesUnknownOnline.GameAdapter/World/GameRestoredWorldFactSink.cs:125-144`: `ApplyWorldEntities` runs the
  three appliers in order — traps (`_entityEvents.OnTrapStateProjected`), opened lockables
  (`_buildingEntities.OnOpenedEntitiesProjected`), building health (`_buildingEntities.OnBuildingHealthProjected`) — and
  returns applied/refused counts.
- `src/CasualtiesUnknownOnline.GameAdapter/World/WorldBuildingEntitySync.cs:200-211` /
  `:224-235`: both building appliers resolve each row by POSITION at the world-entry seam; the health applier uses
  `Physics2D.OverlapPoint` (`:241-243`) and a miss is a refusal (`"no entity there (moved or already gone)"`).
- `src/CasualtiesUnknownOnline.Runtime/Session/World/RestoredWorldFactReplay.cs:315-342`: a refused entity row cancels
  the pending restore and logs the error, because "the regenerated layer has no such entity where the cut recorded it".

## The open question

Whether the 7 refusals were (a) opened/health rows applied before the regenerated layer's building entities exist or
are registered, (b) positions that shifted between the cut and the regeneration (the RNG baseline not restored for that
world), or (c) entities the game itself removed. The next batch stages one opened lockable and one damaged building
before the cut, then continues and reads the two appliers' per-row lines.
