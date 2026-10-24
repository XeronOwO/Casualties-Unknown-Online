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
| `save-mid-run-consistent-cut.md` | the final unified pass is the user's, but two rows wait on fixtures this station lacks: row 2's `worn` resting and row 3's opened lockables ("no drive path") |
| `save-system-mid-run-and-layer-end.md` | the umbrella over the save stages; it closes when S1-S4's tickets do, not on its own |
| `unified-remote-display-projection-rework.md` | every criterion is marked Met except the real dual-client visual pass, which the page itself calls the user's final unified acceptance |
| `unhooked-damage-block-callers.md` | row 2 needs a spider-burrow path this machine does not have |
| `pinyin-search-standalone-mod.md` | every row machine-read (78 cases, 0 failures); only the native search box's own look is left, and that is the user's |
| `recursive-crafting-mod.md` | only the panel's on-screen look is left; frames can read it with the mod installed — and that look is the user's visual pass, as its own status says |

## E — blocked by the ENVIRONMENT's input delivery (not by code, not by a missing fixture)

**Measured, not suspected:** on this station **no mouse BUTTON event reaches Unity's `Input`**, for either button — a right hold
and a left hold both read `mouseHeld=false, mouseDown=false` mid-hold — while the pointer MOVED through both holds, the bracket's
own gates were all open (`allowUseItem=true, conscious=true, minigame=false, paused=false`) and the bind was confirmed as Mouse1
(`keybind-read action=iteminteract` → `keyCode 324`). Cursor movement arrives; keys arrive (a real Tab press opened the inventory
ring and closed the tab-backpack ticket); buttons do not. This is UIPI-style blocking or a child-window/station condition, and it
is the one fact behind every row below. A cycle that fixes delivery unblocks them together.

**One correction this cycle's own runs force on that paragraph:** the remote medical VIEW is not part of this blocker — a probe
opens it (`remote-wound-special mode=open` → `opened=true, displayBodyName=MedicalDisplay_…` once the operator is alive). What the
buttons block is the TREATMENT GESTURE inside it, which is a drag of an item onto a limb. So the rows below fail one step later
than "cannot open the chart", and a future cycle should start from the open chart rather than re-testing the entry.

| Ticket | The row that needs the button |
|---|---|
| `cross-player-drag-use-feedback.md` | rows 1-7 are drags; the ring lists carried items on its own slots (`funguschunk` at `520,568`), so the page hypothesis is retired |
| `cross-player-drag-use-model-bounds.md` | the same drags; its detection shape landed |
| `host-entity-hit-red-flash-not-visible-on-guest.md` | the swing is the player's click; every attempt damaged a block or a pod fixture |
| `remote-medical-treatment-operations.md` | its live row is a treatment performed through the wound view |
| `concurrent-medical-operations.md` | row 7's second half: two operators START the same limb's operation through that view |
| `enter-opener-keybind-capture.md` | a key-bind row must be clicked in the game's settings UI before the Enter/Slash press means anything |
| `mod-cross-player-native-semantics.md` | its LOCAL native halves are already driven by the probes (`item-use` calls `Body.UseItem`, the same call a player's gesture makes), but every CROSS-player half needs the remote view's own entry — the medical chart or a drag — so it waits with the rows above |
| `suppressed-native-call-sounds-stay-unheard.md` | rows 1-4b are the remote-medical and minigame setups, i.e. the same chart entry |
| `sync-player-pain-vocalizations-and-bark.md` | rows 1-3 need a lockable entity and a jammed lockpick driven through the game's own interaction |
| `trade-domain-dual-side-runtime.md` | rows 8 (recruit) and 9 (hostile swing): the recruit runs through the in-game dialogue and the swing through the aim the right button drives; `trade-drive` covers the purchase/give halves only |
| `unhooked-item-and-body-sound-families.md` | rows 1-2 and the nap/shake half of row 6 are limb treatments through the chart |
| `remote-medical-stage-2-shrapnel-multiplayer.md` | row 7: the CUO session runs with NO native minigame on either client here (`minigame-end` read `before=none` right after `started=true`), so the piece-breaking path has nothing to act on — and the ticket then needs a design call between a state that creates the native minigame and a session-level piece removal |
| `guest-remote-pose-head-orientation-desync.md` | the clone's type is NAMED (`Character_…/Body` carries `isRight` and `targetLookPos`, equal to the guest's own body while idle), so what is left is a NON-ZERO facing: the game writes a look target only while AIMING, the right-button hold this station cannot deliver |

## C — needs a capability an agent could still build

| Ticket | What it waits on |
|---|---|
## N — measured to the end: not stageable through the game's own calls

| Ticket | Why it cannot be staged |
|---|---|
| `nested-container-clone-proxy-leaks-as-world-item.md` | the container machinery works (`bandage` into a `trashbag` → `loaded=true`, contents 0→1) but loading a bag into a bag is refused by the game's own guard (`canHold=true, distance=0`), which is exactly the refused-call precondition the fix defends — the nested shape cannot exist through production calls |
| `runtime-entity-markerless-bind-absorption.md` | row 9 needs a world that CARRIES a runtime creation: `runtime-entity-tables` reads `accepted=0, animals=0, pending=0` in two separate sessions, and a real explosion added none, so there is no backfill copy to stamp |
| `guest-background-ghost-item-ground-sounds.md` | measured to the end as an EAR question: the capture probes (`pant-sound`, `generation-audio-probe`) can record what a client plays, but the ticket's claim is what a backgrounded guest HEARS, and no instrument here turns a recording into that judgement |

## E — the medical stage tickets, whose rows are all live two-client scenarios

| Ticket | The row that needs the remote view |
|---|---|
| `remote-medical-stage-1-injection-session.md` | all eight rows are an operator injecting a patient through the chart's own action |
| `remote-medical-stage-2-shrapnel-multiplayer.md` | rows 1-9 (row 10 is read from the path; row 7 additionally needs a native minigame this state does not create) |
| `remote-medical-stage-3-other-actions.md` | every action row, same entrance |
| `remote-medical-native-minigame-parity.md` | the umbrella over the three stages above |
| `topical-limb-action-shape.md` | the parity rows need an item used on the remote chart; its classifier half landed |

## How this list is kept honest

A row leaves this page only through the ticket's own status moving to `done/`, and every claim above names the instrument or the
missing fixture rather than a mood. The classes are not priorities: they say WHO is blocked, not what to do first.

## The state the page reaches (2026-10-25, end of the cycle)

Every remaining ticket is in one of two conditions, and both are outside what this station can do alone:

- **U — the user's own judgement.** A decision (which of two behaviours is intended), a look at a screen this repository cannot
  assert (`PlayerCamera`-driven UI, a mod's panel, the native search box) or a save/restore pass the user runs on purpose.
- **E — the station's input delivery, plus one design choice.** No mouse BUTTON reaches Unity here (measured for both buttons);
  the remote view's own entry is a click, so the drag, the chart-driven treatments, the concurrent-medical start, the key-bind
  capture and the facing comparison all wait on that one fact. The shrapnel session's row 7 additionally needs a decision between
  two designs (a state that creates the native minigame, or a session-level piece removal) — that is the user's, not the agent's.

The C class is empty of agent work: each of its rows was measured to the end — the nested container cannot be constructed through
the game's own calls, the markerless row needs a world that carries a runtime creation (none in two sessions, and an explosion adds
none), the destruction drops were produced and read, the recruit and hostile rows need the in-game dialogue and a swing, and the
sound-family rows need the remote chart. Nothing on this page is waiting for a probe that does not exist; the probes were written.
