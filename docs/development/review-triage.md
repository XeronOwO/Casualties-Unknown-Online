| `console-enter-on-empty-does-not-close.md` | rows 1, 2 and 4 are judged; row 3's key toggle is an IMGUI event the virtual keyboard cannot stage (measured: `keyQueries=0`) |
| `give-degrades-to-a-ground-drop-and-rearranging-retires-it.md` | rows 1, 3 and 4 pass; row 2's target-screen line needs a real refusal on the receiving side |
| `launcher-fails-when-the-alt-box-cannot-start.md` | rows 1-4 judged except the reboot path, which needs a machine restart |
| `the-pre-run-lobby-can-deadlock.md` | **reproduced 2026-11-01**: a member that joins a running session waits forever (its log has no `World join received`) while both gates blame the other side, and the re-entry ask that would cure it is armed only on a host world-exit — the ticket now names the join-armed ask as the fix |


## N — measured to the end: not stageable on this build

| Ticket | Why it cannot be staged |
|---|---|
| `trap-layout-crystal-rows-empty.md` | no prefab carries `CrystalBehaviour` on this build, so no scan can produce a crystal row |
| `nested-container-clone-proxy-leaks-as-world-item.md` | the shape (a container holding a container with contents) does not occur in a generated world: 269 items and 6 containers scanned, none nested-with-contents |
| `runtime-entity-markerless-bind-absorption.md` | row 9 needs a world that CARRIES a runtime creation (`accepted=0, animals=0, pending=0` in two sessions) |
| `unhooked-damage-block-callers.md` | row 2 needs a spider-burrow path this machine does not have |
| `guest-background-ghost-item-ground-sounds.md` | measured to the end as an EAR question: the capture probes record what a client plays, but the judgement is the user's ear |

## How this list is kept honest

A row leaves this page only through the ticket's own status moving to `done/`, and every claim above names the instrument, the code or the
missing fixture rather than a mood. The classes are not priorities: they say who is blocked, not what to do first.

## Where the instruments stand (2026-11-01)

- **Virtual pointer and keyboard** (`VirtualInput` plus the `virtual-input` / `virtual-key` recipes): the pointer patches
  `Input.mousePosition` and the three button methods, and a `pointer-truth` recipe reads back both the staged point and every remote's
  streamed position with its screen point. The keyboard reaches `Input.GetKey*` only — an IMGUI overlay reads `Event`, so a console key
  press cannot be staged (measured: `keyQueries=0`), and the game's own while-dragging loop does not run the bracket for a
  `drag-from-slot` arm even with the virtual button held.
- **OS input** (`send-input.ps1`, authorised for this work): a key press reaches the game when the target window is focused FIRST and the
  press follows about a second later — that is how the ring opened this sweep (`radialOpen=true, count=6`, its six slots with their
  items). A one-call drag helper exists; the give gesture needs a two-phase press, close, move, release instead.
- **Frames** (`capture-frame`): the sandboxed clients write to their own sandbox's temp directory, so a guest's frame is read from there.
- **The console's own surfaces**: `console-ui` types a line and reports the live suggestions; the hover preview now draws after the tooltip
  fix, which is what closed the completion ticket.

