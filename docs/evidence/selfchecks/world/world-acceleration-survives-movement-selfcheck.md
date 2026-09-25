# Manual world acceleration survives movement — self-check (2026-09-25)

Ticket: `docs/backlog/review/world-acceleration-survives-movement.md` (High; user report 2026-09-21
and the ruling the same day). Decision 223. Cycle scope: a session-wide manual acceleration ends
only through the accelerate key back to 1×, the sleep gate or the death/pause transitions — never
through a member's movement. The cycle replaced the world-time router's "every `SetTimeScale` call
is a speed intent" rule with the native flags' own classification, for the whole call family.

## 1. Mechanism inventory — what a `PlayerCamera.SetTimeScale` call IS

The native declaration is `public void SetTimeScale(PlayerCamera.SpeedType speed, bool switchSound =
true, bool force = false)` (reversing/Assembly-CSharp/Assembly-CSharp/PlayerCamera.cs:653), so the
game itself hands the router two discriminators: `force` overrides the paused/death-screen guard
(`if ((PauseHandler.paused || this.selfDestructActive || this.didDeathScreen) && !force) return;`),
and `switchSound` is set exactly by the calls that then play the speed-change sound.

| # | Native call site | Flags | What it is | Verdict |
|---|---|---|---|---|
| 1 | speed1/2/3 hotkeys (:887-895), console `AutoTimeScaleSet` (:649), scripted sequences (:507/:562/:584) | `switchSound: true` | a deliberate, player-visible speed change | **owns the shared clock** (unchanged: host local-first + postfix adoption, guest local-first + `WorldTimeRequest`) |
| 2 | the movement rule — `Input.GetKeyDown(right)/GetKeyDown(left)` (:921-924) | neither | a SILENT automatic reset; the reported defect | **no longer a speed intent**: swallowed on both sides (see §2) |
| 3 | in-world event resets — Vomiter (:52/:105), SpiderHandler (:94/:269), SelfHarmer (:46), SurvivorNote's close (:72) | neither | the same kind of automatic reset, one family with #2 | same rule as #2 |
| 4 | waking up (:2197), the scene start (:734) | neither | an automatic reset; the host's sleep gate and the start gate already own these transitions | same rule as #2 |
| 5 | `HandleUnconsciousScreen` (:2235/:2239/:2244) | neither | the vanilla per-side sleep fast-forward | suppressed by the `WorldTimeSleepLocal` scope before the bridge, and by the sleep branch behind it (unchanged) |
| 6 | `PauseHandler` pause/unpause (:155/:159), `EndSequence` (:2293), self-destruct (:170) | `force: true` | a pause/menu/death transition that must punch through the paused/death guard | unchanged (local-only on a guest, reported by the host) |
| 7 | SurvivorNote's open (:48, Slowmo), EPdaScript (:57, Slowmo), editor debug (:927) | Slowmo | local presentation, never on the wire | unchanged (local-only) |

The leak was in the router, and it had two halves — both reproduced from the code, neither from
narrative:

- **Guest half**: `PlayerCameraSetTimeScalePatch.Prefix` asked `WorldTimeSync.OnTimeScaleSetRequested`,
  which treated a guest's `Normal/Fast/SuperFast` call as LOCAL FIRST and sent a `WorldTimeRequest`;
  the host accepts a representable request (`OnRequestReceived`), so one guest's left/right tap
  replaced the standing request for the whole session.
- **Host half**: the postfix `OnLocalTimeScaleChanged` adopted ANY local `Normal/Fast/SuperFast` as
  `_requestedSpeed` and ran the policy, so the HOST's own movement ended its own acceleration and
  broadcast the drop.

Audited siblings that stay as they are, with the reason: the sleep fast-forward (host-owned by the
sleep gate), the forced pause/death transitions (the ruling names them as legitimate ends), the
direct `Time.timeScale` writes (`ConsoleScript.cs:815` is a deliberate admin action;
`WorldGeneration.cs:866-871` is the earthquake reset, which the host pump adopts BY DESIGN — recorded
as `todo/world-acceleration-quake-direct-write.md` because attributing a field write needs a
mechanism of its own and the gameplay answer is the user's), and `WorldGeneration.cs:1036` /
`PreRunScript.cs:64` (scene reload and run start, where the start gate owns the clock).

## 2. What landed

- `WorldTimeScaleCall` (Runtime, pure, no Unity) owns the whole rule: `Classify(SpeedFamily,
  switchSound, force)` returns `Deliberate` (an announced shared-clock change — the only kind that
  owns the clock), `Transition` (forced), `AutomaticReset` (a shared-clock speed with neither flag),
  `SleepOwned` (the vanilla 25×/3.5× fast-forward) or `LocalPresentation`; `Route(kind, isHost,
  sessionActive, atStartGate)` is the DECISION TABLE the adapter executes verbatim; and
  `ShouldRestoreSessionSpeed(kind, sessionClockInFlight, liveClockAtSessionSpeed)` decides whether a
  silent reset must be answered by putting the screen back on the session speed. Both tables THROW on an unmapped member instead of falling through (a fall-through would ROUTE it), the three enum sizes are pinned by `TheEnumCensusesAreDeclared`, and the adapter's execution of the table is anchored by the source pin.
- `PlayerCameraSetTimeScalePatch` takes the native `switchSound` (and keeps `force`) in BOTH the
  prefix and the postfix, so the flags reach the bridge; `IPatchBridge` carries the same flags and
  both its members are pinned by parameter name.
- `WorldTimeSync.OnTimeScaleSetRequested` is now a thin executor of `Route`: an automatic reset keeps
  the session's speed and returns false on both sides, the announced change is the host's own or a
  guest's local-first report, forced transitions/Slowmo/Paused/the host's sleep speed run locally, the
  guest's sleep fast-forward is suppressed, and while the START GATE holds the clock a reset is
  swallowed without writing anything (the gate's `timeScale` 0 survives — a regression the first cut
  of this cycle had, found by the independent review as W-1).
- `WorldTimeSync.OnLocalTimeScaleChanged` takes the same flags and never adopts an automatic reset
  (the prefix already swallows it, but a skipped original still runs the postfix — the Harmony
  behaviour `WorldGenerationSetBlockPatch.cs` records — so the guard is the line that keeps the host
  from adopting a movement reset as its standing request).
- `WorldTimeSync.KeepSessionSpeed`: nothing is written when the live clock already is the session
  speed — which is the movement case, so a movement key produces no dip and no speed sound — and a
  local presentation effect that held the screen elsewhere (the survivor note's Slowmo) is put back
  SILENTLY through `ApplyLocalTime(speed, switchSound: false, force: false)`, which is why the note
  still closes. Both paths log at Debug, and the restore path RE-READS the clock before it claims a
  write, so a session's log never reports a write that the native pause/death guard or a missing
  camera prevented.
- `ProtocolVersion.Current` is bumped in the same change (a peer without the rule would keep
  reporting its own movement as a speed intent, so a mixed session would reproduce the defect); the
  constant's own doc comment carries the entry, and the two `ProtocolVersion.cs` quotes in
  `docs/evidence/sync-coverage-evidence.json` (rows W1/I6) were re-pointed with it.

## 3. Verification

| Claim | How it was checked | Result |
|---|---|---|
| The router can SEE the discriminator, and the seam between patch and router keeps it | `PlayerCameraSetTimeScalePatchTests` — `Prefix_SeesTheNativeSwitchSoundFlag`, `Postfix_SeesTheNativeSwitchSoundFlagAndForce`, `BridgeSeam_CarriesTheSameDiscriminators` (reflection over the built adapter) | passed |
| The classification is one rule for the whole family | `WorldTimeScaleCallTests.Classify_OnlyAnAnnouncedSpeedChangeIsASpeedIntent` (7 rows — the native call-site families of §1) | 7 passed |
| THE ROUTING DECISION: a silent reset is swallowed and never reported on either side, and it writes NOTHING while the start gate holds the clock | `WorldTimeScaleCallTests.Route_DecidesWhoMayWriteTheClock` (17 rows: both roles, the gate, announced/forced/presentation/sleep, outside a session) | 15 passed |
| The adapter executes that table, has exactly one ANCHORED reporting site, and the tables cannot grow silently | `WorldTimeScaleCallTests.TheAdapterExecutesTheRoutingTable` + `TheEnumCensusesAreDeclared` (source pin on `WorldTimeSync.cs`, enum census) | 2 passed |
| A silent reset never overwrites a clock that is already right, is never restored into a lead window, and never re-asserts for another kind | `WorldTimeScaleCallTests.ShouldRestoreSessionSpeed_OnlyForASilentResetThatMovedThisScreen` (6 rows) | 6 passed |
| The whole world-time family still holds | `dotnet test tests/CasualtiesUnknownOnline.Tests --filter "FullyQualifiedName~WorldTime|FullyQualifiedName~PlayerCameraSetTimeScale|FullyQualifiedName~PatchContract|FullyQualifiedName~PatchInventory|FullyQualifiedName~AdapterCapabilityCatalog|FullyQualifiedName~PatchBridgePort"` (the extra term belongs in the surface: this cycle changed the `IPatchBridge` seam; run recorded in `%TEMP%/cuo-focus4.txt`) | 155 passed / 0 failed |
| The protocol bump's evidence anchors moved with it | `SyncCoverageGateTests` (W1/I6 quotes re-pointed) | passed |
| The build, the gates and the whole suite hold | `dotnet test CasualtiesUnknownOnline.slnx --filter "FullyQualifiedName!~DeliveryChecklist"` (with build) | main suite 3953/3953, normative gates 148/148 (the delivery-checklist case runs on its own: 149/149), 0 warnings / 0 errors; `dotnet format` exit 0 |

## 4. The red, before the fix (workflow gate), and what is pinned now

`dotnet test tests/CasualtiesUnknownOnline.Tests --filter "FullyQualifiedName~PlayerCameraSetTimeScalePatchTests"`
on the frozen HEAD `0e7693f4` before any production edit: **2 failed / 0 passed** —
"the SetTimeScale prefix must receive the native switchSound flag (got: speed, force)" and
"the SetTimeScale postfix must receive switchSound too … (got: speed)".

That red pins the missing discriminator (a router that cannot see `switchSound` cannot tell a speed
change from a reset — half of the defect). It does NOT pin how the router USES the flag, and the
independent review called that out (W-2): reverting the adapter to "every call is an intent" would
have kept it green. The routing decision is therefore now a pure table (`WorldTimeScaleCall.Route`)
with its own 17-row matrix, and the adapter is a thin executor pinned by a source pin — treating a
silent reset as an intent now requires changing the table (red) or bypassing it (pinned).

Still NOT reachable in this harness: a test that constructs `WorldTimeSync` and drives the real
prefix → bridge → domain path. The blockers, checked rather than assumed: the routing branches read
the live `Time.timeScale` (a Unity icall), `_run.LocalBody` / `_gate.WaitingForReady` need real player
objects, the constructor takes the concrete `RunCoordinator` and `StartGateCoordinator`, and the
patch's seam is an internal interface with no net48 proxy path (no non-generic
`DispatchProxy.Create(Type, Type)` overload, no mocking library in the test project). The Game Adapter
assembly itself IS loadable in the test host — `GameAssemblyHost.Adapter` loads it and other tests
construct adapter types with `Activator.CreateInstance` — so "compile-excluded" is NOT the reason; the
Unity/session dependencies above are. No reachable path was found this cycle, which is narrower than
"unreachable in principle".

## 5. Limits — what only a real session can show

- The on-screen rows: the host accelerates, then walks, and the acceleration continues on BOTH
  screens; a guest walks and nothing changes; the HUD speed indicator and its sound do not switch on
  a movement event; pressing the accelerate key back to 1× still ends it for everyone.
- No frame was rendered and no dual-client session was run by this cycle: the evidence here is
  compile, tests, gates, code anchors and the native call-site inventory. `Logging.MinimumLevel` at
  Debug shows the swallowed-reset lines in a session.
- The quake/direct-write sibling (§1) is NOT fixed here; it is an open ticket with its own evidence.
- Recorded, not fixed (pre-existing and repo-wide, outside this cycle's subject): this self-check is
  not listed in `docs/evidence/selfchecks/MANIFEST.md` — neither are the ~26 self-checks added since
  2026-09-20 (196 table rows for 255 self-check pages, while the header still reads as the complete
  index).
- Recorded, not fixed (a neighbouring claim, not introduced here): `StartGateAlertPatchTests` states
  "No GameAdapter is constructed in the test process" while `ItemAdvancedBehaviorProviderTests`
  constructs an adapter type — one of the two comments is wrong.

## 6. Independent adversarial review (2026-09-25)

Frozen tree, fresh context, read-only, FULL tier; report `%TEMP%/cuo-review-world-acceleration.md`
(2 major / 5 minor / 3 nit / 0 blocker). Disposition of every finding:

| Finding | Disposition |
|---|---|
| W-1 (major) `KeepSessionSpeed` was the only `ApplyLocalTime` caller with no start-gate check, so a silent reset during the gate's load freeze would have written the session speed over the gate's `timeScale` 0 | FIXED structurally: the gate is an input of the decision table (`AutomaticReset` + `atStartGate` → `Swallow`, nothing written) and the matrix pins it; ticket row 7 and decision 223 were re-worded with it |
| W-2 (major) the red pinned only the patch surface; the router itself had no behavioural coverage | FIXED: `Route` is a pure decision table with a 17-row matrix, the adapter-executes-the-table pin, and the bridge-seam parameter pin |
| W-3 (minor) §4's reason for "no behavioural red" was wrong (the adapter assembly IS loaded and adapter types ARE constructed in the test host) | FIXED: §4 now names the real blockers and says "no reachable path found this cycle" |
| W-4 (minor) the Debug line claimed a write that may not have happened (`PlayerCamera.main == null`, or the native pause/death guard) | FIXED: the restore path re-reads `Time.timeScale` before it claims a write, and logs the actual outcome otherwise |
| W-5 (minor) the postfix's reset guard was reported as unreachable (the review assumed a skipped original also skips the postfix) | REBUTTED, and the second review then VERIFIED it at runtime on this repository's own `references/0Harmony.dll` (BepInEx HarmonyX 2.9.0): the prefix skipped the original (RUNS=0) while the postfix still fired, `__runOriginal=False`. The guard is the ONLY line that stops the host adopting the swallowed reset — not a redundant second line — and the code comment now says so |
| W-6 (minor) ticket row 7 and decision 223 claimed the start gate was untouched | FIXED with W-1 |
| W-7 (minor) the checklist's "ten native SetTimeScale sites" matched no reproducible count | FIXED: the 26 native call sites and the four direct writes are the recorded numbers |
| W-8 (nit) `docs/backlog/README.md` lost its UTF-8 BOM (an unrelated byte-level change) | FIXED: the BOM is restored |
| W-9 (nit) the new self-check is not in the evidence MANIFEST (as ~26 recent pages are not) | RECORDED in §5: pre-existing and repo-wide, not this cycle's subject |
| W-10 (nit) the port-shape gate pins member names only, so the two signature changes were ungated | FIXED: `BridgeSeam_CarriesTheSameDiscriminators` pins `IPatchBridge`'s parameter names |
