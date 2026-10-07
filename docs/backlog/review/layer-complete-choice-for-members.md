# The end-of-layer choice must be reachable by every member

- Status: Review — the development half landed 2026-10-07 (see *What landed*); the runtime row (a dead host
  at the boundary, two living members) needs three clients and is named in *Limits* item 1. The static
  attribution this ticket's first job asked for is taken above: the native surface, who creates it, and what
  each side does with the click.
- Priority: High
- Category: Layer progression / session flow
- Source: the user's 2026-10-07 backlog request — finishing a layer offers a save-or-continue choice; they ask
  whether both the host and the guests can see it and click it, so that a dead host cannot leave the guests
  unable to reach the next layer.
- Related: `review/save-layer-end-save-and-restore.md` (S2 — the layer-end save itself),
  `review/save-system-mid-run-and-layer-end.md`, `review/save-mid-run-consistent-cut.md`,
  `todo/layer-change-member-recovery.md` (a member out of the world at the boundary),
  `todo/pod-descent-on-a-member-regenerates-locally.md` and `todo/layer-end-save-and-exit-native-write.md`
  (this cycle's two family siblings),
  `done/layer-mod-baseline-divergence-on-continue.md` (a Continue that reopened the wrong world)

## What is asked

One requirement with two halves:

1. **Visibility and reachability**: when the end-of-layer choice appears, every member who is in the world
   sees it and can act on it, not only the host.
2. **No host-shaped dead end**: if the host is dead (or otherwise unable to act) at that moment, the remaining
   members can still move the session to the next layer — the run must not be stuck on one client's state.

The user's framing is a stuck-session report: the choice may exist, but if it is only present or only
clickable on the host, a dead host ends the run for everyone.

## The reading — solution of the first half (static, 2026-10-07)

Every claim below is read off the shipped game (line numbers are the frozen `reversing/` tree) or off this
tree's own quoted text; no client was run, so the runtime half of the reading is named in *Limits*.

| Question | Answer | Where it is read |
|---|---|---|
| Which native surface offers the choice? | `WorldGeneration.savePanel`, a scene panel whose two buttons are the two public entries `WorldGeneration.ContinueRun` (`WorldGeneration.cs:1011`) and `WorldGeneration.SaveAndExit` (`WorldGeneration.cs:1023`) — neither has a caller anywhere in the decompiled assembly, so the panel's own wiring is the only producer | `reversing/Assembly-CSharp/Assembly-CSharp/WorldGeneration.cs` |
| Who creates it, and when? | The client whose OWN body is at the layer's bottom, in its own `Update`: `if (!this.savePanel.activeSelf && !this.doingRegen && !this.generatingWorld && this.worldExists && PlayerCamera.main.body.transform.position.y < (float)(-(float)((ulong)this.halfHeight)) + 3.1f …) { this.savePanel.SetActive(true); this.body.forceWalk = true; }` (`WorldGeneration.cs:979-983`) | same file |
| Is it host-only today? | **No.** The panel is per-client local UI driven by that client's own body (`WorldGeneration.Update` decides on `PlayerCamera.main.body.transform.position.y`), and CUO never wrote the panel before this cycle: the only `savePanel` write under `src/` is the host drive this change adds | `src/` (the one site) + the native condition above |
| What does the Continue click do? | Local only: `ContinueRun` re-checks the same clauses, releases `forceWalk`, hides the panel, writes the local `deepestlayer` record and starts `RegenerateWorld(false)` on the clicking client | `WorldGeneration.cs:1011-1020` |
| Can a member's click reach the next layer today? | **No.** The layer's baseline is the HOST's capture (`WorldParamsService.CaptureAtBoundary` is reached only from the host/solo branch of `RunCoordinator.OnWorldGenerate`; `WorldService.PublishWorldParams` commits the kernel's `AdvanceLayerCommand` from that capture). A guest's local regeneration has nothing new to apply — `EnsureGuestApplied` is idempotent per params instance, and the instance in hand is the layer being left — so the member builds a layer the session never agreed on and the session does not move | `src/CasualtiesUnknownOnline.GameAdapter/Run/RunCoordinator.cs`, `World/WorldParamsService.cs`, `src/CasualtiesUnknownOnline.Runtime/Session/World/WorldService.cs` |
| How does a member reach the next layer when the host DOES click? | The host's generation start flips its `HasLiveWorld` false (`camera + world + !IsGenerating()`), which the members read as the host leaving the world and answer with `RunMenuReturnOrigin.HostPull` (main menu); the host's world-entry edge re-invites the members that are out of the world (`SendWorldJoin` skips members whose `InWorld` is true) and they enter the layer the host captured | `src/CasualtiesUnknownOnline.GameAdapter/HarmonyTraverse.cs`, `Run/RunCoordinator.cs`, `src/CasualtiesUnknownOnline.Runtime/Session/World/WorldStateMessageService.cs` |
| So where is the dead end? | In the producer, not in the surface: the session's advance has exactly one producer — the host's own body standing at the layer's bottom and clicking. A host whose character died anywhere else never produces it, and the living members' clicks die locally | the two rows above |

## The authority answer (written down before implementing)

The repository's rule is that the acting side judges its own experience and the host arbitrates conflicts
(`docs/decisions/active.md` 184, `docs/development/agent-reference.md`). Applied here: the member standing
at the layer's bottom judges whether the layer is finished — that is its own experience, and the native
panel already asks it — while the layer itself stays the host's to generate, because the baseline (RNG
state, world-defining fields, rarity multipliers) is captured on the host's boundary and every client
generates from it. So a member's choice is an **intent that drives the host's own advance**, not a new
generation authority: first writer wins, the host executes once per layer, and nothing about who generates
a world changes. The identity that makes "once" checkable is the one every layer-relative report already
carries — the kernel run baseline's generation (run epoch + layer index).

## Design (frozen before implementation)

1. **A member's choice asks the session, and the member does not descend locally.** The Continue click on a
   guest sends one new guest → host `LayerAdvanceRequest` carrying the member's own kernel generation stamp,
   and the local regeneration that click would start is suppressed: the layer's baseline is the host's
   capture, so the member's own regeneration would build a world the session never agreed on AND would flip
   the member's own state to "generating", which is the state in which the session's follow-up (the pull
   back to the menu, then the world entry that re-invites it) refuses to act. The game's own entry body
   still runs — its guard, the panel close, the walk release and the deepest-layer record are native — and
   the one step whose carrier is suppressed, the layer's own progression (`IncreaseDepthByLayer`), is run by
   the adapter instead, once per layer.
2. **The host executes.** On a request from a handshaken member whose stamp IS the host's current
   generation, the host drives the very entry its own click drives — the native guards, the panel close,
   the walk release and the layer's own progression step — minus the entry's local-body clause, which is
   the clause that ties the choice to the body that made it. A request naming another generation (the
   session already moved), or from a non-member, changes nothing.
3. **Once per layer, and only once.** Two members choosing at the same time produce one advance: the
   generation stamp refuses the second request after the host's capture, and the native entry's own
   re-entrancy clauses refuse one that lands inside the advance already running (the fade before the
   capture is exactly that window).
4. **The other producers of a local descent are not touched here**: the drill pod
   (`StartCoroutine("RegenerateWorld", true)`, carrying its own two-layer step and its own `doPod` arrival
   effects) and the debug console's `skiplayer` reach the same coroutine without going through the panel's
   entry, so the sink patch recognizes the panel's click by the entry's own marker and leaves both alone.
   The panel's other button (`SaveAndExit`, which writes the native `save.sv`) belongs to the save half
   (`review/save-layer-end-save-and-restore.md`). The pod and that button have their own tickets
   (`todo/pod-descent-on-a-member-regenerates-locally.md`,
   `todo/layer-end-save-and-exit-native-write.md`); the console command is the game's own debug surface and
   is deliberately untouched.

## Required work

Status after the 2026-10-07 development cycle:

1. **Attribution** — **done statically** (see *The reading* above), and the three-client reading of a DEAD
   host at the boundary is what remains: it needs the real machine (Limits, item 1).
2. **The authority answer** — **done and written down before implementing** (*The authority answer* above).
3. **The reachable half** — **implemented**: the panel was already per-client native UI, so the reachable
   half is the path from a member's click to the session's step: `LayerAdvanceRequest` (guest → host, stamped
   with the member's kernel generation), the host's admission rule (handshaken member + this host's
   generation), the host's drive of the native entry, and the suppression of the member's own local
   regeneration (without it the member leaves the session's follow-up path — see *Design* item 1). **Once per
   layer** is two rules: the stamp refuses a request for a layer this host has left, the native entry's own
   re-entrancy clauses refuse one that lands inside an advance already running.
4. **The failure paths** — a member out of the world at the boundary is out of this ticket's code path (it
   cannot click a panel it does not have) and stays `todo/layer-change-member-recovery.md`; a dead member
   cannot click either, which is why a dead HOST is the case that mattered and it is covered; a late joiner
   is covered by the same admission rule (its stamp, not its join time, decides).
5. **Verification of the user's exact scenario** — **open**: the row needs three clients with a dead host at
   the boundary (Limits, item 1). The machine-verifiable half of this cycle is in the self-check.

## Non-goals

- Not a rework of the save system: `review/save-layer-end-save-and-restore.md` owns that.
- Not host migration: the host stays the host; this ticket is about a step of the run not being reachable when
  one client cannot act. A host PROCESS that is gone is out of scope — the members are pulled to the menu by
  the existing host-left-the-world rule and the session ends.

## What landed (2026-10-07 cycle)

- **Runtime**: `NetMsg.LayerAdvanceRequest = 142` + `LayerAdvanceRequestMsg` (the requester's kernel
  generation stamp), `LayerAdvanceRequestHandler` (direction `GuestToHost`), and
  `LayerAdvanceRequestChannel` behind the new `ILayerAdvanceControl` — the arbitration (handshaken member,
  `WorldReportGeneration.Relate == Current`, every refusal logged with both sides) and the one wire send.
  It is its own control surface for the reason `IWorldTimeControl` is: `WorldService` is at its size gate,
  and this domain stays independently testable.
- **Game Adapter**: `WorldGenerationContinueRunPatch` marks the native entry's own body
  (`InContinueRun`, the same pattern `WorldGenerationUpdatePatch.InUpdate` uses) and
  `WorldGenerationRegenerateWorldPatch` is the sink that every descent in the game passes through — the
  marker is what tells the panel's click from the pod's and the console's own calls, and only the panel's
  click is delegated (`ILayerAdvancePatchPort.TryDelegateLocalAdvance`, reached through
  `PatchBridge.LayerAdvance`, which keeps the hook out of the frozen `IPatchBridge` aggregate). On a guest
  the sink replaces the returned enumerator with a valid, EMPTY one, so no local regeneration starts and the
  member stays on the session's own transition path; the game's entry body itself is untouched (guard, panel
  close, walk release, deepest-layer record). `LayerAdvanceCoordinator` also drives the host's advance on an
  admitted request and grants the member's own descent progression (`IncreaseDepthByLayer`, once per layer);
  `LayerAdvancePolicy` holds both decisions pure (delegate the local descent, may a request drive) so they
  have a test host; `HarmonyTraverse.IsRegenerating()` reads the native entry's fourth guard clause
  (`doingRegen`, the fade before `generatingWorld` turns true).
- **Docs**: the message row in both protocol-messages blocks (pair hashes re-recorded in
  `docs/standard/alignment.txt`), sync-coverage matrix row `R10` + its wire-vocabulary index entry and
  eleven evidence anchors.
- Two producers of a local descent are deliberately NOT routed through this path (filed rather than
  half-fixed): `todo/pod-descent-on-a-member-regenerates-locally.md` and
  `todo/layer-end-save-and-exit-native-write.md`.

## Verification (machine, this cycle)

- `LayerAdvanceRequestTests` (host + two members simulated over the real composed runtime): a member's choice
  reaches the host for the layer the session is in; a choice stamped for a layer the host has left is
  refused; an unstamped choice is refused; a choice from a peer that is not a member is refused; the host's
  own choice travels nowhere.
- `LayerAdvanceCoordinatorTests` (reflective, the adapter's own artifact and its own constructor, the
  Runtime interface as the double): exactly a guest in a live session hands its descent over — one request
  on the control surface and `true` from the delegation — while a host, a solo player and a session-less
  guest keep the game's own descent.
- `LayerAdvancePolicyTests` (reflective): the delegation rule and the host's drive rule including the native
  entry's clauses, the sink's shape (a prefix that replaces only `ref IEnumerator __result`), the marker's
  shape (void prefix/postfix — it never blocks the entry) and both `PatchInventory` contracts
  (`WorldGeneration.ContinueRun`, `WorldGeneration.RegenerateWorld`).
- `NetPacketTests`: the stamp round-trips, and "no stamp" survives as absent rather than as zeros.
- Gates: `PatchBridgePortShapeGateTests` census (the new port + its accessor), `PatchBridgePortContractTests`
  (the compiled artifact implements the port), `GuestToHostDirectionTests` row, sync-coverage gate.
- Mutation controls (each red observed, file restored byte-identically): removing the member check reddens
  the non-member case only; removing the generation check reddens the stale and unstamped cases; dropping
  the re-entrancy clause reddens the `AlreadyAdvancing` cases; making the delegation rule unconditional
  reddens the host/solo/no-session cases.

## Limits

1. **The runtime row this ticket exists for is NOT verified**: a dead host at the layer's end, two living
   members, the group reaching the next layer — that needs three clients and a real machine (the batch is
   named in the handoff). Nothing in this cycle was deployed or run in game.
2. **The member's click is the session's step, not its own descent**: the local regeneration is suppressed,
   so the member's screen shows the panel closing and then the session's own transition (the pull back to
   the menu and the world entry that re-invites it) — the same path every member takes when the host
   advances. Nothing was verified on a screen; the ordering this rests on (nothing generates on the member's
   side, so the session's follow-up can act) is read from the member-side gates, and the three-client row is
   what confirms it in game.
3. **A lost request** costs the member's click and not session state: no regression starts, the native panel
   re-shows while the body is at the layer's bottom (nothing is generating on this side), and the choice can
   be repeated. No resend timer and no hold were added, so a member whose request never lands stays in the
   layer the session is in rather than in a world of its own. The related dead end the batch must watch for:
   a member whose kernel generation is genuinely BEHIND the host's has its click refused and its descent
   suppressed, so that member can move neither the session nor itself until its baseline refreshes — the
   batch that refreshes it is the one the host's own advancement sends. The send itself refuses to let that
   happen silently: a choice that cannot be reported at all keeps the game's own descent (`ILayerAdvanceControl.TrySendLayerAdvanceRequest` answers false and the sink stands down).
4. **The member's descent progression is run by CUO** (`IncreaseDepthByLayer`, latched per local layer),
   because the regeneration that normally carries it is the step being suppressed; the depth and the rarity
   multipliers arrive with the host's baseline at the member's next generation boundary, and the
   `deepestlayer` record stays the native entry's own write.
5. **The pod and the console's `skiplayer`** reach the same coroutine without the entry's marker, so both
   keep the game's own local descent; the pod in particular carries its own two-layer step and its own
   `doPod` arrival effects, which the panel's request does not express. Filed as
   `todo/pod-descent-on-a-member-regenerates-locally.md`.
6. **The native entry's local steps are mirrored on the host** (panel close, walk release, `deepestlayer`
   record, `RegenerateWorld(false)`) because the entry's own local-body clause cannot hold for another
   member's body. Each of those statements IS a typed member reference (`savePanel`, `forceWalk`,
   `RegenerateWorld`, `worldExists`), so a rename breaks the build — what has no compile-time coupling is
   the SET: a game update that adds, drops or reorders a step of that entry must change the host's drive by
   hand.
7. **The panel's other button** (`SaveAndExit`) is untouched and writes the native `save.sv` —
   `todo/layer-end-save-and-exit-native-write.md`.

