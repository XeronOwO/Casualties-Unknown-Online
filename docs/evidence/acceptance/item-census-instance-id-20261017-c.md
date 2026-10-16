# Acceptance record — the body census's own instance id

- Ticket: `docs/backlog/done/item-census-own-instance-id.md` — required work 1 and 2 are DONE and required
  work 3 is read on the machine in its two-client form. The ticket stays in `todo/` for the one thing the
  reading leaves it: what a row should say when an item genuinely has no id yet, and which of the two
  carriers a scenario should act on.
- Batch: `20261017-c` — `run.ps1` (one client, three readings) and `run-two.ps1` (two clients: `-Label two`
  read 11 s after the world-entry read, `-Label settled` read 65 s after it). **`settled` is the reading
  below**; `two` is kept because its two-extra-seconds difference is the whole lesson. Scripts, logs and
  JSON answers are in the batch directory.
- Commit: this cycle's commit — the recipe is read from the TREE by the driver, so the deployed artifact
  (`0.1.0+333d3a1a…`, `verify-deploy.ps1` exit 0) is unchanged by it and was not rebuilt.
- Run: 2026-10-17, 02:02:39 → 02:03:36 · Host: physical machine · Member: `Steam1` sandbox.
- What changed in `tools/acceptance/recipes/item-census.cs`: `mode=body` now reads an entry's id from the
  component that CARRIES it — the local `ItemInstanceId` (whose id is a PROPERTY, whose setter registers the
  object in the runtime index) for a live object, the remote `RemoteInventoryItemId` (a field) for a display
  proxy — every entry carries `idSource` (`local` | `proxy` | `none`) so the two can never be confused in a
  field a scenario then acts on, and the header's `returns:` line no longer advertises `ids[]`,
  `localSteamId` or `combineablePairs`, none of which any mode emits.

| # | Row | Verdict | Evidence |
|---|---|---|---|
| 1 | One item's id, read from BOTH sides, is the same id and each row says which carrier answered | **pass** | In one session: member `id='bandage' origin='slot0' instanceId='9492956821' idSource='local'` and host `id='bandage' origin='proxy' instanceId='9492956821' idSource='proxy'`; and the member's starting supply pairs the same way — `id='emergencylight' origin='slot3' instanceId='5197989525' idSource='local'` on the member, `id='emergencylight' origin='proxy' instanceId='5197989525' idSource='proxy'` on the host |
| 2 | Every row names the component that answered it, and all three answers occur | **pass** | `local` for the member's own items, `proxy` for the host's view of them and for the member's view of the host, `none` for the two own-body items whose own client has not stamped them (a host runs no carry-report pass, so its own starting supply reads `instanceId='0' idSource='none'` on both clients' rows about it) |
| 3 | A reading taken inside the world-generation window says something else | **the lesson, not a defect** | `-Label two`, 11 s after the world-entry read, answered `id='bandage' origin='slot0' instanceId='0' idSource='none'` for the item it had just created — while the host already showed `5197989525` for the starting supply. `inWorld` is `IsInWorldOrGenerating` (the driver's own `state` field), and `ItemIdAllocator.EnsureId` returns `0` while `HarmonyTraverse.IsGenerating()`, so no id can exist at that moment. `docs/acceptance/lessons.md` already carries both halves (2026-10-11: wait for the restore's own completion line, because no id can be allocated while the world is generating; 2026-10-08: a host's own supplies carry no id until they enter the domain) |

## Why the early reading looked like a defect, and what settled it

The `two` reading's own numbers contained the answer: the id the HOST showed for the member's item has the
MEMBER's account in its low 32 bits (`ItemInstanceIds.Compose(counter, accountId)` packs
`(counter << 32) | SpaceOf(accountId)`, and `ItemIdAllocator` is its only caller), so the member had already
allocated that id when the member's own census said it had none — the census had simply been asked too
early. Two seconds later the host saw it; sixty-five seconds after the world-entry read the member's own
row answers `idSource='local'` with the id itself.

The path that stamps it is the member's own carried-inventory report: `CarriedInventoryReporter` runs on a
guest and calls `ItemIdAllocator.EnsureId` for every carried item, which stamps an `ItemInstanceId` the
first time the world is no longer generating. That is also why a HOST's own supply has no id — no
carry-report pass runs there — and the census reports exactly that as `none` rather than as a failure.

## Limits

- **The census is not "finished" for every item**: its item set is a body's items, their descendants and the
  scene's `RemoteCloneRender` proxies, so a STANDING item object (the cross-player eat's target, which
  deliberately carries neither marker) is never listed. That is outside this ticket's required work and is
  named here rather than discovered later.
- **`owner` is meaningful only on the `proxy` branch** (`RemoteInventoryItemId` is its only carrier), so a
  `local` row reports `0`; `idSource` is what tells the two apart, and a scenario matching on `owner` has to
  read it first.
- **What this ticket still owes** is not a reader defect: a member's own item has no id until the
  carry-report pass stamps one, so a recipe that needs one must let the world settle first — and the ticket
  now says which paths stamp an id and which do not.
- **No runtime change**: nothing under `src/` moved, so no deployed behaviour differs from the artifact the
  batch verified.
