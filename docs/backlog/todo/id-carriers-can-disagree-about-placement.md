# The archive's two id carriers can disagree about where one id is

- Status: Todo
- Priority: Low
- Category: Persistence / item identity / restore
- Source: found by the independent adversarial review of `review/id-resume-sources-still-unseeded.md`
  (2026-10-11), which asked what the new two-carrier seed does NOT settle. It settles WHICH ids exist;
  it says nothing about the two carriers disagreeing about one id it names twice.
- Related: `review/id-resume-sources-still-unseeded.md` (the seed and the carrier reader,
  `RestoredItemIds`), `done/item-id-counter-is-not-resumed-by-a-restore.md`,
  `src/CasualtiesUnknownOnline.Runtime/Session/Persistence/RestoredItemIds.cs`,
  `src/CasualtiesUnknownOnline.Runtime/Session/CharacterData/TransferTableRestoreMerge.cs`

## The claim

An archive writes one item's instance id in two places: the kernel checkpoint's item rows (which state
its `ItemLocation` — world, carried, contained, worn) and `characters/<playerKey>.json` (which states the
body placement that item occupies). Both are written from the live world at different moments — the
character file by the 1 Hz report (`CharacterDataHandler` → `CharacterDataStore.SaveCharacterData`), the
kernel row by the item command that moved it — so for one item the two copies can describe different
places: a pickup that reached the kernel while the reporter's last snapshot still showed the item on the
ground, or the reverse.

The restore applies both halves independently — the kernel's rows through the checkpoint restore
(projected by `KernelBatchItemProjection`), the character rows through `WorldCharacterBinder` → the
character table → the adapter's carried restore — and nothing reconciles them against each other.
Whether that can produce a visible duplicate (the item in a body AND in the world) or a silent loss is
NOT established: the reviewer could not falsify reachability by reading, and no probe has been run.

## What would settle it

A probe on the physical machine that cuts a world inside the window: a guest picks an item up, the host's
kernel records the pickup, and the cut is taken before that guest's next character report — then read
`items.json`'s location for that id against `characters/<playerKey>.json`'s. If the two disagree, the
next question is which half the restore should let win (the kernel's placement is the arbitrated fact;
the character's is the body slot the player sees), and that answer belongs in the save-archive format
page as a rule, not in this ticket.

## Limits

- Nothing here is a defect claim yet: it is an unproven reachability found by review, recorded so it is
  not rediscovered. No acceptance rows until the probe names one.
- The landing this came from deliberately reads both carriers for WHICH IDS EXIST (`RestoredItemIds`); a
  later fix for this question must not narrow that back to one carrier.
