# World-entity restore: the damaged building comes back whole

Batch `20261108-s2` on `0.1.0+75133718` (the live session, world `w-20261108-8cbc`, host in world).

## Fixture

- The host was moved to the settlement (`teleport x=0 y=490`) where the base's buildings stand.
- `building-health-set x=0 y=490 health=50` damaged the nearest building: the answer read `healthBefore: 300, healthAfter: 50`.
- `/save` was queued and the Command cut committed: `Cut Command committed for world w-20261108-8cbc … revision 4278 … 4 world-entity row(s) …`.
- `leave-world` answered `ok: true, inWorld: false`; `continue-run` answered `ok: true, inWorld: true`.
- The world-entry seam armed: `[WorldEntityKernel] the restored world-entity facts are armed for the world-entry seam (revision 4278, kernel restore 2): 2 consumption(s), 0 opened entit(ies), 0 health row(s).`
- The restore completed and the live world took the facts: `CUO restore of world w-20261108-8cbc reached the live world: … the live world took every restored world-entity fact (2 row(s)) …`.
- The same building's health was then read: `healthBefore: 300` — full, not the 50 the cut carried.

## What it says

- The cut carries four world-entity rows, but the restore arms only two of them, all consumptions: zero opened entities and zero health rows.
- The damaged building therefore comes back at full health after a continue — the player's damage is lost.
- This is the same refusal family the earlier batch reported (`restore INCOMPLETE (… 7 applied / 7 refused world-entity row(s))`), narrowed to a readable shape: the projection does not see a building-health row at all, so nothing is left to refuse.

## Limits

- One building, one cut kind, one continue. The opened-container half was not staged.
- The building was damaged through the game's own health setter; whether the same loss follows from a naturally damaged building is not measured here.
- The cut's four rows were not enumerated by kind in this run; the arm line is the only kind split.

## Next

Find why `WorldEntityKernelProjection` turns a cut's world-entity rows into consumptions only: read the projection's kind switch against the row kinds the cut writes, then make a health row arm (and fail loudly when it cannot) before the restore completes.
