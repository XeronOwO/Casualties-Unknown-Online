# A dead player can watch the run by spectating a teammate

- Status: Review — implemented and gated on both choosers; the two-client run is what is left.
- Priority: Medium
- Category: Player interaction / presentation / KrokMP parity
- Source: the user's 2026-10-13 backlog request, in their own words: "操控的角色死亡后，能观战他人，参考
  KrokMP 的功能" — after the character you control dies, you should be able to spectate the others, with
  KrokMP's feature as the reference.
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