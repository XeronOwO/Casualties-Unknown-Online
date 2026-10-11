# The id resume reads one source: the synthesized-item counter and the character rows are not seeded

- Status: Todo
- Priority: Medium
- Category: Remote inventory / item identity / persistence
- Source: found by the independent adversarial review of `review/item-id-counter-is-not-resumed-by-a-restore.md`
  (2026-10-11). That landing resumes every id space from the restored archive's item rows; the review asked
  the completeness question of the rule itself ("does the restore carry EVERY id this process will
  re-stamp?") and found two sources the seed does not read. Neither is a defect of the landing: both are
  gaps in the same rule, one namespace over.
- Related: `review/item-id-counter-is-not-resumed-by-a-restore.md` (the landing and its rule),
  `Runtime/Session/Items/ItemInstanceIds.cs` (the format owner), `docs/architecture/save-archive-format.md`
  §3.4 (what a cut holds)

## 1. The host's synthesized-item counter is per process and a restore does not resume it

`OtherMedicalRemovalApplier.NextAwardedItemId` mints the id of a removed splint or tourniquet it awards
to the operator from its OWN counter, in the high-bit namespace:

```csharp
	private ulong NextAwardedItemId() =>
		ItemInstanceIds.AwardedHighBit | (_nextAwardedItemId++ << 32) | ItemInstanceIds.SpaceOf(_session.LocalSteamId);
```

The awarded item is a real carried item: it goes into the operator's character record and into the
kernel (`TrySpawnCarried` / `AdoptTransferredItem`), so it is in the cut's `items.json` and in
`characters/<playerKey>.json`. A world continued in a NEW process therefore re-stamps those ids onto
the rebuilt items while `_nextAwardedItemId` starts at zero again — the first removal after the continue
re-issues `AwardedHighBit | 0 | <space>`, an id the restored world already holds, with the same
consequences the parent ticket reproduced (kernel `Conflict (item … already exists)`, one id on two
objects).

The landing handles the collision hazard this namespace creates for the PLAIN counter (the resume skips
awarded ids, so the plain allocator is never pushed into that namespace), but the awarded counter itself
still needs its own resume point: the same rule — "the archive's ids state how far each id space's
counter had run" — read in the synthesized namespace. The seam is the same one
(`ItemIdCoordinator.ResumeRestoredIds` sees every restored id, high bit included), the consumer is
`NextAwardedItemId`, and the delivery is the local resume event.

## 2. The character rows are a second id carrier the seed does not read

`characters/<playerKey>.json` carries `CharacterItemMsg.InstanceId` per carried item, and the restore
stamps those onto the local body (`CharacterRestoreApplier.ApplyItems` → `ItemStateCodec.RestoreItem`
/ `WearableRestorer`). The seed reads only the checkpoint's item rows. For a mid-run cut the two are the
same items with the same ids — the probe's own archive showed the character file's ids inside the item
table's set — but the subset relation is not structural: the character record is filled by the live 1 Hz
report (`CharacterDataHandler` → `CharacterDataStore.SaveCharacterData`), which creates no kernel item
row, while the kernel learns a guest's carried ids through the registration cadence
(`ItemArbitration.RegisterCarried`, dense-then-60 s). An id that reached the character file inside that
window would be seeded for nobody, and the member whose process restarted would resume below it.

Closing it means seeding from both halves of the archive at the one seam that holds them together
(`WorldRestoreApplier.TryApply` has the checkpoint AND the decoded characters) — or proving the subset
relation structurally and recording the proof where the rule is stated.

## Acceptance matrix

| # | Scenario | Expected | State |
|---|---|---|---|
| 1 | Save a world in which a splint was removed (an awarded id exists), continue it in a new process, remove another splint | The new awarded id is above the restored awarded ids of that space, and no kernel refusal follows | pending |
| 2 | The same, read from the archive: the awarded ids in `items.json` and in the operator's character file | Both carry the same ids, and the resume point covers them | pending |
| 3 | A guest's carried id that reached the character file while its kernel registration was still pending | The restore's seed covers it (or the subset relation is proved and recorded) | pending |
