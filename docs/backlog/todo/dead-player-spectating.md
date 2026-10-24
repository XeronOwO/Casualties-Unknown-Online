# A player can watch the run as a spectator, and possess a teammate to see through their eyes

- Status: Todo — the user's 2026-10-24 design (below) supersedes the death-screen entry and the `/spectate`
  command this ticket was built on; the camera half and the three host switches stay as its foundation, and the
  parts the new design replaces are named in the pivot section.
- Priority: Medium
- Category: Player interaction / presentation / KrokMP parity
- Source: the user's 2026-10-13 backlog request, in their own words: "操控的角色死亡后，能观战他人，参考
  KrokMP 的功能" — after the character you control dies, you should be able to spectate the others, with
  KrokMP's feature as the reference. **Refined by the user on 2026-10-24** (verbatim, Chinese): "观战应当把所有的
  与玩家状态游玩相关的内容，例如屏幕特效、声音特效、生命体征危机时的心跳、正常游玩的快捷键等待这些所有的都屏蔽掉，
  纯观察。还有你的命令设计我觉得应当参考 Minecraft，用 /gamemode survivor|spectator 这样，然后附身是复用对指定
  玩家的右键菜单，里面加个观战的按钮（无距离、可达性校验，仅观察者模式校验），最好能够继承观战玩家的所有内容，例如
  能只读查看背包、查看医疗面板，底部有对应玩家的状态图标、玩家的屏幕特效音效等等，就像自己在玩一样，亲身经历"。

## The 2026-10-24 pivot (user's design, binding)

The entry is a MODE, not a death-screen action, and watching is not merely a camera:

1. **`/gamemode survivor|spectator`**, shaped like Minecraft's `/gamemode` (the console-command rule: fetch the
   Minecraft syntax rather than remember it). It replaces `/spectate <selector>` and `/spectate off` as the way
   in and out of watching; free-fly stays reachable (see question 3).
2. **Pure observation**: while spectating, everything tied to PLAYING your own character is suppressed — screen
   effects, sound effects, the vital-crisis heartbeat, the normal-play hotkeys and any other player-state
   presentation. A spectator watches; nothing of their own body reaches them.
3. **Possession reuses the right-click context menu** on a specific player: a "watch" entry on that menu, with
   **no distance or reachability check and only the spectator-mode check** as its gate.
4. **Possession inherits the watched player's whole experience**: read-only access to their backpack, their
   medical panel, their state icons along the bottom, their screen effects and sound effects — "like playing it
   yourself, experiencing it firsthand", not a detached camera.

### What this replaces, and what stays

- Replaced: the death screen as THE entry (a dead player is a spectator, but the mode is not tied to death), the
  `/spectate` verb as the entry, and the free camera as the only "beyond following" capability.
- Stays: the native follow target (`PlayerCamera.following`), the free camera (`isFreecam`) and the three host
  rules' existing plumbing — their defaults and whether they still bind the new mode is question 3 below.
- New surface the design needs: the mode itself (local and session-visible), the suppression layer for player-state
  presentation, the context-menu entry, and the borrowed UI/state projection for a possessed target.

### Open questions this ticket carries to the user before the first slice

1. **The body while spectating**: does the spectator's own character stay in the world (invisible/ghost, like a
   Minecraft spectator) or does it leave the simulation (as a dead player's body does today)?
2. **Ownership of the view**: when possessing, does the watcher see the target's HUD INSTEAD of their own (a full
   mirror, as "就像自己在玩一样" suggests) or in addition to it? And is the target told they are being watched?
3. **The three host switches** (`SpectatorFreeFly`, `SpectateWhileUnconscious`, `SpectateWhileWaitingToJoin`):
   keep them as the session's rules over the new mode, or drop them because `/gamemode` makes the mode a choice
   rather than a death state?

### Answered by the user, 2026-10-24

1. **The body stays in the world, in place, and can still be attacked and injured** — a spectator's own character
   remains a real body while they watch, not a ghost and not an exited simulation. Being wounded (or killed) while
   spectating is therefore possible and must not be hidden by the mode.
2. **Possession replaces the watcher's UI**: only the watched player's interface is shown — their backpack
   (read-only), their medical panel, their state icons, their screen effects and their sounds. The watcher's own
   HUD is hidden, exactly as "就像自己在玩一样" asks. **The watched player is told** ("the recommended notice").
3. **`/gamemode` is a CHEAT command, gated only by the cheat permission** — no host rules over it. Spectator mode
   has exactly one other entrance: **the death screen's "back to main menu" button is replaced by a "spectator
   mode" button**, so a dead player may watch instead of leaving; and **when that player is revived they return to
   normal play immediately**. The three old host switches (`SpectatorFreeFly`, `SpectateWhileUnconscious`,
   `SpectateWhileWaitingToJoin`) are therefore superseded by this permission gate and the death-screen button; free
   flight stays a capability of the mode itself rather than a host rule (how it is toggled is the first slice's
   detail).

3. **The host switches** are still being decided: the user asked what
   `SpectateWhileUnconscious` and `SpectateWhileWaitingToJoin` mean, so the answer records them in plain words
   before the choice (see the question history in the ticket's status line).

## The implementation plan (2026-10-24, after the design ruling)

Four slices, each with its own commit and its own reading. The entry mapping comes first because every slice
crosses players or mirrors a native action.

### Entry mapping

| CUO entry | Native call site / surface | Input sequence |
|---|---|---|
| `/gamemode survivor` / `/gamemode spectator` | the console's own cheat family (`IMemberCommandRules`: the host's `CheatVerbsOpenToMembers` switch plus `AllowsCheatVerb`, i.e. `/op`); the mode itself is local presentation, so nothing crosses the wire | type the verb in the console, Enter |
| the death screen's "spectator mode" button (replaces "back to main menu") | `PlayerCamera.HandleDeathScreen` and the `endScreen` sprite/button it shows; the button's own handler is the native menu-return call | the death screen is modal and the button is a click, exactly as the screen's current button already is |
| the right-click context menu's "watch" entry on a member | the menu's own builder (`OnlineUiMemberListDrawer`'s card / the right-click menu), reusing the entry the member rows already carry | right-click the member (its menu opens), click the entry; no distance check by ruling |
| possession's inherited UI | the local HUD's own readers: the backpack window, `RemoteMedicalPanel` (already displays another member's body), the bottom state icons, the presentation families that render the possessed body's effects/sounds | none: the mode switch drives it |

### Slice 1 — the mode and its two entrances

- `ISpectatorControl` grows the mode itself (`IsSpectator`, `EnterSpectatorMode()`/`ExitSpectatorMode()`); the
  current `TryWatch` gating (dead only) moves to possession, and `WatchingSteamId`/`TryWatch` become the
  possession half.
- `GamemodeCommands` (a new command group beside `SpectatorCommands`) exposes `gamemode survivor|spectator`,
  declaring the cheat permission the console already enforces for `/give` and `/op` — the declaration mechanism
  is the first code question (`ConsoleCommandAttribute`'s permission today is `Anyone`/`HostOnly`; the cheat
  family is gated in the service by `IMemberCommandRules`, so the verb must be declared the way the cheat family
  is, and the refusal must name its ground).
- The death screen's receipt: **the exploration receipt (结算单) carries its own text button that returns to the
  main menu, and THAT button becomes the spectator-mode entry** (the user's correction of 2026-10-24; the pause
  menu's give-up button is NOT it). The receipt is `PlayerCamera.EndSequence(0)` — it activates `endScreen`, fills
  `deathStats` children 0–13, and its own Button only rips the receipt off; the menu-return button is a separate
  scene-wired control on that screen (no decompiled call site: the scene's UnityEvent names
  `PlayerCamera.ToMainMenu` or `WorldGeneration.SaveAndExit`, which is why a code grep never shows it). The
  change is therefore RUNTIME, not a Harmony prefix: when `didDeathScreen` holds and `endScreen` is active, walk
  the screen's Buttons, find the one whose persistent event targets the menu return, relabel it to the
  spectator wording (CUO's own text service) and replace its action with `EnterSpectatorMode()`. The rewire is
  done once per death and undone with the screen.
  **Landed 2026-10-24 (the action half):** `SpectatorCameraService.RewireReceiptMenuButton` walks the death
  screen's buttons once per death, matches the persistent event named `ToMainMenu`/`SaveAndExit`, and replaces
  its onClick with the mode switch — so the receipt's own menu button now enters spectator mode instead of
  loading the menu. **The label half is NOT landed**: it needs the adapter's localization service in this
  service's constructor, which is a composition change; until then the button keeps its native label and the log
  line names the rewire (`the death receipt's ToMainMenu button now enters spectator mode`).
- The exit stays what a revive already does, and every revive path (`RespawnCoordinator`, `RespawnPolicy`) calls
  `ExitSpectatorMode` before anything else; `SpectatorCameraService.Update` already leaves the mode the moment the
  local body is alive again.
- Reading: two clients, host dies, the receipt's button enters the mode, host is revived, the mode ends and
  the normal HUD is back; `/gamemode spectator` works for a granted member and is refused without the grant.

**Slice 1 is read on three clients (2026-10-24, deployed `0.1.0+9757f22b`)**, host + guest + alt in one world:

| Step | Reading |
|---|---|
| the receipt appears | the host's vitals held at zero until dead, then `alive:false`; the receipt shows only after the black fade — `endScreen:true` about 8 s after the hold (the probe's own sleep blocks the frame that fades, so the wait is the game's, not the mode's) |
| its menu button enters the mode | the click on the visible button whose persistent event is `PlayerCamera.ToMainMenu` (label now the localized `spectator.receipt_enter`) → `spectator:true`, and the state right after is still `alive:false`, `endScreen:true` — **no scene load**, the world stayed up |
| revive ends the mode immediately | `bloodVolume`/`brainHealth` restored → `alive:true`, `spectator:false`, `endScreen:false`, and the host is still `role: Host` in the world; guest and alt stayed `inWorld: true` throughout |
| why the first attempt failed | the earlier runtime rewire could not remove the scene's persistent listener, so the click ran the native menu load as well and the session went to the menu; the method-level intercept (dead local body + receipt active + no pause menu) is what makes the button's click mean the mode |

Still owed by slice 1: the free camera's toggle is slice 4's detail; the `/gamemode` verb was read in slice 2's run
below.

**Slice 2's effects and command half read on three clients (2026-10-24, deployed `0.1.0+74905438`)**, in the same
session:

| Step | Reading |
|---|---|
| the death fade is up before watching | with the vitals zeroed across several frames the receipt shows and the screen is fading: `black: 0.726`, `vignette: 0.75` |
| entering the mode clears the effects | a click on the receipt's menu button → `spectator: true`, and the next read is `black: 0`, `vignette: 0` — the writer stays off, so the frame does not fade back |
| `/gamemode survivor` leaves the mode and the game's own effects resume | `Normal play.`, `spectator: false`, and the values come back from the body: `black: 0.05`, `vignette: 0.443` |
| `/gamemode spectator` enters it again (the cheat-gated verb, live) | `Spectator mode on. Right-click a member to watch them; /gamemode survivor returns to play.`, `spectator: true`, `black: 0`, `vignette: 0` |
| the world survives the whole round | the guest stayed `inWorld: true` throughout, and a restore of the vitals revived the host afterwards |

Still owed by slice 2: the sound and input halves are implemented (the `Sound.Play` refusal and the early return
in `PlayerCamera.HandleInput`) but NOT read on the machine — neither has an in-process observable the probe could
assert without a new counter, and the run did not add one. They are recorded as implemented, not as judged.






### Slice 2 — pure observation

- One presentation gate (the adapter's spectator state, read by every family that renders the LOCAL body's
  experience): screen effects (`PlayerCamera`'s vignettes/black amount), the local body's sounds, the critical
  heartbeat loop, and the normal-play hotkeys' handlers. Each family asks the gate; the gate is the mode.
- Reading: in spectator mode, none of the local body's effects/sounds/heartbeat fire and the hotkeys do nothing;
  the body is still attacked and injured in the world (the ruling) and the damage reaches it while the watcher
  sees nothing of their own.

### Slice 3 — possession

- The "watch" entry on a member's context menu, offered only in spectator mode (no distance or reachability
  check by ruling), which sets `PlayerCamera.following` to that member's display body and marks the possession.
- The watcher's UI is replaced by the target's: the backpack window reads the target's carried items read-only,
  the remote medical panel opens on the target's body, the bottom state icons come from the target's body, and
  the presentation families render the target's effects/sounds instead of suppressing them.
- The target is told: a line in their UI naming the watcher.
- Reading: two clients, one spectator possesses the other; the watcher sees the target's icons/backpack/medical
  panel, the target sees the notice, and ending the mode restores the watcher's own HUD.

**The notice's exact change, planned 2026-10-24 (the next unit; the entry and the watch menu are landed):**

| Piece | What it is |
|---|---|
| message | `SpectatorNoticeMsg` with `WatcherSteamId` (whose possession it is), `TargetSteamId` (who is being watched) and `Watching` (start or stop) — the shape `ChatMsg` already has, one direction each way |
| number | the next free `NetMsg` after `LayerAdvanceRefused = 163`; pre-release the number is not frozen (decision 241), so the change records itself in the message header |
| path | the watcher sends to the host through the world channel (`ChatChannel` is the template: `ChatMsg` is guest→host then host-relayed), the host relays to `TargetSteamId` ONLY (a targeted relay, unlike chat's broadcast) |
| target side | a `SpectatorNoticeHandler` that keeps the current watcher's name (`ISessionControl` display name) and feeds the Online UI's existing notice surface with the localized `spectator.watched_notice`; a stop notice clears it |
| gates to move in the same change | `NetMsg` direction classification (guest→host and host→guest), the sync-coverage matrix row, the wire vocabulary index, the localization catalogue (`spectator.watched_notice` becomes read when the handler formats it) |

**The chain the first attempt measured (2026-10-24; the attempt was reverted rather than left half-built):** the
message, its `NetMsg` number, the channel and the handler are the easy four; the cost is the world-control
plumbing every message must cross — `IWorldControl` (four members), `WorldChannelRelay` (constructor parameter,
field and three forwarders), `WorldService` (constructor parameter, three forwarders and the relay's constructor
call) and the DI registration of the new channel beside `ChatChannel` — plus the watcher's send (the spectator
service needs `IWorldControl`, a composition line) and the target's consumer (the plugin subscribes to the event
and formats the localized line). That is ten files in one commit, and the gates then ask for the direction list,
the coverage matrix row and the vocabulary index in the same change. The next attempt starts with all ten named
here instead of rediscovering them.

**The notice, landed and read on three clients (2026-10-24, deployed `0.1.0+6c09903c`):** the chain landed in
`db2601a0`; reading it found and fixed two real defects in the same session — the mode exited 18 ms after it was
entered (the revive exit treated "the body is alive" as "the player was revived", while a live-bodied spectator is
exactly the ruling's shape; `eb8f443d`), and a host's own notice never left its client (the channel's send required
the GUEST role although a host is itself the relay; `6c09903c`). After both fixes the reading gives: host
`/gamemode spectator` stays on, `/spectate <member>` answers `Watching <id>`, and **the watched member's own log
receives the notice** — `[Spectator] notice watcher=<host> target=<member> watching=True role=Guest` in the
target's client. The plugin then publishes the localized line into the session's notice slot.
**The visible line: the display path is proven, the publish is not.** A probe that calls
`ISessionNotices.Publish("PROBE STATUS LINE")` on the target's client — with a capture through `-Id` 3 s later —
shows that text in the client's top-left status line, so the slot, the drain and the 1.5 s delay + 15 s hold all
work. The same capture taken 3 s after a real watch shows `RTT` only: the notice reaches the client (the handler's
log line), but the plugin's `OnSpectatorNotice` never turns it into a status line. The slot probe also reads empty
right after a watch, which is consistent with either case (the drain consumes it every frame). The next attempt
starts by logging inside `OnSpectatorNotice` to decide between the two candidates: the subscription is on a
different `IWorldControl` instance than the handler fires on, or the plugin host that carries it is not the one
built for this client.

**Round 84: the subscription moved into the Runtime and the publish now happens — the line still does not show.**
The plugin-side subscription was replaced by `SpectatorNoticeService`, an `ICuoService` registered beside the two
liveness watchdogs and subscribing to `IWorldControl` in its own `Initialize`, so the world instance is the same by
construction. The reading then shows the service publishing —
`[Spectator] the watched player was told (watcher=<host>)` in the target's own log — and the handler's arrival line
beside it, **but a capture of that client three seconds later still shows `RTT` with no notice line**, while the
probe's own `Publish` in the same client does show. That leaves one kind of explanation: the Runtime's
`ISessionNotices` instance the service publishes into is not the one the Online UI drains (a container or scoping
difference between the plugin's provider and the Runtime's injection), and it is what the next attempt proves by
comparing the two instances' identities from in-client probes before any further change.






### Slice 4 — free flight

- How the free camera is toggled in the mode (a verb, a key, or part of the possession exit) is the one detail
  left open; it keeps using `PlayerCamera.isFreecam` and stops being a host rule.



- Related: `reversing/Assembly-CSharp/Assembly-CSharp/PlayerCamera.cs` (the native follow target, the native
  free camera and the death screen), `reversing/KrokMP/KrokoshaCasualtiesMP/KrokoshaCasualtiesMP/UIInGame.cs`
  (KrokMP's spectator mode), `src/CasualtiesUnknownOnline.Runtime/Session/HostRules/IHostRules.cs` (the host
  rules surface, where KrokMP's three spectator switches belong), `src/CasualtiesUnknownOnline.GameAdapter/World/RespawnCoordinator.cs`
  and `src/CasualtiesUnknownOnline.Runtime/Session/World/RespawnPolicy.cs` (the revive/respawn paths that
  must put the camera back), `docs/backlog/done/middle-click-location-marker.md` (its recorded
  "Dead/spectating" clause), `docs/backlog/done/enter-key-opens-plain-chat.md` (its input-context list
  already names a death/spectator panel)

## What the game already gives us

The demo ships both cameras this feature needs, and a death screen to step aside from:

- **A follow camera**: `PlayerCamera.following` is a `Transform` field (`PlayerCamera.cs:3124`) and the
  camera reads `following.position` whenever it is set (`PlayerCamera.cs:1947-1949`), so "watch this body"
  is one assignment, not a camera of our own.
- **A free camera**: `PlayerCamera.isFreecam` (`PlayerCamera.cs:3127`) drives the camera from
  `ConsoleScript.instance.freecamPos` (`PlayerCamera.cs:1963-1965`).
- **The death screen**: `endScreen` is activated and filled with the run's stats
  (`PlayerCamera.cs:2298-2328`, `:2365`), and `didDeathScreen` is latched once the body is no longer alive
  and the fade has finished (`PlayerCamera.cs:2392-2394`).

**Today CUO does nothing with any of it**: there is no spectator code in `src/`, so a dead player sits at
that screen until the group revives them at a trader, the next layer respawns them, or they leave
(decision 165's Continue path owns the last one).

## What KrokMP does (reference, not a model)

KrokMP replaces the in-game UI class with its own and keeps a spectator state in it
(`reversing/KrokMP/KrokoshaCasualtiesMP/KrokoshaCasualtiesMP/UIInGame.cs`):

- `StartSpectatorMode()` (`UIInGame.cs:355-379`) hides `PlayerCamera.main.endScreen`, then points the
  camera at a body — the local one when it still has one, otherwise it reports `NOBODY` — and remembers
  which member is being followed by name and index.
- `StopSpectatorMode()` (`UIInGame.cs:382-401`) clears `PlayerCamera.main.following` and re-shows
  `endScreen` only when `didDeathScreen` is latched, so a revived player gets their own view back.
- Target cycling walks the member list and re-points the camera (`UIInGame.cs:194-206`), and a `spectate`
  console command toggles the mode (`Con_.cs:1268-1284`). A server can also order one client into it
  (`ClientMain.cs:657-662`, `ClientReceiver__forcespectator`).
- Three of its game rules govern it (`KrokoshaMultiplayerGameRules.cs`): `AllowSpectatorFreecam`
  (`:194`, default true), `SpectateWhileUnconscious` (`:199`, default false) and `LateJoinSpectate`
  (`:211`, default false). `Con_.CanFreecam()` (`Con_.cs:348-370`) allows the free camera for anyone the
  mode applies to — dead by default, unconscious only when that rule is on.

Read it as behaviour, never as a design to copy (the same rule `docs/en/internals/game-internals.md` §"KrokMP
is a reference, not a model" states): KrokMP broadcasts body state and replaces the game's own coroutines,
while CUO keeps one owner per body and a frozen render proxy.

## What is asked

After the local character dies, its player should be able to keep watching the run — **follow a living
teammate, and free-fly if the host allows it** — until they are revived, respawn on the next layer, or
leave. The death screen stays the entry point and the place the player returns to; spectating is something
they do from it, not a screen that replaces the run's rules.

This is a **presentation-only** feature and that is what makes it cheap: the camera belongs to the client
that is watching, the body being watched is already replicated as that client's own display clone, and a
dead player's state is already synchronized. **Nothing here needs a wire change, a new message or a new
judgment** — which also means no protocol number moves (decision 241).

Three switches do belong on the host, because they decide what the group tolerates rather than what one
client renders. Their KrokMP-aligned defaults are the starting proposal, and the cycle confirms them where
it starts:

| Rule | Default | Why |
|---|---|---|
| Free-fly while spectating | On | A free camera can scout the layer ahead, so it is a group decision; KrokMP ships it on. |
| Spectate while unconscious | Off | An unconscious player is still playing in CUO's model (their own body is theirs to act on), so this changes a live player's client, not a dead one's. |
| Spectate while waiting to join | Off | That is the late-join case, and it is a separate promise about what a not-yet-in-world client may see. |

## Required work

1. Put the three switches on the host rules surface beside the existing respawn options
   (`IHostRules.cs`), each with the default named above, so a session decides once and every client reads
   the same answer.
2. Reuse the native mechanisms and nothing else: the follow target is `PlayerCamera.following`, the free
   camera is `PlayerCamera.isFreecam`, and the death screen is hidden and restored through `endScreen`
   with `didDeathScreen` respected — no CUO camera, no CUO death screen, no new UI window (the redo would
   have to pass the "why is the native surface not enough" test in `AGENTS.md` first).
3. Pick the target from what CUO already shows: the Online UI's member list is the natural chooser
   (`OnlineUiMemberListDrawer`), with the console as the keyboard path. A followed teammate who dies,
   leaves the layer or disconnects must move the camera on — the one case KrokMP leaves open and this
   feature cannot.
4. Keep every existing death-path intact: the revive at a trader, the next-layer respawn and the
   menu-return cut (decision 167's seam) each end the spectator view and clear `following`, and the
   spectator's own dead body keeps being simulated on its own client exactly as today (decision 216's
   rule is about the body, not the camera).
5. Verify on two clients in one session: kill one character, follow the survivor, cycle targets, free-fly
   with the rule on and off, then take each of the three exits above and confirm the local view returns —
   including the case where the followed teammate dies first.

## Non-goals

- Not a respawn or revive redesign: the per-layer respawn and the trader revive stay as they are, and this
  ticket only decides what the dead player sees while waiting for one of them.
- Not spectating the enemy or the world through the kernel: the followed subject is a member's body, and a
  free camera moves a client's own view rather than reading state it was not sent.
- Not a change to what a dead player may *do*: a spectator places no ping, performs no action and drives no
  possession, which is the clause `done/middle-click-location-marker.md` recorded for the dead case and
  this ticket keeps.
## Step 1 landed (2026-10-17): the three host rules

`SpectatorFreeFly` (on), `SpectateWhileUnconscious` (off) and `SpectateWhileWaitingToJoin` (off) now exist end
to end — id, census row, rule set, the rules message's fields 14-16, the option, the service property, an
Admin-page row each, the console's own write verb, both language labels, both configuration pages, the config
entries inside the monitor's watched set, the plugin editor's own setters, and the tests' rule-set
constructions. A session decides once and every client reads the same answer, which is required work 1.

**Nothing reads them yet**, and that is deliberate: no camera follows anyone, so this step changes no
behaviour at all. Required work 2-5 are what make the switches mean something, and they are the next session's
work: the follow target and the free camera, the member-list chooser plus the rule that a followed teammate who
dies, leaves the layer or disconnects moves the camera on, the three exits (trader revive, next-layer respawn,
menu-return cut) each clearing the follow target, and the two-client verification.

**One measured number moved and the reason is worth keeping**: the rules message's entry pass grew 2458 → 2460
→ 2462 bytes across this cycle's two rule additions, and only TWO of these three fields are on the wire — a
value equal to the wire format's own default is not written, so three booleans cost two bytes. The assertion in
`EntryRepairConvergenceTests` and both evidence documents moved with it.
## Steps 2 and 3 landed (2026-10-17): the camera and the keyboard chooser

The camera uses the game's own mechanisms and nothing else, which is required work 2: `PlayerCamera.following` is
the follow target, `PlayerCamera.isFreecam` the free camera, and the death screen is hidden and restored through
`PlayerCamera.endScreen` with `didDeathScreen` respected. All four are public fields on the game's own camera, so
there is no CUO camera, no CUO death screen and no new window. `/spectate <selector>`, `/spectate off` and
`/spectate freefly on|off` are the keyboard path; the runtime port is `ISpectatorControl` and the adapter answers
it, so the console layer never touches a game type.

**An adversarial review ran against the frozen tree before the commit and found 1 blocker, 6 major, 9 minor and
3 nits.** The three that would have been felt in play are fixed here:

- **The followed teammate's death did not move the camera.** The check asked the member's render clone whether it
  was alive, and that clone's `alive` is its own brain health, which CUO never writes — so a dead teammate kept
  answering "alive" and the camera stayed on the corpse with the death screen hidden. This is precisely the case
  the ticket says must not be left open. The liveness now comes from the `PlayerEntity` the session publishes.
- **The free camera was cleared on every frame of every session**, including for players who never spectated —
  and `isFreecam` has a second owner in the game's own `/freecam` console command, so CUO would have switched
  that command off. The camera now gives back only what it took.
- **Turning the free camera on dropped the followed body** and jumped the view to whatever `freecamPos` last
  held, because nothing seeded it. It is now seeded from the camera's own position, the way the game does it.

The contracts the review also caught are fixed: the service implements the session-teardown contract every other
session-scoped service implements (a session ending mid-watch no longer leaves the state alive for the next one);
the port is registered on the aggregate and pinned in `AdapterCapabilityPortShapeTests`, so deleting its wiring
can no longer pass every gate; the free-fly sub-command requires its own operand instead of silently choosing
"off"; it is refused while nobody is being watched; and one property with no reader was deleted rather than kept.

Seven unit cases cover the half that is testable without the game: the command's answers and every refusal path.

## Limits this cycle names

- **The two-client run's death is the staging blocker, measured 2026-10-24**: with a host and a guest in one
  world, a probe set the guest's own `bloodVolume` and `brainHealth` to 0; a second read six seconds later showed
  `bloodVolume` back at 100 (the host's authoritative copy re-sends it — the guest's body is simulated on the
  host, per decision 216) while `brainHealth` stayed 0, and the game never printed a death, so no death screen
  and no spectator state appeared. The scenario must therefore be driven where the vitals are authoritative (the
  host's own body, or the host's copy of the member's body) or through the game's own injury path, not by editing
  a guest client's vitals; the row stays `unproven` until a run does that.
- **The member-list chooser now exists beside the console**, and the two read ONE rule: a member card offers the
  button on the same two terms the camera itself reads (a dead character, or an unconscious one while the host's
  rule allows it), and the row being watched carries the way out. What the card cannot know is whether the
  target's body exists on THIS client — that fact is adapter-only — so a click can still be refused, and the
  card, unlike the console, has nowhere to say so.
- **The camera half has no test and no runtime reading.** Everything touching `PlayerCamera`, the clone renderer
  or the local body needs a running client. The judgement itself (local liveness, the session flag, the three
  rules, the member's published liveness) is pure enough to be extracted into the Runtime and unit-tested; the
  review named that extraction as the place this defect class would have been caught, and this cycle did not do
  it.
- **`SpectateWhileWaitingToJoin` still has no reader**, and `MayWatch` hard-refuses the state it governs. It is a
  switch that cannot take effect until a later step gives the late-join path a way to watch.
- **The non-goal about not seeing more than was sent holds for FOLLOWING and not for the free camera**: a free
  camera moves this client's own view around a layer it already renders, which is why the host owns the rule.
- **The death screen's own receipt is interrupted while watching** (its statistics animation waits for a click on
  a panel that is hidden), and a single-frame flash of it is possible on revive depending on script order. Both
  are screen-only and unverified.
- **The free-fly rule is read from the announced rules**, so between a host's change and its arrival a member
  still reads its own configuration.
## Step 3's other half landed (2026-10-17): the member list is a chooser now

The card that already carries every other member action carries this one too: a "Watch" button on a member this
client may follow, and a "Stop watching" on the row being followed. The rule is the CAMERA's rule, read through
the same two terms (dead, or unconscious while the host's rule allows it), so the list and the console cannot
disagree about who may watch. The id being watched is published by the camera itself rather than remembered by
the UI, because the camera gives a watch up on its own when the followed member dies, leaves the layer or drops —
a second copy would have kept offering a stop that stops nothing.

**A second adversarial review ran against the frozen tree before the commit and found 1 blocker, 3 major, 4 minor
and 4 nits.** The blocker was exactly that second copy: the watched id was read into the context once, when the
plugin was constructed, so the stop button could never render and the row was never marked — the feature's whole
exit was unreachable. It is fixed by publishing the id from the port and refreshing it every frame, the way the
IP-direct fact beside it already was. The majors: the two answers could disagree (fixed with the published id),
the button ignored the host's unconscious rule and offered itself where the camera would refuse silently (fixed
by reading the camera's own terms, and by letting the console remain the surface that reports a refusal), and the
roster's local id came from a different key space than the roster itself in IP-direct sessions (fixed by taking
it from the session). Five unit cases now pin the projection's two new facts.

Still open and named rather than assumed: the target's body being present on this client is an adapter-only fact
the card cannot mirror, the right-click menu builds its own action list and now carries the same two entries,
and the whole of it still needs the two-client run of required work 5.
## The shared rule landed (2026-10-17)

Both readers of "who may watch" now call one predicate, `SpectatorPolicy.MayWatch`, in the Runtime: the camera,
which enforces it, and the member card, which decides whether to offer the button. The previous cycle left them
as two copies of the same two terms, which is exactly how the card had stopped reading the host's unconscious
switch without anything failing. Four unit cases pin the rule's own four states (dead, alive, unconscious with
the switch off, unconscious with it on) and a fifth pins that no session means nobody watches.

**The last gap closed in the same cycle.** A refused card click now says so: the card answers the watcher's
side of the rule and the camera answers the target's, so a click it refuses is reported through the surface
line the Online UI already uses for its own notices, in both languages. That was the last thing this ticket
owed, which is why it moves to `review/`: what is left is the two-client run, not code.

## The camera half's first two-client reading (2026-10-24)

The ticket's own biggest limit — "the camera half has no test and no runtime reading" — is closed for the core
path. Two clients in one world, the host's own body killed through its vitals (held at zero for three seconds
so the game's own death check fired; a single edit is restored, a guest's is restored by the host's copy), then
the dead host drove the camera:

| Step | Reading |
|---|---|
| the death the camera needs | `alive:false`, `deathScreen:true`, the guest still `inWorld:true` |
| follow the survivor | `/spectate @p` → `Watching <the guest>` and `following:true` |
| free camera on | `/spectate freefly on` → `Free camera on.`, `freecam:true` |
| free camera off, still watching | `/spectate freefly off` → `Free camera off.`, `freecam:false`, `following:true` |
| the off exit | `/spectate off` → `Stopped watching.`, `following:false` |

What this does NOT cover, and the row stays open for: target CYCLING (only two players were present, so there
was nothing to cycle), the free camera with the host's rule OFF (`SpectatorFreeFly` is on by default), the other
two exits (the trader revive, the next-layer respawn, and the menu-return cut), the "followed teammate dies
first" case, and the member-list chooser as an entry. Those need a longer session with a revive path and a
third player.