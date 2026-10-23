# Batch 20261023-e — the placement outcome's machine row: the give ran, the carrier dropped the flags

- Applies to acceptance row 1 of `docs/backlog/todo/placement-failure-report-anchor.md` ("a give applied at the
  answered destination reports the same place; the host sees a match and writes nothing").
- Artifact: `0.1.0+aaf89655cd9a988bb1af2136a1b6cabf97e2227e`, deployed and verified against `<game-dir>` before the
  clients started (`preflight.ps1`: 11 present, `RESULT: OK`; the deployed `ProductVersion` and the repository HEAD
  agree).
- Dependencies: `preflight.ps1`, `session-environment.ps1 -Mode status` (`active=cuo`, `game-running=false`,
  `launch=ok`), the physical pointer and keyboard. The pointer takeover is the standing authorisation of 2026-10-22
  ("随时可以接管，不需要向我提问"), used with the batch-local actuator under the local artifact area; nothing under
  `tools/` gained a cursor or a keyboard call.
- Run: host on the physical machine (the GIVER) and the first sandbox (the RECEIVER); one world, one session. The
  host's Online UI window was closed before the gesture.
- Setup substitutions (declared): both bodies stood at `(0, 501.615)` after the world entry; the receiver's was placed
  at `(3.5, 501.615)` with the `body-place` recipe; the giver's `bandage` was created with `item-provide
  mode=create` (instance `1181436987331`, its own answer: slot 0). The ring was opened with the game's own
  `toggleinventory` key and the drag itself was the real right-button press, move and release.

## What was observed

The ring showed the created item (the case batch `20261023-d` could not reach): `remote-gesture mode=list` read six
`slot` buttons with the item on the `slot 0` button, `pointerInside` true after the pointer was moved onto it. The
right-press started the drag (`dragItem=Item`, `radialOpen=true`). Moving the pointer into the receiver's detection
range opened its backpack on the same frame range (`viewOpen=true`, the focus named the receiver, the ring re-anchored on
the receiver) and the host wrote:

`[2026-10-23 19:36:22.138] [INF] [BackpackView] opened native backpack view for <receiver> — operable.`
`[2026-10-23 19:36:22.139] [INF] [BackpackView] opened <receiver>'s backpack because the dragged item entered its detection range (the give gesture's entry).`

The release landed on the receiver's `slot 1` button (`remote-gesture mode=list`: button `i=4`, `slot=1`, empty,
`pointerInside=true`, `viewOpen=true`). The give then ran end to end on the host's and the receiver's own clients:

- Host `19:36:41.883` `[RemoteIntent] GiveToBody captured for item 1181436987331 of <host> (container 0, slot 1, body <receiver>, …)`.
- Host `19:36:41.890` `[Placement] asked <receiver> where item bandage (id 1181436987331) can go (query 1, slot 1, container 0).`
- Receiver `19:36:41.912` `[Placement] answered query 1 for item bandage (id 1181436987331): slot 1`
- Host `19:36:41.942` `[Take] <receiver> takes bandage (id 1181436987331) from <host> (remote-backpack allowConscious=True, container 0).`
- Receiver `19:36:41.991` `[PlayerInteraction] placed bandage (id 1181436987331) in local slot 1.`

**No placement outcome line appeared on either client.** Neither the receiver's `[Placement] item …: placed it at …
— reporting it to the host.` nor the host's `[Placement] item … is where this host wrote it (…): the placement
outcome matches, nothing moved.` was written after the placement, and no return followed: the committed give simply
sat until its 60 s window expired.

## Root cause (found by this run, fixed in the same commit)

The transfer event crossed the kernel wire without the two flags the give depends on. `WirePlayerInteraction` carried
the transfer's `Destination` (member 26) but had no `ExactDestination`/`Returned` members, so
`KernelPlayerInteractionWireMapper.FromWireInventoryTransfer` rebuilt the event with both false. On the receiver,
`PlayerInteractionApply.OnPlayerInventoryTransfer` therefore took its non-exact arm (`PlaceCarriedItem(…,
msg.ExactDestination=false)` — a fallback-capable placement) and its `if (msg.ExactDestination)` block never set the
outcome, so `SendPlacementOutcome` was never called. The existing
`PlacementAnswerTests.TheTransferFlags_SurviveTheKernelProjection` only ran `PlayerInteractionKernelCodec.
ToTransferMessage` on an in-memory event, i.e. after the wire, which is why the suite was green.

The fix adds `ExactDestination` (ProtoMember 28) and `Returned` (ProtoMember 29) to `WirePlayerInteraction`, maps both
in `ToWire`/`FromWireInventoryTransfer`, and pins them with two `KernelWireMapper` round trips plus a wire-level read
of the give and return flags off the serialized committed batches. Sync-coverage row P7 now names payload fields 28/29
with their own two anchors (14). The protocol number is untouched before the first release (decision 241; the change
is recorded by this commit, the ticket and the matrix).

## What this record does NOT claim

- Row 1 still has **no machine reading on the fixed artifact**: the re-read was not run. The user's instruction of
  2026-10-23 closed the round for tickets and code only, because the machine was needed for a co-op session.
- No match line and no return line were produced in this run; rows 2-3 were not staged.
- The adapter's outcome send is now covered by a wire-level test, not observed on a real client; the real-client
  observation is the re-read this record owes.

## What a next batch needs

Restart both clients on the new artifact and repeat exactly this gesture (the ring showed the item on the real client,
so the setup batch `20261023-d` could not reach is now known to work): release on the receiver's displayed `slot 1`;
the expected readings are the receiver's `[Placement] item 1181436987331: placed it at slot 1 — reporting it to the
host.` and the host's `[Placement] item 1181436987331 is where this host wrote it (slot 1): the placement outcome
matches, nothing moved.`

Local artifacts for this attempt (not committed): `.acceptance/20261023-e/` holds the deployment/preflight output, the
state dumps (`host-item.json`, `guest-place.json`, `host-ring-list.json`, `host-drag-start.json`,
`host-in-range2.json`, `host-on-slot1.json`, `host-screen*.json`), the red/green test logs, and the log excerpts quoted
above.
