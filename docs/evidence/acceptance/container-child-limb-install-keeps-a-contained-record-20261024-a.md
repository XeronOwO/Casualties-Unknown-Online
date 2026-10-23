# Acceptance — a bag child installed on a limb survives the bag's next report (the contained-record row)

- Ticket: `docs/backlog/done/container-child-limb-install-keeps-a-contained-record.md`
- Artifact: `0.1.0+7b02e9d3`
- Batch: `.acceptance/20261024-c/` (local, gitignored; run `limbrow`)
- Date: 2026-10-24
- Result: **the ticket's machine row passes** — the kernel row goes `Contained → Applied` on a native
  install, the bag's next report leaves it alone, and the take-off relocates the same instance id.

## What was run

1. Three clients in one world (the Steam prelude: host, member, bystander); the operator is the member.
2. The operator's `tourniquet` was placed by `item-provide mode=create` (the game's own grant) and loaded
   into a `fannypack` by `container-fill` (the game's own `Container.LoadItem`, so the adapter's container
   sync reported it). The HOST's kernel row then read
   `{"instanceId":"9492956821","type":"tourniquet","kind":"Contained","owner":"<member>","revision":3}`.
3. The wound-view apply ran the game's own `PlayerCamera.ApplyWoundItem(tourniquet)` with `selectedLimb` set
   to the operator's limb 3 — the exact call `PlayerCamera.TryPerformSpecialUIAction`'s `WoundViewLimb`
   branch makes. The limb gained `TourniquetScript`; the member's log carries
   `[LimbItem] the local limb action of tourniquet (id 9492956821) installed the item on limb 3; it keeps
   its id and now lives on that limb.`; the HOST received one `ItemApplyLimbCommand` and its kernel row read
   `{"kind":"Applied","owner":"<member>","revision":5,"condition":0.875}`.
4. The bag's next report: another item (`emergencylight`) was loaded into the fannypack, so the adapter sent
   the bag's container sync WITHOUT the tourniquet among its children. The HOST's row for the tourniquet was
   still `{"kind":"Applied","revision":5}` — the stale-content walk did not terminalise it.
5. The take-off: the component's own `TourniquetScript.TakeOff()` ran (the native wound-view take-off path);
   the item was revived into the operator's slot 0 with condition 0.875, and the HOST's row became
   `{"kind":"Carried","owner":"<member>","revision":6,"condition":0.875}` — the SAME instance id.

## Rows

| Step | Expected | Observed |
|---|---|---|
| Bag child, kernel row | `Contained` | `Contained`, revision 3 |
| Native apply | The seam admits the bag child; the row becomes `Applied` | `Applied`, revision 5, condition 0.875 |
| Bag's next report (child absent) | The applied row is not terminalised | `Applied`, revision 5 |
| Native take-off | The same id relocates to the operator | `Carried`, revision 6, same id, condition kept |

## Substitutions and limits

- The apply and the take-off were invoked through the game's OWN methods
  (`PlayerCamera.ApplyWoundItem`, `TourniquetScript.TakeOff`) rather than a mouse drag. The wound-view
  release's input sequence and the real reachable shape (a lagging kernel record) were established by the
  independent review and are recorded in the ticket; this run proves the seam/relocation behaviour those
  calls produce.
- The bag child was placed by the native grant and the native `LoadItem`, not by a drag out of a bag panel
  (the review showed the bag panel and the wound view are mutually exclusive).
- No OS-level mouse or keyboard input was used.
