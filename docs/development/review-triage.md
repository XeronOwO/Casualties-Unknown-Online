# Review-block triage — what each open ticket actually waits on

One row per ticket in `docs/backlog/review/`, classified by what has to happen next. The point is to make the queue answerable in one
pass: a `U` row is the user's, an `E` row needs one more live reading with instruments that exist, and an `N` row cannot be staged on
this build at all. A ticket moves between classes as instruments land — this page is regenerated when a batch changes the picture,
never accumulated.

## Closed by the medical and join sweeps (2026-11-01)

Fifteen tickets left `review/` with their rows judged and their evidence written: `a-command-owns-its-usage-line`,
`music-and-recursive-crafting-mods-fail-to-load`, `player-facing-text-localized-per-receiver`, `item-custody-single-authority`,
`context-menu-name-wraps-behind-actions`, `split-the-oversized-classes`, `host-blast-rolls-a-second-drop-set`,
`selectors-exclude-the-executor`, `a-disconnected-member-leaves-his-arrow-on-the-hud`, `completion-list-keys-and-ids-disagree`, `no-compatibility-branches-before-the-first-release`, `a-limb-component-applied-outside-a-world-throws-every-frame`, `console-enter-on-empty-does-not-close`, `escape-closing-the-console-also-opens-the-pause-menu`. Two of
them needed a code change in the same commit: the mod adapter's gate classification (row 5 of the console contract) and the console's
hover preview (row 8 of the completion ticket, which finally drew once the tooltip outlived the event that reports it).

## Closed since the first sweep (2026-11-01, after the sandbox came back)

- `the-pre-run-lobby-can-deadlock` — three breaks fixed (the member never asked, the host refused for want of a baseline it could not have, and a join whose menu never loaded the pre-run script held its phase forever) and the chain read end to end: a restarted member asks `1/12`, the host invites, the member enters, both gates clear.
- `remote-medical-treatment-operations` — a bandage released through the remote chart now runs the game's own minigame, its wraps reach the host (`terminal Completed: progress 0.481`), and the patient's own client applies the effect. Two product defects fell out of it (a void release prefix let the native local apply run behind CUO's remote handle; a stale start rejection cleared the live session) and two more were recorded with their evidence (the end request carried zero progress for a completed bandage; a minigame's hand cannot be driven by writing its fields).

The instruments those two cycles produced are reusable and are named here so the next batch does not rebuild them: `wound-seed` (the game's own `Limb.ImpactDamage`), `minigame-live` (what the game itself sees of the minigame's hand and progress), `bandage-ring`/`bandage-ring2` (an in-client pointer driver), `virtual-input`'s `press-key`/`release-key`, and the printer rule that a give before the world is live is refused while a give into full slots lands in the carried container.

## Medical family — what each remaining row now needs (2026-11-01)

| Ticket | What it waits on |
|---|---|
| `topical-limb-action-shape.md` | its saline half reads as its design decides (refused by name, no minigame); the syringe half needs a syringe FILLED with a liquid — an empty one does nothing — which is a container-fill fixture, not an instrument |
| `remote-medical-cpr.md` | needs the CPR control on a downed patient: `cpr-rhythm` already drives the presses, and the missing piece is the downed fixture (`cpr-rhythm` was read working on 2026-10-28) |
| the other `remote-medical-*` rows | the wound-view release path itself is proven (see the closed ticket); each now needs its own item/liquid fixture, and the give-line rule above decides whether a batch can even start |
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
| `topical-limb-action-shape.md` | the remote chart opens in-process (`medical-open`) and a release lands on it (`remote-medical-release`, item consumed, view stays open); the owed row now needs a WOUNDED patient as its fixture — a healthy limb answered `calls=none` and nothing to start |
| `use-on-sleeping-target-refused.md` | rows 1-2 read the eating family; row 3's treatment families reach the patient through the wound view |
| `sync-player-pain-vocalizations-and-bark.md` | rows 1-3 need a lockable entity and a jammed lockpick driven through the game's own interaction |
| `guest-remote-pose-head-orientation-desync.md` | the clone's type is named; the facing pair needs one aim-drive read |
| `give-degrades-to-a-ground-drop-and-rearranging-retires-it.md` | rows 1, 3 and 4 pass; row 2's target-screen line needs a real refusal on the receiving side |
| `launcher-fails-when-the-alt-box-cannot-start.md` | rows 1-4 judged except the reboot path, which needs a machine restart |
| `the-pre-run-lobby-can-deadlock.md` | **fixed 2026-11-01** (three breaks: the member never asked, the host refused without a baseline, the member could not accept the instruction) and read end to end — a restarted member asks `1/12`, the host invites, the member enters, both gates clear. Rows 1 and 2 keep their exact sub-cases owed (a re-join BEFORE the run starts; a drop while the gate waits) |


## The drag family needs one human motion (2026-11-01)

The four drag tickets (`cross-player-drag-use-feedback`, `cross-player-drag-use-model-bounds`, `cross-player-eat-skips-native-steps`, and the cross halves of `mod-cross-player-native-semantics`) share one condition a batch cannot satisfy: the give cue and its displayed backpack are ONE question — what is under the pointer — and the pack is drawn only once that question is answered. Every instrument around it exists and reads its own state honestly (the arm from a slot, the view's `IsOpen`/`FocusedSteamId`, `drag-cue`, the staged pointer), and the batches ruled out the window, the range and the arm. What is left is the motion the game itself needs: drag a bandage onto a teammate standing beside you. Until a player does that, those rows are the user's.

## What the working medical chain already covers (2026-11-01)

The remote chart is proven end to end for a BANDAGE (release, native minigame, host-arbitrated progress, `terminal Completed`, the patient's own client applying it). For the stage tickets that is one action in one role: `remote-medical-stage-3-other-actions` still needs its action × role matrix, `remote-medical-stage-1-injection-session` needs the injection action (a syringe holding a liquid), `remote-medical-stage-2-shrapnel-multiplayer` needs the shrapnel minigame, and `remote-medical-native-minigame-parity` is the umbrella over them.
## N — measured to the end: not stageable on this build

| Ticket | Why it cannot be staged |
|---|---|
| `trap-layout-crystal-rows-empty.md` | no prefab carries `CrystalBehaviour` on this build, so no scan can produce a crystal row |
| `nested-container-clone-proxy-leaks-as-world-item.md` | the shape (a container holding a container with contents) does not occur in a generated world: 269 items and 6 containers scanned, none nested-with-contents |
| `runtime-entity-markerless-bind-absorption.md` | row 9 needs a world that CARRIES a runtime creation (`accepted=0, animals=0, pending=0` in two sessions) |
| `unhooked-damage-block-callers.md` | row 2 needs a spider-burrow path this machine does not have |
| `guest-background-ghost-item-ground-sounds.md` | measured to the end as an EAR question: the capture probes record what a client plays, but the judgement is the user's ear |

## The one fixture the medical stage tickets share (2026-11-01)

Every remaining medical stage row needs a container holding a LIQUID — the injection rows need a syringe with something in it, and the topical row needs a limb-action container refilled with a health-usable liquid. This build offers no batch path to that state: the console carries no fill or liquid argument, and the item catalog has no prefilled syringe variant the batch could spawn. The readings that bound it are on `topical-limb-action-shape` (a health-usable liquid in a limb-action container refuses by name, plain water refuses by name, an empty syringe does nothing), and the honest route to the fixture is the game's own water-fill interaction, which a player performs — the same class of gap as the drag family's one human motion.
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

## What is left after the night (2026-11-01, 03:00)

Forty-two tickets remain and every one of them now needs something only the user can supply. Two kinds:

- **The sandbox service.** `SbieSvc` is Stopped and an unelevated `Start-Service` is refused ("cannot open the service"), so no boxed client can start: the launcher's own `Ensure-SandboxService` would raise a UAC prompt, and nobody is at the keyboard to approve it. Every two-client row — the drag family, the medical family, the join/re-entry reproduction, the trade fixtures — waits on that one approval. Starting the service (or rebooting, after which the launcher's elevated start runs) unblocks the `E` section in one step.
- **A decision, a look, or a pass of the user's own.** The `U` section: the panel-width choice on the dead-player menu, the listener controls for the music, the three mod panels' look, the save/restore passes, and the two open design questions (whether the remote chart opens over the trade menu, and whether the console should open while the game's own settings menu is up — a question the 03:00 batch's frames raised).

Nothing else is left: the single-client rows that remained were read tonight (the console key, the escape leak, the limb-component storm and its icon, the key-bind capture's reachability), and the key-bind ticket now carries the exact instrument it still needs rather than a hope.
