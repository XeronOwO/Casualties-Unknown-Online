# Acceptance record — Remote inventory operations: run the native path end to end

- Ticket: `remote-inventory-native-parity-rework` — verdict: **back to `todo/`** (status field
  `- Status: Todo — Rejected (…)`): matrix row 6's OCCUPIED-destination half (the swap) now passes on every
  client, and rows 7 and 8 stay `unproven` because this session staged no fixture for them
- Batch: `20261006-g` — tickets `remote-inventory-native-parity-rework`,
  `container-content-event-gap-on-repick` (the reference reading its acceptance asks for)
- Commit: `c2eb9e99` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+c2eb9e99b1cbd7467bdc236a2ea5d3e8bfac67f8`
- Run: 2026-10-06 21:50 → 22:08 local · Host: physical machine (operator) · Guest: sandbox (owner) ·
  third peer: the alternate sandbox
- Dependencies: the eleven the batch's preflight reported present. The third client was recovered mid-run
  (see Limits); the two reading clients were up for the whole run
- Artifacts: `20261006-g/` in the directory named by `acceptance-artifacts-dir` (release dumps, item trees,
  ring listings and the per-step log marks)

## The rows this batch drove

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 6b | Matrix row 6, OCCUPIED destination: the swap runs on the owner's body | machine | **pass** | `g1-host-release-onto-occupied.json` — the operator's release over the occupied slot 3 ends with `calls: "none"` and `dragAfter: "none"` (batch `20261006-f` read `TargetInvocationException` and a drag that was never cleared); the operator logs `[RemoteIntent] SwapSlots captured for item 9492956821 of 76561198863287957 (container 0, slot 3 …)` and forwards it; the owner logs `[PickUpResult] emergencylight → slot (slot 0)`, `[PickUpResult] bandage → slot (slot 3)`, `[SlotMoved] bandage (id 9492956821) → slot 3 (Swap) reported.`, `[SlotMoved] emergencylight (id 5197989525) → slot 0 (Swap) reported.` and `[RemoteIntent] replayed native SwapSlots on item 9492956821 (container 0, slot 3)`; the owner's own tree reads the two items exchanged (`d1-guest-tree-after-swap.json`) |
| 6b′ | The same row re-driven with the third peer in the world | machine | **pass** | `g4-host-swap3.json` — the same five lines on the operator and the owner, and the third peer's view converges (it reports the two slot changes as a divergence, below) |
| 6a | Matrix row 6, EMPTY destination (the control) | machine | **pass** | `g7-host-empty-slot.json` — `castSlot: 2`, `castItem: "none"`, `calls: "none"`, `dragAfter: "none"`; the operator logs `PickUpToSlot captured for item 9492956821 … slot 2` and forwards it; the owner logs `[SlotMoved] bandage (id 9492956821) → slot 2 (Drag) reported.`, `[PickUpResult] bandage → slot (slot 2)` and `replayed native PickUpToSlot`; **zero** `divergence` lines on either viewer |
| 2t | Matrix row 2's transfer half: the held owner item lands on the OPERATOR (`TransferToBody`, the double-Tab take) | machine | **pass** | the operator's own ring is open, the remote backpack view is closed, and one call drags the owner's proxy and releases it onto the operator's slot 2: `[PickUpResult] bandage → slot (slot 2)`, `[RemoteIntent] TransferToBody captured for item 9492956821 of 76561198863287957 (container 0, slot 2, body 76561198281246659 …)`, forwarded to the owner, and the drop is reported with `calls: "none"`. The custody fact moves on both sides: the operator's tree carries `bandage (id 9492956821)` in slot 2 (`d2-host-tree-after-transfer.json`) and the owner's tree no longer carries it at all (`d3-guest-tree-after-transfer.json`) |
| 7 | Use / wear / combine / battery load-unload / favourite | machine | **unproven — not driven** | no battery receiver, no combineable pair and no favourite key were staged; the verdict carries over from batches `20261005-d`, `20261005-e` and `20261006-f` |
| 8 | Held remote item used from the medical panel | machine | **unproven — not driven** | no treatable limb fixture and no wound-view staging this session |

## What the swap half's re-read shows

The seam asymmetry batch `20261006-f` found is closed on a real session. The release that used to abort
inside the game's own argument list now walks the branch, produces the `SwapSlots` intent, and the owner's
client replays the native sequence: both items exchange slots on the owner's real body and the owner reports
both moves as `(Swap)`. The three checks the batch asked for are all green — the operator's capture line
exists, the owner's `replayed native SwapSlots` line exists, and neither client logs a `UnityException` nor
`produced no intent — unclassified native gesture`.

The one thing this run adds to the row is a monitor reading, below: the swap converges on the viewers, but
each viewer learns it from the periodic snapshot rather than from an event.

## The item-sync monitor finding, and the reference reading the sibling ticket lacked

Three readings in one session, all on the deployed artifact:

1. **The swap (an item displacing an occupant) warns twice per viewer.** After the operator's swap, the
   operator and the third peer each log two lines of the shape
   `[CharSync] divergence for …'s bandage (id 9492956821): slot 3 → 0 — a carried move without an event sync
   (the 1 Hz snapshot carried it).` The owner logs none.
2. **The same swap done by the OWNER, purely locally, warns identically.** The owner dragged its own slot
   item onto its own occupied slot (the native path, no CUO intent involved): the owner reports the two
   `(Swap)` moves, and both viewers log the same two divergence lines. This is the reference reading
   `todo/container-content-event-gap-on-repick.md` names in its acceptance — for an item move, the warning
   is the native path's own behaviour, **not** an artifact of the intent replay.
3. **A move onto an EMPTY destination warns on nobody** (row 6a), and the custody transfer warns once per
   viewer with its own message (`left the inventory without an event sync`).

Read together, the discriminator is the move's shape, not who drove it: a slot move whose destination
displaces another item, and a custody move that removes an item, leave the viewers to learn the change from
the periodic snapshot; a plain slot move reports an event that reaches them. The three readings are filed on
`todo/container-content-event-gap-on-repick.md`.

## A finding outside this ticket: a late joiner never binds the host's generated enemies

The third peer joined the lobby and entered the world **after** the run had started. It then logs, once per
60 s cycle, `[Enemy] generation spawn pairing failed (69 host vs 69 guest generated enemies) — generated
copies stay local (generation divergence); runtime spawns are still bound.` while the guest that had joined
before the run reads `[Enemy] snapshot applied: 69 generated bound, 0 runtime spawns, mapping=True.` in the
same cycles, and the host sends both peers the same `69 enemies, 0 runtime spawns` snapshots.

The counts agree, so this is not a counting defect: the late joiner's copies never bind to the host's
anchors, which is the failure shape the closed `done/enemy-runtime-spawn-classification.md` fixed for a
member present at world entry. It is filed as `todo/enemy-generation-pairing-late-join.md`; this batch's
inventory rows are unaffected by it.

## Residuals for the user

None: every row this run drove is a machine row, and the rows it did not drive are named `unproven` with
their missing fixtures rather than handed over.

## Limits

- **One direction and one reading per row.** The operator was the physical-machine host and the owner the
  sandbox guest; the reverse direction (the guest operating the host's backpack) is covered by the earlier
  batches, not by this one.
- **The third client was recovered mid-run.** The machine's sandbox service had crashed before the batch
  (nothing to do with CUO), and the alternate sandbox would not start a program again until its stale
  process tree was terminated. Rows 6b (first reading) and the reference reading were therefore driven on
  the operator and the owner only; row 6b′ plus rows 6a and 2t carry all three clients.
- **No visual or feel judgement.** Every row above is a message, a state or a log line; no frame was read
  and no sound was judged. Matrix rows 12 (the operator's screen) and the native feedback stay the rows a
  later run judges.
- **Rows 7 and 8 keep their `unproven` verdict** and still need their fixtures built.
- **The transfer row is not a matrix row of this ticket**: it is matrix row 2's transfer half, driven here
  because the batch was the first with the state staged for it. Its own acceptance stays matrix row 2.
- **No wire or save check**: the run read no messages and no saves.
