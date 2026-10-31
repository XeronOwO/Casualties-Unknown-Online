# Review-block triage — what each open ticket actually waits on

One row per ticket in `docs/backlog/review/`, classified by what has to happen next. The point is to make the queue answerable in one
pass: a `U` row is the user's, an `E` row needs one more live reading with instruments that exist, and an `N` row cannot be staged on
this build at all. A ticket moves between classes as instruments land — this page is regenerated when a batch changes the picture,
never accumulated.

## Closed by the sweep (2026-10-31 → 2026-11-01)

Ten tickets left `review/` with their rows judged and their evidence written: `a-command-owns-its-usage-line`,
`music-and-recursive-crafting-mods-fail-to-load`, `player-facing-text-localized-per-receiver`, `item-custody-single-authority`,
`context-menu-name-wraps-behind-actions`, `split-the-oversized-classes`, `host-blast-rolls-a-second-drop-set`,
`selectors-exclude-the-executor`, `a-disconnected-member-leaves-his-arrow-on-the-hud`, `completion-list-keys-and-ids-disagree`. Two of
them needed a code change in the same commit: the mod adapter's gate classification (row 5 of the console contract) and the console's
hover preview (row 8 of the completion ticket, which finally drew once the tooltip outlived the event that reports it).

## U — waits on a user decision, a user's own pass, or a user's own look

| Ticket | What it waits on |
|---|---|
| `adapter-capability-catalog.md` | stage rows beyond stage 1 need the user's call on which capability levels ship |
| `dead-player-right-click-name-suffix.md` | the row lands for a short name; the long-name case needs the panel-width decision (grow the panel or elide the name) |
| `layer-complete-choice-for-members.md` | its premise is superseded by the ruling that only the host presses the choices; it wants the confirmation written |
| `player-music-sync-playback.md` | built and read; the listener controls (mute, volume, "only during rest") are a design the user owns |
| `plugin-host-shell.md` | every acceptance clause is read; what is left is the user's judgement of the Online UI's look |
| `pinyin-search-standalone-mod.md` | every row machine-read; the native search box's own look is the user's |
| `recursive-crafting-mod.md` | the resolver, the game half, the command and the panel's states are read; the panel's look is the user's |
| `save-layer-end-save-and-restore.md` | the in-game rows are the user's own save/restore pass |
| `save-native-character-field-parity.md` | three game UI displays (wound window text, death-stats number, tooltip happiness) are the user's to read |
| `save-native-run-field-parity.md` | rows 1 and 2 need a continue at a layer with non-default settings and a crafted recipe; row 3 asks whether the made-before flag is a field CUO must persist |
| `save-mid-run-consistent-cut.md` | the final unified pass is the user's; two rows also wait on fixtures this station lacks |
| `save-system-mid-run-and-layer-end.md` | the umbrella over the save stages; it closes when S1-S4's tickets do |
| `unified-remote-display-projection-rework.md` | every criterion is Met except the real dual-client visual pass, which the page itself calls the user's |
| `concurrent-medical-operations.md` | rows 1-6 are pinned by the 36-case concurrency suite; row 7 is the user's dual-client acceptance |
| `trade-domain-dual-side-runtime.md` | rows 1-7 pass; rows 8 (recruit) and 9 (hostile swing) need the in-game dialogue and an aim-driven swing |
| `the-command-console-is-a-contract.md` | rows 5, 10 and 11 are judged and the rest is complete; rows 6 (a Chinese member's keyed answer) and 8 (a mod's selector frame) need a Chinese member client and an example-mod selector parameter |
| `remote-medical-panel-speech-text-desync.md` | row 6 asks whether the remote chart should open OVER the trade menu or is meant to refuse while trading |

## E — one more live reading with instruments that already exist

| Ticket | The reading it waits on |
|---|---|
| `cross-player-drag-use-feedback.md` | the give gesture's release/refusal family. The own-UI gate is ruled out and the ring opens again with focus-first; the next batch needs a two-phase right press (down, close the ring, move, up), a small addition to the input helper |
| `cross-player-drag-use-model-bounds.md` | the same drags; its detection shape landed |
| `cross-player-eat-skips-native-steps.md` | the eat path through a real dragged release onto a teammate |
| `host-entity-hit-red-flash-not-visible-on-guest.md` | a swing that damages a teammate rather than a block or a pod |
| `enter-opener-keybind-capture.md` | a key-bind row clicked in the game's own settings UI before the Enter/Slash press means anything |
| `mod-cross-player-native-semantics.md` | its local halves are driven by the probes; every CROSS half is the drag/medical entry |
| `suppressed-native-call-sounds-stay-unheard.md` | rows 1-4b are the remote-medical and minigame setups |
| `unhooked-item-and-body-sound-families.md` | rows 1-2 and the nap/shake half of row 6 are limb treatments through the chart |
| `remote-medical-stage-1-injection-session.md` | all eight rows are an operator injecting a patient through the chart's own action |
| `remote-medical-stage-2-shrapnel-multiplayer.md` | rows 1-9; row 7 additionally needs a native minigame this state does not create |
| `remote-medical-stage-3-other-actions.md` | every action row, same entrance |
| `remote-medical-native-minigame-parity.md` | the umbrella over the three stages above |
| `remote-medical-treatment-operations.md` | its live row is a treatment performed through the wound view |
| `topical-limb-action-shape.md` | the remote chart opens in-process (`medical-open`) and a release lands on it (`remote-medical-release`, item consumed, view stays open); the owed row now needs a WOUNDED patient as its fixture — a healthy limb answered `calls=none` and nothing to start |
| `use-on-sleeping-target-refused.md` | rows 1-2 read the eating family; row 3's treatment families reach the patient through the wound view |
| `sync-player-pain-vocalizations-and-bark.md` | rows 1-3 need a lockable entity and a jammed lockpick driven through the game's own interaction |
| `guest-remote-pose-head-orientation-desync.md` | the clone's type is named; the facing pair needs one aim-drive read |
| `a-limb-component-applied-outside-a-world-throws-every-frame.md` | the codec guard landed (inert until the view exists) and rows 1/3 read clean on the fixed build; row 2 owes the bleeding/icon reading and row 4 one reading per component kind |
| `console-enter-on-empty-does-not-close.md` | rows 1, 2 and 4 are judged; row 3's key toggle is an IMGUI event the virtual keyboard cannot stage (measured: `keyQueries=0`) |
| `give-degrades-to-a-ground-drop-and-rearranging-retires-it.md` | rows 1, 3 and 4 pass; row 2's target-screen line needs a real refusal on the receiving side |
| `launcher-fails-when-the-alt-box-cannot-start.md` | rows 1-4 judged except the reboot path, which needs a machine restart |
| `the-pre-run-lobby-can-deadlock.md` | the deadlock was hit once; the row needs the same shape reproduced |


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
