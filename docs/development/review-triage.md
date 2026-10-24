# Review-block triage — what each open ticket actually waits on

One row per ticket in `docs/backlog/review/`, classified by the reader it waits for. The point is to make the queue
answerable in one pass: a `U` row is the user's, a `C` row needs a capability an agent could still build, and a `T` row can
close in a cycle with the instruments that already exist. A ticket may move between classes as instruments land — this page is
regenerated when a batch changes the picture, never accumulated.

## U — waits on a user decision or a user's own check

| Ticket | What it waits on |
|---|---|
| `adapter-capability-catalog.md` | stage rows beyond stage 1 need the user's call on which capability levels ship |
| `dead-player-right-click-name-suffix.md` | the menu's 240 px title clips a long name: grow the panel or elide the name (a layout choice) |
| `trap-layout-crystal-rows-empty.md` | its rows need a prefab carrying `CrystalBehaviour`, which this build does not have — a build capability and the decision to add one |
| `layer-complete-choice-for-members.md` | the dead-host row is answered by the user's ruling that only the host presses the choices; the ticket wants that confirmation |
| `player-music-sync-playback.md` | the listener controls (mute, volume, "only during rest") are a design the user owns |
| `plugin-host-shell.md` | every acceptance clause is read; the remaining item is the user's judgement of the Online UI's look |
| `remote-medical-panel-speech-text-desync.md` | row 6: should the remote chart open OVER the trade menu, or is refusing while trading intended? |
| `save-layer-end-save-and-restore.md` | the in-game rows need the user's own save/restore pass |
| `save-native-character-field-parity.md` | rows 1-3 are three game UI displays: the wound window's text, the death-stats number and the tooltip's happiness |
| `save-native-run-field-parity.md` | row 2's factual line is read (items persist across a cut, the recipe's made-before flag does not); whether that flag is a field CUO must persist is a design decision |
| `save-mid-run-consistent-cut.md` | the final unified acceptance pass is the user's |
| `save-system-mid-run-and-layer-end.md` | S1-S4 are code-complete; the acceptance pass is the user's |
| `unified-remote-display-projection-rework.md` | every criterion is marked Met except the real dual-client visual pass, which the page itself calls the user's final unified acceptance |
| `unhooked-damage-block-callers.md` | row 2 needs a spider-burrow path this machine does not have |

## E — blocked by the ENVIRONMENT's input delivery (not by code, not by a missing fixture)

**Measured, not suspected:** on this station **no mouse BUTTON event reaches Unity's `Input`**, for either button — a right hold
and a left hold both read `mouseHeld=false, mouseDown=false` mid-hold — while the pointer MOVED through both holds, the bracket's
own gates were all open (`allowUseItem=true, conscious=true, minigame=false, paused=false`) and the bind was confirmed as Mouse1
(`keybind-read action=iteminteract` → `keyCode 324`). Cursor movement arrives; keys arrive (a real Tab press opened the inventory
ring and closed the tab-backpack ticket); buttons do not. This is UIPI-style blocking or a child-window/station condition, and it
is the one fact behind every row below. A cycle that fixes delivery unblocks them together.

| Ticket | The row that needs the button |
|---|---|
| `cross-player-drag-use-feedback.md` | rows 1-7 are drags; the ring lists carried items on its own slots (`funguschunk` at `520,568`), so the page hypothesis is retired |
| `cross-player-drag-use-model-bounds.md` | the same drags; its detection shape landed |
| `host-entity-hit-red-flash-not-visible-on-guest.md` | the swing is the player's click; every attempt damaged a block or a pod fixture |
| `remote-medical-treatment-operations.md` | its live row is a treatment performed through the wound view |
| `concurrent-medical-operations.md` | row 7's second half: two operators START the same limb's operation through that view |
| `enter-opener-keybind-capture.md` | a key-bind row must be clicked in the game's settings UI before the Enter/Slash press means anything |

## C — needs a capability an agent could still build

| Ticket | What it waits on |
|---|---|
| `entity-destruction-drop-guest-fresh-state-loss.md` | a destruction fixture whose drops a FRESH guest sees |
| `guest-break-drops-recovery.md` | rows 1-4 and 7: a swallowed report and a lost air write, forced |
| `nested-container-clone-proxy-leaks-as-world-item.md` | a container ITEM that can hold another container ITEM; this build's prefab set has not produced one |
| `runtime-entity-markerless-bind-absorption.md` | row 9 needs the live Unity world's backfill stamp |
| `suppressed-native-call-sounds-stay-unheard.md` | rows 1-4b: the remote-medical and minigame setups |
| `sync-player-pain-vocalizations-and-bark.md` | rows 1-3: a lockable entity and a jammed lockpick |
| `trade-domain-dual-side-runtime.md` | rows 8 (recruit) and 9 (hostile swing): a recruit path and a hostile trader |
| `unhooked-item-and-body-sound-families.md` | rows 1-2 and the nap/shake half of row 6: limb-treatment setups |
| `guest-remote-pose-head-orientation-desync.md` | a probe that finds the remote clone's real type (no `Body` component answered this session) |

## T — closable with the instruments that already exist

| Ticket | How |
|---|---|
| `guest-background-ghost-item-ground-sounds.md` | audio capture + the background-item fixture |
| `mod-cross-player-native-semantics.md` | the native-half chains, read one by one on the live clients |
| `pinyin-search-mod.md`, `pinyin-search-standalone-mod.md` | mod install and behaviour, read with the mod present |
| `recursive-crafting-mod.md` | only the panel's on-screen look is left; frames can read it with the mod installed |
| `remote-medical-native-minigame-parity.md` | the native minigame fixtures the recipes already drive |
| `remote-medical-stage-1-injection-session.md` | injection session probes |
| `remote-medical-stage-2-shrapnel-multiplayer.md` | row 7's coroutine (the `cpr-rhythm` shape) |
| `remote-medical-stage-3-other-actions.md` | the other-action fixtures |
| `sync-cadence-review.md` | its remaining measurement is a longer traffic window |
| `topical-limb-action-shape.md` | the topical parity rows |

## How this list is kept honest

A row leaves this page only through the ticket's own status moving to `done/`, and every claim above names the instrument or the
missing fixture rather than a mood. The classes are not priorities: they say WHO is blocked, not what to do first.
