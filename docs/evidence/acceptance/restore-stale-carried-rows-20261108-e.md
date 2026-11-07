# Restore drops the carried rows of a character no present peer claims

- Batch: `20261108-e` (deployed `0.1.0+9efb0f6c`; the acceptance channel deploy)
- Ticket: `docs/backlog/done/restore-leaves-stale-carried-rows.md`
- Fix: commit `9efb0f6c` — `WorldCharacterBinder.Plan`/`Bind`, `WorldRestoreApplier` filters the restored checkpoint.

## Fixture

1. `launch-clients.ps1` brought the three clients up; the host created the run (lobby `109775243788771557`, all in world 01:17:03).
2. The host advanced a layer (`layer-descend twice=0`): `Cut LayerAdvance committed for world w-20261107-bc3b: layer-end cut taken at layer-boundary, revision 889, layer 1, 11 file(s) … 12 item row(s), 3 player row(s), 0 enemy row(s), 0 fluid chunk(s), 0 world-entity row(s), 3 character(s) …` — both members stood in the world with their carried items at the cut.
3. Every client quit; the host alone relaunched and ran the panel's Continue (`continue-run`): the restore line named `world w-20261107-bc3b … layer-end cut … revision 889, layer 1`.

## Readings

- The member was ABSENT at the continue, so its stored character was unclaimed:
  `Restored characters: 1 bound to present peers, 2 left unclaimed, 0 refused (key space Steam).`
  and the rows it carried were dropped before the kernel write:
  `The archive names 8 carried row(s) of 2 character(s) no present peer claims; they are not restored (decision 162: that player joins as a new character).`
- The host opened a session in the restored world (lobby `109775243788994229`); the two members rejoined at 01:19:12 (`all in world: True`) — they joined as NEW characters, and their starting supplies arrived as new items.
- The host's custody line after the rejoin settled at:
  `[ItemCustody] rows=316 objects=316 mismatches=0 — the kernel's rows and this client's objects agree.`
  (The transient line during the join read `rows=316 objects=308 mismatches=8`, the objects of the just-joined members not yet materialized; the settled line is the reading the ticket's exit names.)
- The host's own body kept its four items: `1:trashbag|3:lantern|4:dogfood|5:waterbottle` — the claimed character's rows survived the filter.

## What each settles

- The row's exit condition ("a member joins a session restored from a layer-end cut and the host's `[ItemCustody]` line reads `mismatches=0`") — the settled line reads 0 mismatches with 316 rows and 316 objects.
- The claim split kept the claimed character's rows: the host's four carried items are on its body and in the table.
- The id seed stayed over the full archive: the members' new items were allocated without collision (no id-rejection line in the batch).

## Limits

- The two members' old characters were unclaimed for the whole batch (they were absent at the continue), so the "later claim" half (a continue where the member IS present keeps binding its old character) was not re-run in this batch; it is covered by the binder's existing tests (`WorldContinueLocalCharacterTests.Continue_HandsTheCallerTheCharacterTheLocalPlayerClaims`).
- The batch deployed with `-Acceptance` (the channel the driver needs); the production deploy is the release-cycle's step.
