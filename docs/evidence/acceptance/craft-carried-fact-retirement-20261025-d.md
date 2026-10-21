# Acceptance record — A craft report never retires a carried material

- Ticket: `craft-carried-fact-retirement` — verdict: **pass, moved to `done/`**. The member crafted the
  wearable `slingbag` from four carried destroyable materials (then a second one with the wear slot
  occupied): every viewer applied four `[CarriedSync] removed …` lines from the ONE craft report, the host's
  apply line and the third peer's relay line are in the same window, **zero** `[CharSync] divergence` appears
  in any of the three windows over 6 s (six 1 Hz snapshots), and the post-craft records/clone trees show the
  consumed materials gone and the product exactly where the game put it.
- Batch: `20261025-d` — this ticket only. The same session read the enemy generation pairing failure both
  guests log; that is the separate ticket `done/enemy-generation-pairing-late-join.md` (rejected on this
  batch), not a row here.
- Commit: `426f71be` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+426f71beb856fb50487b5075daa5fb7551f46bc1` (`prelude-verify-deploy.txt`; `tools/verify-deploy.ps1`
  exit 0).
- Run: 2026-10-20 23:15 → 23:19 local · Host: physical machine · Guest: sandbox `<sandbox-guest>` · Third
  client: sandbox `<sandbox-alt>`.
- Dependencies: the eleven the preflight reported present (`RESULT: OK - a full two-client run is possible;
  the alternate third client is configured`; `preflight.log`).
- Artifacts: `20261025-d/` under the directory named by `acceptance-artifacts-dir` — the prelude reads
  (`prelude.log`, `session.json`, `prelude-verify-deploy.txt`, `prelude-session-environment.txt`,
  `preflight.log`), the fixture (`fixture-int.json`, `fixture-create-*.json`, `fixture-craftable.json`,
  `fixture-host-record0.json`, `fixture-guest-local0.json`, `fixture-host-clone0.json`,
  `fixture-alt-clone0.json`), the two craft answers (`row1-craft.json`, `row2-craft.json`), the post-craft
  reads (`row1-*`, `row2-*`), the log windows (`m-craft.txt`, `m-craft2.txt`) and the judgement notes
  quoted below.

## The fixture

The member's body carried the `emergencylight` starting supply (slot 3) and the four `slingbag` materials
created through the game's own grant path (`item-provide mode=create`: `foliagebag` 9492956821,
`string` 13787924117, `rope` 18082891413, `rope` 22377858709 — slots 0/1/2/4). Baseline reads: the host's
record carried all four with `isTransferred: true` and kernel `Carried` (`fixture-host-record0.json`), and
both viewers' clone trees showed them plus the light (`fixture-host-clone0.json`, `fixture-alt-clone0.json`).
The recipe is `slingbag` (declaration index 15, `INT` 7); the member's `INT` was set to 20 first so
`Recipe.TryMake`'s failure roll could not abort the craft (`fixture-int.json`).

## The rows

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | The owner crafts the wearable product from four carried destroyable materials: the report retires all four rows on every receiver, the host's apply line and the third peer's relay line are present, the product reaches both peers as a carried fact, and every viewer's window over at least one 1 Hz cycle carries ZERO `[CharSync] divergence` | machine | **pass** | `row1-craft.json`: `crafted: true`, product `slingbag` id 26672826005. Guest `.416` `[Crafting] Craft committed: 4 entries, 1 products.`, then `.418` `local body changed — immediate re-report` (the craft postfix's snapshot; no in-craft one). Host `.438 – .441` four `[CarriedSync] removed 9492956821 / 13787924117 / 18082891413 / 22377858709 from <member>'s snapshot — the operation's report destroyed it`, `.442` `[CarriedSync] added slingbag (id 26672826005)`, `.446` `[Crafting] Craft of <member>: 4 entries, 1 products applied.`, `.448` the member's next char data (after the report). Third client `.461 – .476` the same four removals + product add and `[Crafting] Craft relay of <member>: 0 world destroy(s), 4 carried retirement(s), 0 skipped entry/entries, 1 product(s) applied.` **Zero** `divergence` lines in all three windows. Post-craft: `row1-host-record.json` carries only `emergencylight` + `slingbag` (both `isTransferred: true`, kernel `Carried`); `row1-guest-local.json` has the light in slot 3 and the `slingbag` at slot -1 (worn); `row1-host-clone.json` / `row1-alt-clone.json` show the member's `slingbag` + light and no material |
| 2 | The occupied-slot variation: with the row-1 `slingbag` worn, a second craft's product is refused by the game (not worn, stays a world item), the incumbent keeps its row on both viewers, and every window carries ZERO divergence for the incumbent and the consumed materials | machine | **pass** | `row2-craft.json`: `crafted: false`, `result: slingbag`, reason "no product was picked up (the result may have landed on the ground…)" — `RecipeResult.SpawnResult`'s `AutoPickUpItem` called `WearWearable`, which the occupied slot refused (native code in the ticket's correction). Guest `.243` `[Crafting] Craft committed: 4 entries, 0 products.`; host `.254 – .256` the four fresh removals, `.256` `[Crafting] Craft of <member>: 4 entries, 0 products applied.`; third client `.261 – .262` the same removals and `[Crafting] Craft relay of <member>: 0 world destroy(s), 4 carried retirement(s), 0 skipped entry/entries, 0 product(s) applied.` **Zero** divergence in all three windows. `row2-host-clone.json` / `row2-alt-clone.json` still show the incumbent `slingbag` 26672826005 and the light; the refused product reached both peers as a WORLD item — host `.263` and third client `.282` `[ItemSpawn] materializing slingbag (id 48147662485) at (0.0,505.6)`, and the third client starts its physics at `.360` |

## The chain the run read

The craft's only immediate re-report is the craft postfix's: the wear postfix's in-craft snapshot was
withheld (no `local body changed` line exists before the guest's `.416` commit), and the one at `.418`
follows it. On the host the four retirement lines and the product's carried fact are applied in
`ApplyGuestReport` before the report's own `.446` line and before the member's next char data at `.448` —
the fact table was cured by the report, not by the snapshot. The third client applies the same report at
`.461 – .476` and logs that no snapshot preceded it as a divergence: the zero-warning window is the proof
that the retirement ran first, because the post-craft snapshot necessarily no longer carries the four
materials.

## Limits

- One world, one repetition per variation; the zero-divergence windows are 6 s each (six 1 Hz snapshots),
  which is the periodic cycle of the mechanism that can emit the warning, not a statistical claim.
- The member is the owner; the host-as-owner craft direction is covered by the simulation half only (the
  kernel-row and stamp tests) and stays a useful repetition.
- The refused product's world-item registration was read from the host's and third client's `[ItemSpawn]`
  lines, not from `item-world-read`: that recipe takes world CELL coordinates and this run passed the body's
  world position, so its empty answer is a reader-input mistake, not a missing item.
- A transport-refused craft report is still dropped without retry (the ticket's Limit); this run's report
  was delivered.
- The session's enemy generation pairing failure is read and rejected on the enemy ticket; it does not
  touch the item rows above.
