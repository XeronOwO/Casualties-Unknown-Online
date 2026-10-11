# The item-id counter is not resumed by a restore, so a restored world can re-issue a live id

- Status: Todo
- Priority: High
- Category: Remote inventory / item identity / persistence
- Source: found by reading during the `favourite-marks-are-local-and-saved` cycle (2026-10-11), which
  keys a local favourite mark by the item instance id and therefore had to answer "does that id survive
  a save and a load". It does — but for a reason that leaves this question open, and the cycle that
  depends on it must not silently own the answer.
- Related: `docs/architecture/save-archive-format.md` §6.1 (the restored item reconcile),
  `docs/backlog/review/save-multiplayer-restore-and-backups.md`, `review/save-mid-run-consistent-cut.md`

## The claim to prove or refute

CUO instance ids are `(counter << 32) | SteamId` and the counter is PER PROCESS
(`ItemIdAllocator`, "Instance-id allocation for the item domain: ids are (counter, account id)").
The only thing that ever raises it is the host's grant to a guest
(`GameAdapterSessionBinding.OnItemIdWatermark` → `ItemIdAllocator.SetWatermark`, on the
`ItemIdWatermark` message, whose recorded purpose is the crashed-and-REJOINED guest). A world restore
re-stamps the ARCHIVED ids onto the rebuilt objects — `ItemStateCodec.RestoreItem` /
`WearableRestorer` for carried items, `RemoteItemSceneOps` and `GeneratedItemAuthority`'s
`ReconcileRestoredItems` for world items — and nothing resets or raises the allocator to the restored
high-water mark.

So a host that continues a world in a NEW process allocates its next item from counter 0 while the
restored scene already holds objects stamped from the previous process's counters. If the archive
holds any item the host itself allocated at counter 0 (the first item a fresh process hands out), the
next allocation collides with it: two live objects with one instance id, and `ItemInstanceId.ById` is
an id → object index that would then resolve to whichever registered last.

## What this ticket must do

1. **Make the claim falsifiable first**, at runtime, on the real machine: continue a mid-run cut in a
   new process, allocate one new host item (drop one from the inventory, or a fresh world item), and
   compare its instance id against the restored scene's id set and against the archive's rows. The
   probe is the deliverable of that step, not a fix.
2. If it reproduces, fix it where the restore lands: the id high-water mark is a fact the archive
   already carries (every restored row names its id), so the allocator can be resumed from the
   restored maximum at the same seam that re-stamps the ids — one counter per SteamId, exactly like
   the guest-side grant it already implements. If it does not reproduce, record WHY in this ticket
   with the reading that refutes it, and close.
3. Either way, check the sibling direction: a GUEST that rejoins after a restore resumes from the
   host's grant, which is the mechanism this ticket would extend to the host — the two must not end up
   with two different resume rules.

## Why it is filed rather than fixed in the cycle that found it

It is not the same mechanism: that cycle changes who owns a favourite mark and where it is stored, and
this one is about item-identity allocation across a process restart. The favourite ticket states the
dependency it now rests on (`docs/evidence/selfchecks/items/favourite-marks-local-and-saved-selfcheck.md`,
"Verification limits") and stops there, because a paper reading is not a reproduction.
