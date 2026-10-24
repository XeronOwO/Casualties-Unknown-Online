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
| `unhooked-damage-block-callers.md` | row 2 needs a spider-burrow path this machine does not have |

## C — needs a capability an agent could still build

| Ticket | What it waits on |
|---|---|
| `cross-player-drag-use-feedback.md` | a right-press that enters the drag bracket on the INVENTORY page (this session pressed three points on the equipment page and `dragging` stayed false) |
| `cross-player-drag-use-model-bounds.md` | the same drag bracket; its detection shape landed |
| `concurrent-medical-operations.md` | row 7's second half: two operators START the same limb's operation and the progress is read on the victim's client |
| `enter-opener-keybind-capture.md` | a key-bind row waiting in the game's own settings UI, then a real Enter/Slash press |
| `entity-destruction-drop-guest-fresh-state-loss.md` | a destruction fixture whose drops a FRESH guest sees |
| `guest-break-drops-recovery.md` | rows 1-4 and 7: a swallowed report and a lost air write, forced |
| `host-entity-hit-red-flash-not-visible-on-guest.md` | a melee hit that provably damages a `BuildingEntity` (this session's clicks landed on pod fixtures and blocks) |
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
| `remote-medical-treatment-operations.md` | the treatment drives the probes already carry |
| `sync-cadence-review.md` | its remaining measurement is a longer traffic window |
| `topical-limb-action-shape.md` | the topical parity rows |
| `treatment-gore-presentation-carried.md` | the audio capture probes (`pant-sound`, `generation-audio-probe`) plus the treatment fixture |
| `unified-remote-display-projection-rework.md` | the projection rows against the live display |

## How this list is kept honest

A row leaves this page only through the ticket's own status moving to `done/`, and every claim above names the instrument or the
missing fixture rather than a mood. The classes are not priorities: they say WHO is blocked, not what to do first.
