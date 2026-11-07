# Restore leaves the member's pre-cut carried rows in the custody table

- Status: Review — the reading is on record (2026-11-08, batch `20261108-a`); the open question is which restore step gives the member's carried items new instance ids without retiring the rows that named the old ones.
- Priority: Medium
- Category: Persistence / save system — the character restore's instance-id handling
- Source: found while reading the layer-end continue with a member in the world (`review/save-layer-end-save-and-restore.md`, row 1)
- Related: `review/save-layer-end-save-and-restore.md`, `review/save-mid-run-consistent-cut.md` (S3), `done/save-multiplayer-restore-and-backups.md` (S4)

## Design

Read against `docs/design/cross-cutting/saves-and-recovery.md` (the save/recovery family: what a restore
settles, what the kernel keeps as facts) and `docs/architecture/save-archive-format.md` (the archive's
character rows and the transfer table's id rules). The rule the defect breaks: a restore settles the world
as one state — the kernel's carried rows and the live bodies' instances must name the same ids. The
restore's id hand-off is the page's own subject ("the character rows carry the instance ids the world will
use"), so the fix belongs in the restore path, not in a later reconciliation.

## The reading

A host and a member stood in one world, each carrying four items. The host advanced a layer (the layer-end cut committed at the boundary), every client quit, the host continued the world, and the member joined the restored session. The host's custody line then read:

```
[ItemCustody] rows=314 objects=306 mismatches=4 — the first ones: 5197989525:RowWithoutObject:Carried, 9492956821:RowWithoutObject:Carried, 13787924117:RowWithoutObject:Carried, 18082891413:RowWithoutObject:Carried
```

Those four ids are the member's carried items from before the cut (its stored character at that time listed them). Its live body after the rejoin carried the same four item types in the same slots but with new instance ids (`35262760597@1:trashbag; 22377858709@3:lantern; 26672826005@4:dogfood; 30967793301@5:waterbottle` in the stored character), and the member's own client showed the correct four items. The host's body was unaffected.

## Why it matters

- The kernel table keeps four `Carried` rows that no object answers — a reader of the table would count eight carried items for that member.
- The custody checker names them as mismatches, so any later session that starts from this restore starts with four pre-existing mismatches.
- Nothing player-visible broke in this run; the defect is in the restored world's kernel state, which the next save inherits.

## Chain

Read from the batch's logs and the restore path:

1. The continue decodes the archive and calls `WorldRestoreApplier.Apply`; `kernel.Restore(checkpoint)` at
   `src/CasualtiesUnknownOnline.Runtime/Session/Persistence/WorldRestoreApplier.cs:269` brings back **every**
   item row the cut wrote, including the carried rows of characters no present peer claims.
2. `characters.ClearSavedCharacters()` and `binder.Apply(decode.UsableCharacters)` at `:332-333`:
   `WorldCharacterBinder.Apply` (`src/CasualtiesUnknownOnline.Runtime/Session/Persistence/WorldCharacterBinder.cs:128-205`)
   binds only the present claimants; the absent member's key stays unclaimed — log
   `Character steam-76561198863287957 has no claimant in this session; decision 162: that player joins as a new character. The file stays in the archive for a later claim.`
   and `Restored characters: 1 bound to present peers, 2 left unclaimed`.
3. The in-memory character table has no entry for that member, so `HandshakeHandler`'s
   `CharacterData.SendSavedCharacter` sends nothing (`CharacterDataStore.SendSavedCharacter` returns silently on
   a missing entry, Debug only), and the member's client judges itself a NEW player:
   `StartingSupplyCoordinator` grants the starting supplies — four generation items with **new** ids
   (`ItemTrace op=0..3 origin=OnItemPickedUp events=[Pickup, GenerationItem]`).
4. The kernel therefore keeps the archive character's four carried rows (old ids) forever —
   `RowWithoutObject:Carried` in every custody read and in every later save.

## Design

The row belongs to a character the restore did NOT bind, so it must not enter the kernel at all. The claim
decision is computable before the restore: `WorldCharacterBinder.Plan(decode.UsableCharacters)` resolves the key
space and each stored character's claim against the present peers without touching the character table, and the
restore then filters those characters' rows out of the checkpoint:

- `checkpoint with { Items = checkpoint.Items.Where(row => !unclaimedIds.Contains(row.Identity.InstanceId)).ToList() }`
  where `unclaimedIds` is the unclaimed characters' carried ids, recursively (`RestoredItemIds.Collect`'s rule).
- The id seed stays over the **full** checkpoint and **all** decoded characters
  (`RestoredItemIds.Of(checkpoint, decode.UsableCharacters)`, `WorldRestoreApplier.cs:282-289`): the counters must
  resume above the archive's ids even for a character this session did not bind, or a later claim would collide.
- `binder.Bind(plan)` performs today's saves on the claimed characters; the unclaimed ones stay in the archive for
  a later claim (decision 162), with no rows in this session's world.

Rejected alternatives: (a) a kernel command to remove a character's rows — the terminal vocabulary (`Consumed`,
`Destroyed`, `ReplacedBy`) has no "never existed in this world", and a terminal row is still a row; (b) retiring the
rows at the member's join — the join path has no row-removal program either, and the defect would already be in the
restored table.

## Family

One seam, two cut kinds, two behaviours unchanged:

| member | how it reaches the table | what keeps it honest | touched? |
|---|---|---|---|
| the restore's bind (`binder.Apply`, the only caller at `WorldRestoreApplier.cs:333`) | mid-run continue | the claim rules (`WorldCharacterBinder.Apply:128-205`) + this fix | yes |
| the same seam | layer-end continue (the `w-20261107-1adb` batch) | `DropReplacedLayerKernelTables` drops world rows only; carried rows of unbound characters survive the same way | yes — fixed by the same filter |
| the cut's write (`WorldCharacterBinder.Collect`, `WorldCutTrigger`) | the archive's character files | one file per present member; a shared key is carried by neither (`:85-101`) | no — the write side is correct |
| the member's join (`HandshakeHandler` → `CharacterDataStore.SendSavedCharacter` → `StartingSupplyCoordinator`) | a new character with new ids | decision 162; the supplies are the game's own | no — a new character is the correct outcome, not the defect |
| `AppliedItemArchive.Audit` (`WorldRestoreApplier.cs:339`) | the applied-items carrier | names a disagreement, moves nothing | no |

The row filter is the root fix, not a guard at the custody checker: the custody line only NAMES the disagreement; the
rows must never enter the table.

## Exit

A member joins a session restored from a layer-end cut and the host's `[ItemCustody]` line reads `mismatches=0`; the stored character and the live body carry the same instance ids; a save taken after the rejoin preserves that state.
