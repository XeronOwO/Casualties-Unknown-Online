# A layer change pulls every member back to the menu instead of loading the group together

- Status: Done (batch `20261021-i`, 2026-10-21: all four rows pass — the members stay in the world scene,
  run their own new-layer generation from the host's baseline and enter within 13 ms of the host; the
  dropped-edge fallback also recovers through the ordinary `WorldJoin` with the group still released
  together. Record: `docs/evidence/acceptance/layer-change-menu-round-trip-20261021-i.md`.)
- Acceptance record: `docs/evidence/acceptance/layer-change-menu-round-trip-20261021-i.md`
- Priority: Medium
- Category: World generation / layer transition / loading choreography
- Source: the user's 2026-10-21 report while watching batch `20261021-g` — after the host descended one
  layer, the host's screen showed the native loading screen while the member's screen showed the game's
  cover (the main menu), and the member rejoined only after the host's new layer had finished loading. The
  user's ruling: "这个明显不符合游玩逻辑，就应该一起加载" (this obviously does not match how the game should
  play — they should load together).
- Related: `done/layer-change-member-dropout.md` (the family's dropout half: its park experiment
  `20261007-c` is the evidence that a member CAN be kept in the world through the host's regeneration),
  `done/layer-change-member-recovery.md` (the member that lost its body to the same boundary),
  `todo/layer-descent-spawn-separation.md` and `todo/pod-descent-on-a-member-regenerates-locally.md` (the
  same boundary, other symptoms), `todo/loading-screen-progress-detail.md` (what the screen says while it
  waits), `src/CasualtiesUnknownOnline.GameAdapter/Run/ReentryCoordinator.cs`,
  `src/CasualtiesUnknownOnline.GameAdapter/World/LayerAdvanceCoordinator.cs`

## What is observed (batch `20261021-g`, session A)

The host ran the game's own `skiplayer` at 21:15:43.3. The host logged its own layer reset
(`[LayerReset] dropped the previous layer's 375 enemy row(s) and 248 fluid chunk(s)`, 21:15:43.935),
captured the new layer at 21:15:52.258 (`host roster is ready over 77 enemies`) and sent the entry
snapshot at 21:16:00.476. The member's own log for the same boundary reads
`guest dropped its enemy bindings: the host reset the roster for a new layer` at 21:15:44.173, then
`generation finished: 77 animal(s) exist at the settled edge` at 21:16:01.504 — the member was out of
the new world for those ~17 s. The user, watching both windows, reports the host on the native loading
screen and the member on the game's cover (main menu) through that window, with the member following
only after the host's loading finished.

## Why it happens (code)

`ReentryCoordinator`'s own contract states it: "the host's world exit (a layer change, or the host
leaving) pulls every member to the menu — the member's own `RunCoordinator` does that from the host's
scene report. Coming back is driven by ONE `WorldJoin` the host sends at its world-entry edge." So the
menu round-trip is the current design, not an accident: the host's exit takes the member out, the host
regenerates, and the host re-invites it into the new layer. The member's own new-layer generation starts
only after that entry instruction lands, which is why it trails the host by the host's generation time.

## What the user wants (ruling, 2026-10-21)

The host's descent must be followed IMMEDIATELY, in the same shape as the host clicking start in the main
menu: the moment the host descends, every client enters the loading screen **at the same time** and each
generates the new layer in parallel (latency is not a factor to model — the descent is one shared edge,
not a request/answer round-trip). Nobody goes to the menu and nobody waits for the host's generation to
finish before starting its own. The group is then released into the new layer together when every client's
new layer is ready (the start-gate family's hold, applied to the descent instead of the run start), so the
member's screen shows the loading screen the whole way through.

`done/layer-change-member-dropout.md`'s `20261007-c` park experiment already shows a member can be kept in
the world through the host's regeneration, so the mechanism exists; the design question is what replaces
the menu round-trip without re-introducing the drop/re-entry race that ticket fixed.

## The reading (batch `20261021-h`, 2026-10-21) — the member really is in the main menu

One native `skiplayer` descent on the pre-fix artifact (`0.1.0+03cc1099…`), host + two members, all three
in a settled world. The batch's own probes (`attribution-r*-guest-read.json`) and per-client window frames
(`attribution-r0*-{guest,alt,host}.png`) are beside the log windows
(`attribution-{host,guest,alt}-log.txt`), all under the directory named by `acceptance-artifacts-dir`.

**The member's screen is the game's cover, not the loading screen.** The guest's own frame one second
after the command (`attribution-r01-guest.png`) shows the main-menu cover with the Online UI and a
generation label; `attribution-r02-guest.png` shows the same cover alone. Its probe answers
`scene:"PreGen"`, `world:false`, `body:false` at t+2.5 s and t+5.5 s and t+9.5 s after the edge
(`attribution-r01/r02/r03-guest-read.json`), while the host's own probe in the same rounds answers
`scene:"SampleScene"`, `generating:true`, `loadingActive:true` (`attribution-r01-host-read.json`). The
host's frame `attribution-r01-host.png` is the loading screen.

**The menu return is CUO's own decision, taken 1 ms after the host's report.** Guest timeline (local
time, guest log): `21:29:04.031 Peer <host> scene state: InMenu (PreGen)` → `04.034 [Reentry] the host
left the world — this member follows it to the menu` → `04.040 RunMenuReturnCoordinator: Leaving the
world to the main menu.` The host's own line `04.016 Scene state: InMenu (PreGen)` is CUO's report,
fired because `HarmonyTraverse.HasLiveWorld` is false while the game regenerates in the same scene.

**The member then starts its new layer only after the host finished.** Host: `04.328` layer reset,
`04.613 captured world params` (the new layer's baseline), `13.431 generation finished: 80 animal(s)`,
`13.372 World join sent to 2 member(s)` + `Start gate armed — waiting for 2 member(s)`, release
`22.439`. Guest: `13.372 World join received — starting a run to follow`, `14.436 applied host world
params`, `23.280 generation finished`, `23.430 World ready — start playing`. So the member's new-layer
generation STARTED 9.3 s after the host's descent (after the host's own generation had already
finished) and it entered ~10 s after the host. The same session's second member read the same shape
(`21.508 InWorld`, `22.084 generation finished`, `22.524 ready`).

**The gate already does its half.** `Start gate armed — waiting for 2 member(s)` at the host's own
entry and `Start gate released — everyone is in the world` after the second member arrived: the
"everyone loads together" hold exists and worked. What is missing is that the member generates in
parallel and stays on its loading screen instead of taking the menu round-trip first.

## What the reading answered

- **What the member's screen shows today**: the real main-menu scene (`PreGen`), because
  `RunMenuReturnCoordinator` loads it; the loading screen CUO keeps is shown on top only for the first
  frames (`attribution-r01-guest.png`), then goes with the menu scene.
- **Which client performs the member's new-layer generation**: the member's own, through the game's own
  `WorldGeneration.RegenerateWorld` — the same coroutine a solo descent runs. It does not need the
  scene reload `StartRun` does, so the body and its carried items survive (the host log's
  `3 item record(s) remain (carried items cross the boundary)` is the same table on the host side).
  The member's own CLICK is suppressed because the layer baseline is the host's capture; a host-driven
  descent is the opposite case — the host has just announced the step and captures the baseline.
- **How the descent's shared edge is carried today**: not at all — only the host's `InMenu` scene
  report and, at the host's next entry, the ordinary `WorldJoin`. The descent itself has no message,
  which is why the member can only react after the host has finished.
- **Whether the start gate can carry it**: yes, unchanged. Its wait set is "handshaken members not in
  the world"; a descent needs that set seeded with the members the host is pulling through (their
  presence still reads InWorld until their own report arrives), and the same gate then holds the
  host and releases everyone with one `WorldReady`.

## The design (2026-10-21)

**One shared descent edge.** The host sends a new host → guest `LayerDescent` (NetMsg 157) at the
start of its own `WorldGeneration.RegenerateWorld`, before the regeneration's scene transition can be
read as an exit. It carries the native `twice` flag (the drill pod's two-layer step) and the host's
kernel generation stamp; the receiver accepts it only for the generation it is in. It goes to every
handshaken member currently in the world — the members this host is pulling through — and the start
gate records them as expected back.

**The member generates its own new layer immediately.** On the edge the member (a) cancels a menu
return already requested — only a return that came from the host's own InMenu report, never the
player's own leave, (b) drops the run baseline it still holds (the layer it is leaving), so its
generation boundary holds until the host's new baseline arrives through the kernel layer-advance, and
(c) runs the game's own `RegenerateWorld` locally, with the native entry's local steps (`forceWalk`
release, save panel closed, deepest-layer record) minus its local-body clause. Its own `GenerateWorld`
then applies the host's captured baseline exactly as a run-start generation does, and its `Clear`
puts the game's own loading screen up. The run-follow phase gains `Descending`: the world's not-live
edge during it is the descent, not a menu exit, so the member never loads `PreGen`. The layer's own
progression step (`IncreaseDepthByLayer`) is the native coroutine's, taken once when that generation
runs — the click that asked for the advance no longer grants a second one.

**The follow can be abandoned.** Besides the settle, a follow ends when the world object is gone (the
member took a real leave or a death-screen exit mid-descent — the scene load destroys it) or when one
of its waits outlived its bound (the host's baseline never came, 30 s; the generation never settled,
180 s). Abandoning clears the descend phase and hands the member back to the ordinary host-pull /
`WorldJoin` path; without it a member that left mid-follow would be refused every later invite for
the rest of the session. A half-built world with no way forward is left to the menu, the same exit
the start gate's valve uses.

**Everyone is released through the existing gate.** At its own new-world entry the host arms
`WorldStartGate` (now seeded with the descent's expected members); each member reports InWorld when
its generation settles and waits in `WaitingReady` (frozen, loading screen kept); the host's
`WorldReady` releases the group. No second gateway, and the run-start path is unchanged.

**Presentation.** The descent is inside the gate window on both roles, so the native layer fade is
skipped and the loading screen is continuous instead of "old world dark, then loading"; the layer-title
alert stays deferred to the release, as at a run start.

**Fallback.** If the edge is lost the member is not stranded: the host's `WorldJoin` at its new entry
still invites it, and the pre-existing menu-return + re-entry path recovers it (that is today's
behaviour, kept as the degraded path and named in Limits). A member already out of the world at the
descent edge is not pulled through the loading screen; it enters through the ordinary invite.

## Independent review (2026-10-21) and its fixes

The independent adversarial review of the frozen change found one blocker, three majors and seven
minors; all are fixed in the same commit. Its verdict and findings are recorded in full here because
they are the change's own review record.

- **Blocker — the follow had no abort path.** `RunCoordinator`'s descent phase left only through the
  settle or a `WorldReady`, while the follow suppressed every recovery edge (`OnRemoteSceneChanged`,
  `ReentryCoordinator`, `OnWorldJoin`: `if (_phase != Idle) return`). A member that genuinely left the
  world mid-descent (a pause quit or a death-screen exit — `PlayerCameraMenuExitPatches` only defers
  when `HarmonyTraverse.HasLiveWorld`) stayed `Accepted`/`Launched` forever: every later `WorldJoin`
  was refused and only `SessionEnded` cleared it. **Fixed** by `UpdateDescentAbort` (world object gone,
  hold timeout 30 s, settle timeout 180 s → `AbortDescent`), with `LayerDescentPolicy.ShouldAbort` and
  its tests.
- **Major — the previous run's release survived into the descent.** `WorldStartGate.ExpectDescent`
  did not reset `_released`, so a member that settled before the host's new-world entry passed via
  `LateJoinerPasses` (`_released || (_armed is null && LocalInWorld)`) and played while the host was
  still loading. **Fixed** by resetting `_released` at the edge and suspending the late-joiner pass
  while a descent is expected (`_descentPending is null && (...)`), with
  `APriorRunsRelease_DoesNotPassAMemberThatSettledBeforeTheDescentGateArms`.
- **Major — a member-requested advance granted progression twice.** `TryDelegateLocalAdvance` still
  ran `GrantLocalDescentProgression` (`world.IncreaseDepthByLayer()`), and the member's own
  regeneration at the host's edge now runs that step natively as well: the requesting member gained
  +80 skill exp twice per member-initiated layer. **Fixed** by deleting the click-time grant; the
  native coroutine is the single source. The degraded path (edge lost, re-entry through `StartRun`)
  is the one path that loses the step — named in Limits.
- **Major — the saved-character handback suppression never ended.** `IsDescentBodyPreserved` stayed
  true for the preserved body's whole in-world life, so a later revive handback (the default
  `ReviveOnNextLevel` respawn) was silently dropped and the member stayed dead on its own client.
  **Fixed** by the leaf `DescentBodyPreservation` shared by the run and the character domain: the
  suppression is one-shot for the handback that answers that InWorld edge, and a revive handback
  (its `Position` is null by `RespawnPolicy.PrepareRespawn`) is never consumed.
- **Minor — `_menuReturn.Clear()` cancelled a leave it does not own.** **Fixed**: only a request whose
  `Origin` is `HostPull` is cleared; a pending player leave makes the member ignore the descent.
- **Minor — the host's entry `WorldJoin` mutated a descenting member.** The runtime dropped the
  member's held baseline on the join's promise even though the run-follow refused the join.
  **Fixed** by `CancelWorldJoinBaselineDrop` (the dropped instance lives for the event only), which
  `OnWorldJoin` calls when it refuses the instruction; the checkpoint that follows is the host's
  current session state and its world projections are deferred by their own generation guards.
- **Minor — `AlreadyDescending` covered `WaitingReady`.** A second host descent while the member was
  only waiting for the release was ignored, leaving it a layer behind. **Fixed**: a `WaitingReady`
  member accepts the next edge as a fresh follow (`AcceptDescent`), and the decision's
  `generationInFlight` input covers only an actually running generation.
- **Minor — the ticket had no Limits section.** **Fixed** by the section below.
- **Minor — `GlobalDarkDarkenPatch`'s comment said layer switches keep their fade.** Now false and it
  cited a file that does not exist. **Fixed** to name both fades and `WorldGeneration.cs`.
- **Minor — the handback suppression was a post-construction delegate.** **Fixed** by the shared
  `DescentBodyPreservation` leaf, constructed with both consumers.
- **Minor — the tests did not pin the release or the abort.** **Fixed** by the prior-release gate
  test, the `ShouldAbort` cases, the preservation tests, and the baseline-cancel tests.

## Limits

- **A member mid-entry at the descent edge is not pulled through it.** Its run phase is not `Playing`
  (it is loading the previous entry), so it keeps that entry; when the host's new world is up, the
  ordinary `WorldJoin` and the gate carry it in. It may therefore enter after the host's group, and
  the ticket's rows say nothing about that shape.
- **A guest's own drill-pod descent is still its own ticket**
  (`todo/pod-descent-on-a-member-regenerates-locally.md`): only the HOST's descent is announced. A
  guest pod still regenerates locally with its old baseline.
- **The degraded path costs the menu round trip and one progression step.** A member whose edge is
  lost goes back to the pre-fix behaviour (the host's `WorldJoin` re-invites it) and, because the
  click-time grant is gone, does not take `IncreaseDepthByLayer` on that path. The layer's depth and
  travelled distance are corrected by the host's baseline; the lost part is the +80 skill exp.
- **A descenting member can still receive the host's entry `WorldJoin` and its checkpoint.** The
  join is refused by the run phase and its dropped baseline restored; the checkpoint is the host's
  current session state, and the world-facing projections of a checkpoint are deferred by their own
  generation-finished guards. The reserve is a rare swallowed edge: the member would then take the
  menu path, which is the named fallback.
- **The two timeouts are bounds, not measurements**: 30 s for the baseline hold and 180 s for the
  settle are chosen well above the observed ~1 s and ~10 s. A slower machine abandons late rather
  than blocking forever.

## What is not known yet

None for this fix's normal path: the reading above chose the mechanism and the native coroutine it
runs. The remaining shape — a member mid-entry at the descent edge, and a guest's own drill-pod
descent — is named in Limits rather than guessed at.

## Acceptance (for the batch that lands the fix)

1. The host descends a layer; within one second each member's screen shows the game's loading screen,
   never the main menu or the cover — a captured frame from each client window, one artifact per client,
   plus the native scene probe (`scene`, `world`, `loadingActive`) from each member in the same window.
2. The member enters the new layer in the same window as the host: the two clients' settling/entry
   lines at most 2 s apart, the member's roster adopts `aligned=True`, and the items the member carried
   across the change are the same items afterwards (count and ids read before and after, and still in
   the host's item table).
3. No member-side `RunMenuReturnCoordinator: Leaving the world to the main menu.` line and no member
   probe reading `scene: "PreGen"` in the window; no `WorldReady never arrived` valve line and no new
   warning on either side.
4. The degraded path is unchanged: with the new edge dropped on one member only, that member still
   recovers through the ordinary `WorldJoin` and the gate (the pre-fix behaviour), and the group still
   loads together once it is in.
