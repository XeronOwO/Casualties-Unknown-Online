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

## What to find

- Which step allocates the restored items' instance ids: the character restore applies the stored ids (`[RestoredItems] apply instance <id> …` lines in the member's log) but the live body ends with new ids — the restore, the pickup/creation fallback, or the materialization path is the candidate.
- Whether the old rows should be retired when the new ids replace them (the transfer table's retire path), or whether the restore should force the stored ids into the live body.
- The fix must keep the layer-end row-1 state intact (a member's carried items come back as the same four items in the same slots) and must leave `[ItemCustody] mismatches=0` after the rejoin.

## Exit

A member joins a session restored from a layer-end cut and the host's `[ItemCustody]` line reads `mismatches=0`; the stored character and the live body carry the same instance ids; a save taken after the rejoin preserves that state.
