# The End-of-Layer Choice for Members — Self-Check (2026-10-07)

Ticket: `docs/backlog/review/layer-complete-choice-for-members.md` (moved from `todo/` with this cycle).
This cycle is the development half; the ticket's runtime row (a dead host at the boundary, two living
members) needs three clients and is named in §4.

## 1. Mechanism inventory

Every mechanism this change touches, with the evidence that was read before writing code (the game's lines
are the frozen `reversing/` tree; our own files are quoted, never line-numbered).

| # | Mechanism | Evidence |
|---|---|---|
| 1 | The end-of-layer choice is the native scene panel `WorldGeneration.savePanel`, whose two entries are `ContinueRun` and `SaveAndExit` | `WorldGeneration.cs:1011-1030`; neither method has a caller anywhere in the decompiled assembly — the panel's own wiring is the only producer |
| 2 | The panel appears on the client whose OWN body stands at the layer's bottom, and freezes it (`forceWalk = true`) | `WorldGeneration.Update`, `WorldGeneration.cs:979-983` (`PlayerCamera.main.body.transform.position.y`); CUO never wrote the panel before this cycle — the only `savePanel` write under `src/` is the host drive this change adds |
| 3 | `ContinueRun` re-checks the same clauses, then runs the whole descent locally | `WorldGeneration.cs:1013-1018` (`doingRegen`, `generatingWorld`, `worldExists`, the body's position, then `StartCoroutine(RegenerateWorld(false))`) |
| 4 | The layer a session enters is the HOST's capture | `WorldParamsService.CaptureAtBoundary` is reached only from the host/solo branch of `RunCoordinator.OnWorldGenerate`; `WorldService.PublishWorldParams` commits the kernel's `AdvanceLayerCommand` from that capture (`WorldRunProjection.CommitBaseline`) |
| 5 | A member's own regeneration has nothing new to apply: the params instance in its hand is the layer it is leaving | `WorldParamsService.EnsureGuestApplied` — "Idempotent per params instance — a layer switch delivers a new instance and re-applies", and the reference check `if (!ReferenceEquals(_appliedWorldParams, parameters))` only fires for a NEW instance |
| 6 | The identity that makes "once per layer" checkable already exists for every world report | `WorldReportGeneration.Relate(current, reported)` (Current / Stale / Unknown) and `KernelWorldGenerationSource.Current`, fed by the kernel run baseline both sides hold |
| 7 | The transition a member takes when the host advances (so the new path does not invent one) | `HarmonyTraverse.HasLiveWorld` goes false while the host generates → the member's `OnRemoteSceneChanged` requests `RunMenuReturnOrigin.HostPull` → the host's world-entry edge calls `SendWorldJoin`, which skips members whose `InWorld` is true |
| 8 | The native entry's fourth guard clause is the one that ties the choice to the body that made it | `WorldGeneration.cs:1013` (the `PlayerCamera.main.body.transform.position.y < -halfHeight + 3.1` clause); the other three are `doingRegen`, `generatingWorld`, `worldExists` |
| 9 | The same sink has a second producer (the drill pod) with its own two-layer step and its own arrival effects | `DrillPod.cs:20-32` (`doPod = true`, `StartCoroutine("RegenerateWorld", true)`), read at `WorldGeneration.cs:3611-3618`; filed as `todo/pod-descent-on-a-member-regenerates-locally.md` |
| 10 | The panel's other entry reaches a native writer this repository believed unreachable | `WorldGeneration.SaveAndExit` → `SaveSystem.SaveGame()` (`WorldGeneration.cs:1023-1030`), no caller in the assembly; filed as `todo/layer-end-save-and-exit-native-write.md` |
| 11 | The patch seam's house pattern for a new domain port | `IFluidPatchPort` + `PatchBridge.Fluid` + the frozen aggregate note in `IPatchBridge`; pinned by `PatchBridgePortShapeGateTests` and, on the compiled artifact, `PatchBridgePortContractTests` |
| 12 | A large Runtime service stays untouched by putting a domain on its own control interface | `IWorldTimeControl`'s own doc: "Separate from IWorldControl so the already-large world service stays untouched and the time domain stays independently testable" |
| 13 | Suppressing the member's local descent is what keeps it on the session's follow-up path (the review's blocker) | three member-side gates key on the member's OWN state, which its own regeneration flips: `RunMenuReturnCoordinator.Request` → `RunMenuReturnPolicy.Decide(role, inWorld)` answers `None` (the request is dropped, no retry) when the member is not in a world; `WorldStateMessageService.SendWorldJoin` skips `member.InWorld`; `RunCoordinator.TryStartWorldJoin` drops a join that arrives while `HarmonyTraverse.IsGenerating()`. A member that regenerates locally is, in that window, "not in the world" and then "in its own world", so none of the three acts on it |
| 14 | The panel's click is distinguishable from the pod's and the console's own descents at the sink | `WorldGenerationContinueRunPatch` marks the entry's own body (`InContinueRun`, the pattern `WorldGenerationUpdatePatch.InUpdate` establishes) and `WorldGenerationRegenerateWorldPatch` is the prefix on `RegenerateWorld`, whose callers are exactly three: `WorldGeneration.cs:1018` (the panel's entry), `DrillPod.cs:29` (by name, `twice`) and `ConsoleScript.cs:721` (`skiplayer`) |

## 2. The change

**Runtime**

- `Protocol/NetMsg.cs`: `LayerAdvanceRequest = 142` (guest → host) with the authority note above it.
- `Protocol/Messages/LayerAdvanceRequestMsg.cs` (new): the requester's `WorldGenerationMsg` stamp.
- `Session/Handlers/LayerAdvanceRequestHandler.cs` (new): `[PacketHandler(GuestToHost)]` into the new
  `ILayerAdvanceHandlerContext`.
- `Session/World/ILayerAdvanceControl.cs` (new) + `Session/World/LayerAdvanceRequestChannel.cs` (new): the
  send, the admission rule (handshaken member, stamp == this host's generation) and the admitted event.
- `Session/HandlerContext.cs`, `Session/HandlerContexts/ILayerAdvanceHandlerContext.cs` (new),
  `Composition/WorldComposition.cs`, `Composition/NetworkingComposition.PacketPlane.cs`: the control's DI
  registration and the handler context member.

**Game Adapter**

- `Patches/WorldGenerationContinueRunPatch.cs` (new): the marker on the native entry — a void prefix/postfix
  pair that never blocks the entry, so its guard, panel close, walk release and deepest-layer record all
  stay the game's.
- `Patches/WorldGenerationRegenerateWorldPatch.cs` (new): the descent sink. It delegates only when the
  marker says the call came from the panel's entry AND the port says this client is a guest in a live
  session, and then replaces the returned enumerator with a valid, EMPTY one — the pod's and the console's
  own descents keep the game's path.
- `ILayerAdvancePatchPort.cs` (new) + `PatchBridge.cs` + `GameAdapterBridge.cs`: the port (`TryDelegateLocalAdvance`)
  and its one door (`PatchBridge.LayerAdvance`), so the hook does not widen the frozen aggregate.
- `World/LayerAdvanceCoordinator.cs` (new): the guest's delegation (the request plus the member's own
  descent progression, latched per layer) and the host's drive of the native entry.
- `World/LayerAdvancePolicy.cs` (new) + `World/LayerAdvanceDecision.cs` (new): both decisions pure.
- `HarmonyTraverse.cs`: `IsRegenerating()` (the native entry's `doingRegen` clause).
- `Run/RunCoordinator.cs`: the comment on the join-while-generating guard, which said a member's layer switch
  is never the host's instruction — no longer true.
- `GameAdapter.cs`, `GameAdapterDomains.cs`, `GameAdapterSessionBinding.cs`,
  `Capabilities/AdapterCapabilityCatalog.cs`: the injection, the field, the session bind and the capability
  home for both new patch classes.

**Tests and docs**

- `tests/.../World/LayerAdvanceRequestTests.cs` (new), `tests/.../Patching/LayerAdvanceCoordinatorTests.cs`
  (new), `tests/.../Patching/LayerAdvancePolicyTests.cs` (new), `NetPacketTests` (two cases),
  `GuestToHostDirectionTests` (one row),
  `PatchBridgePortShapeGateTests` (the new seam's census; its two pins now derive the port list instead of
  naming the fluid port), `PatchBridgePortContractTests` (the compiled half of the same split).
- `docs/en|zh/reference/protocol-messages.md` + `docs/standard/alignment.txt` (pair hashes re-recorded),
  `docs/evidence/sync-coverage-matrix.md` (row `R10`, the vocabulary index row, the verdict summary) and
  `docs/evidence/sync-coverage-evidence.json` (eleven anchors, `count` 1031 → 1042).

## 3. Verification

| # | Claim | How it is proven |
|---|---|---|
| 1 | A member's choice reaches the host for the layer the session is in | `LayerAdvanceRequestTests.AMembersChoice_ReachesTheHostForTheLayerTheSessionIsIn` — the real composed runtime, a host and two members over the fake transport: the guest's send arrives as the host's admitted event with that member as its sender |
| 2 | A request naming a layer this host has left changes nothing | `AChoiceForALayerTheHostHasLeft_IsRefused` — the frame is delivered with an explicit older stamp |
| 3 | A request with no generation stamp changes nothing | `AChoiceWithNoGenerationStamp_IsRefused` |
| 4 | A request from a peer that is not a member changes nothing | `AChoiceFromAPeerThatIsNotAMember_IsRefused` — the frame is delivered from a stranger id; the mutation control below proves the frame really arrives and the member check is what refuses it |
| 5 | The host's own choice produces no request | `TheHostsOwnChoice_TravelsNowhere` |
| 6 | Exactly a guest in a live session hands its descent over — one request on the control surface and `true` from the delegation; a choice that could not be reported keeps the game's own descent | `LayerAdvanceCoordinatorTests.TryDelegateLocalAdvance_HandsTheDescentOverExactlyForAGuestInALiveSession` and `_WhenNothingWasReported_KeepsTheGameDescend` — the adapter's own coordinator built through its own constructor with the Runtime interface as the double |
| 7 | The delegation rule is "a guest in a live session, and nothing else" | `LayerAdvancePolicyTests.ShouldDelegateLocalAdvance_OnlyForAGuestInALiveSession` (guest/host/solo/no-session rows) |
| 8 | The host's drive rule is the native entry's own clauses, minus its local-body clause | `DecideDrive_UsesTheNativeEntrysOwnClauses` (idle, `doingRegen`, `generatingWorld`, no world) + `DecideDrive_RefusesASideThatIsNotThisHost` |
| 9 | The sink replaces ONLY the returned enumerator, and every refusal (not the panel's click; the port refused; no session) keeps the game's own descent | `TheDescentSink_ReplacesOnlyTheReturnedEnumerator` (`ref IEnumerator __result`, a bool prefix) + `TheDescentSink_WithoutASession_LetsTheGameDescend` (the marker off, then the marker on with no bound bridge) + `TheChoiceMarker_MarksWithoutBlocking` + `ShouldSuppressLocalDescent_NeedsBothThePanelEntryAndAnAcceptedDelegation` (the decision itself, four rows) |
| 10 | Every patch target resolves against the real game assembly | the repository-wide `PatchContractTests` (every `[HarmonyPatch]` class, the two new ones included) + `PatchInventory_ContainsTheContinueRunContract` for the entry's own row |
| 11 | The new wire message is fail-closed on direction and registered | `GuestToHostDirectionTests` row (accepted on the host, dropped on a guest) + `NetMessageRegistryTests` (every `NetMsg` registered) |
| 12 | The stamp survives the wire, and "no stamp" survives as absent | `NetPacketTests.LayerAdvanceRequest_RoundTripsTheGenerationStamp` / `_WithoutAStamp_DecodesAsNull` |
| 13 | The new port is a door of its own, not a widening of the frozen aggregate | `PatchBridgePortShapeGateTests` (aggregate census unchanged, the new seam's own census pinned, the seam's accessor list pinned, non-composed ports share no member name with the aggregate) and `PatchBridgePortContractTests` on the compiled adapter |
| 14 | Every wire member is indexed with an owning row and evidence | `SyncCoverageGateTests` — the vocabulary index row points at `R10`, which mentions `LayerAdvanceRequest`, declares eleven anchors and carries verdict `OK`; the evidence file's quotes are re-read from the sources |
| 15 | The two docs blocks agree with each other | `DocumentationTreeGateTests.EveryPair_IsRecordedAtItsCurrentContents` after re-recording the pair hashes in `docs/standard/alignment.txt` |
| 16 | The tests discriminate (no green-for-the-wrong-reason) | Four mutation controls, each red observed and each file restored byte-identically (SHA-256 re-checked): (a) the member check skipped → only the non-member case red (1 of 5); (b) the generation check skipped → the stale and the unstamped cases red (2 of 5); (c) the re-entrancy clause dropped → the two `AlreadyAdvancing` rows red (2 of 15); (d) the delegation rule made unconditional → the host/solo/no-session rows red in both the policy suite and the coordinator suite (3 of 4 rows each) |
| 17 | The change leaves the architecture gates green | `dotnet build` (0 warnings, 0 errors) + `dotnet format` (exit 0) + the gate project 451/451 (the `WorldService` size gate is why the new domain got its own control interface instead of three forwarders, and the `RunCoordinator` debt entry is why its comment had to stay within the file's registered length) |

## 4. Limits

1. **The ticket's runtime row is NOT verified by this cycle**: a dead host at the layer's end, two living
   members, the group reaching the next layer. That needs three clients on the real machine; nothing here
   was deployed or run in game, and the deployed plugin still predates this commit.
2. **No in-game evidence exists for the panel's own visibility** — §1 rows 1-3 read the native condition,
   not a screen. The engine-side facts are native code; the reading is a batch's.
3. **The member's click is the session's step, not its own descent** (by design, after the review's
   blocker): the local regeneration is suppressed, so the member stays on the path the session's own
   follow-up acts on (§1 row 13) and its screen shows the panel closing, then the session's transition. The
   ordering that rests on (nothing generating on the member's side while the host's layer change is in
   flight) is read from the member-side gates, not observed in game.
4. **The host's drive mirrors four native statements** (the walk release, the panel close, the
   `deepestlayer` record and the `RegenerateWorld(false)` call), because the native entry's local-body
   clause cannot hold for another member's body. Each statement is a typed member reference, so a rename
   breaks the build — but the SET has no compile-time coupling: a game update that adds, drops or reorders a
   step of that entry must change the host's drive by hand.
5. **The member's descent progression is run by CUO** (`IncreaseDepthByLayer`, latched per local layer)
   because the regeneration that normally carries it is suppressed; the depth and the rarity multipliers
   arrive with the host's baseline at the member's next generation boundary. The latch's key is the local
   layer depth, so a member that somehow never leaves a layer cannot be granted twice.
6. **The pod and the console's `skiplayer` are out of scope and unchanged** (the marker keeps them off this
   path); a member's pod descent therefore still diverges, exactly as before this cycle, and is filed.
7. **A lost request has no retry**: nothing regenerates on the member's side, so the native panel re-shows
   and the player can click again; the member stays in the layer the session is in rather than in a world of
   its own. No bounded wait, no resend timer and no new state machine was added. The dead end to watch for in
   a batch: a member whose kernel generation is genuinely behind the host's has its click refused AND its
   descent suppressed, so it can move neither the session nor itself until its baseline refreshes; and a
   choice that cannot be reported at all does NOT suppress the descent (`TrySendLayerAdvanceRequest` answers
   false and the sink stands down), which is the conservative side of that gap.
8. **The arbitration's second half is the native guard**: the Runtime refuses a request whose stamp is not
   this host's generation, and the adapter refuses one that lands while the world is already advancing.
   The first is proven in the test host, the second only as a pure decision (`LayerAdvancePolicyTests`) —
   the coroutine's own timing (the fade before the capture) is a runtime fact.
9. **The reference mod ships the same shape**: `reversing/KrokMP`'s `ContinueRun` patch also blocks a
   member's local descent (a "wait for the host" outcome) — the review found it, and it agrees with this
   cycle's fix rather than with the first draft's "let the member descend and be superseded".
