# The layer-change member recovery self-check

Owner cycle: `review/layer-change-member-recovery.md` — the world-entry instruction (`WorldJoin`) is a
one-shot EDGE, and a member the host's own world exit pulled to the menu can lose it (the lazy-P2P
swallow, or its own entry gate discarding an invite that arrived while it was still in its old world).
The member then sat in the menu with no local body while the host played (batch `20261007-a`'s
unprotected member was still out at +60 s). This cycle lets the member ask for the instruction again
inside a bounded window, and the host answers with the SAME targeted instruction every other entry uses.

## 1. Mechanism inventory

| # | Mechanism | Evidence |
|---|-----------|----------|
| 1 | The exit that pulls the member | the host's InMenu scene report → `SceneStateHandler` (relay) → `SessionService.FireRemoteSceneChanged` → `RunCoordinator.OnRemoteSceneChanged` (`_phase = Idle; _menuReturn.Request(..., HostPull, _inWorld)`) → `SaveCutSeam.FlushMenuReturn` → `RunMenuReturnCoordinator.Leave` → `PlayerCamera.ToMainMenu()` (`reversing/Assembly-CSharp/Assembly-CSharp/PlayerCamera.cs:613`, one-line `LoadScene("PreGen")`) |
| 2 | The entry instruction it loses | host senders: `WorldService.SendWorldJoin` (the click, and the host's `InWorld` edge in `RunCoordinator.UpdateSceneState`) and `SendWorldJoinTo`; receiver: `WorldJoinHandler` → `RunCoordinator.OnWorldJoin` → `TryStartWorldJoin` → `PreRunScript.instance.StartRun()` (`reversing/Assembly-CSharp/Assembly-CSharp/PreRunScript.cs:273`) |
| 3 | The member's ask | `NetMsg.ReentryRequest` = 156 (`GuestToHost`, no payload, reliable) + `ReentryRequestChannel` (`TrySendReentryRequest` reports the transport's verdict; the host admits a handshaken member its presence table holds OUT of the world, outside a 2 s per-sender cadence that also bounds every refusal) |
| 4 | The window | `ReentryWindow` (pure): 12 asks at 5 s inside a 180 s lifetime; armed only when the host's exit takes this member out of a world it is in, entering or generating; "in world" counts as a return only after the window has seen this member go out; a backwards clock restarts the window |
| 5 | The host's answer | `ReentryCoordinator.OnReentryRequested` → `ReentryPolicy` (this side is the host and the session is active; a live world; a committed kernel run baseline) → `IWorldControl.SendWorldJoinTo` (the targeted `WorldJoin` + the run-baseline checkpoint) |
| 6 | The baseline fact | `WorldService.HasCommittedRunBaseline` reads the same `KernelWorldGenerationSource` the instruction's `RunBaselineFollows` promise reads — not the published params |
| 7 | The wire surface | `ReentryRequest = 156`; the report-validation census row (Request: handshaken + the host's presence table + the cadence + the answer's world facts); sync-coverage matrix row R11 (9 anchors); `ProtocolVersion.Current` untouched pre-release (decision 241) |

## 2. Self-check table (mechanism × change × evidence)

| # | Claim | Evidence |
|---|-------|----------|
| 1 | The lost instruction is the gate, not a body the game never rebuilt | batch `20261007-a`'s park experiment (held inbound keeps the member in the world; unheld drops it) and batch `20261025-a`'s red: the recipe `net-drop-message` dropped exactly one `WorldJoin` (`dropped: 1`), the host logged ONE `World join sent to 2 member(s)` (20:17:49.104), the guest's log carries the host exit and no `World join received`, and at +80 s and +112 s `state.inWorld` was false with `container-read mode=local` → `no-local-body`, while the control member (invite delivered) returned with its two carried items |
| 2 | The member asks again, and only when the host's exit took it out | `ReentryWindowTests.TheArmingFrame_DoesNotCloseTheWindow_TheMemberIsStillInItsOldWorld` (the review's B1), `...AMemberAlreadyInTheMenu_DoesNotArm`, `...AMemberOnItsWayIn_DoesNotAsk`, `...TheAskCadence_IsFiveSeconds` |
| 3 | The host answers with the ordinary instruction | `ReentryRequestTests.AnAdmittedAsk_IsAnsweredWithTheOrdinaryEntryInstruction` (the answer seam sends the targeted `WorldJoin`), `ReentryPolicyTests.Decide_AnswersOnlyWithALiveWorldAndARunBaseline` |
| 4 | A member the host holds IN the world is refused and settled by its scene re-report | `ReentryRequestTests.AnAskTheHostHoldsInWorld_IsRefused` |
| 5 | A peer that is not a handshaken member is refused | `ReentryRequestTests.AnAskFromAPeerThatIsNotAMember_IsRefused` |
| 6 | The cadence bounds both the answer and every refusal | `ReentryRequestTests.AnAskInsideTheCooldown_IsDroppedAndOneAfterIt_IsAdmitted` (and the channel's `lastMs <= nowMs` wrap guard) |
| 7 | A member back in the world stops asking | `ReentryWindowTests.AMemberBackInTheWorld_ClosesTheWindow_ButOnlyAfterItWasOut` |
| 8 | The window has a deadline and a budget, and the clock wrap cannot stall it | `...AWindowThatNeverGetsAWorldEntry_ClosesAtItsDeadline`, `...TheWindowSpendsABoundedNumberOfAsks_AndThenClosesByName`, `...TheClockGoingBackwards_RestartsTheWindowInsteadOfStalling` |
| 9 | Nothing else regressed | full solution: `dotnet format` 0; build 0 warnings 0 errors; normative gates 751/751; behaviour suite 6092/6092 (batch `20261025-a`'s logs) |

## 3. The independent review's findings, and what this cycle did with them

The frozen-tree review returned FINDINGS; every one was fixed in the same change rather than recorded:

- **B1 (blocker)** — the window disarmed on the very frame it was armed (the member is still in its old
  world while its menu return waits at the frame-end seam), so the recovery never asked in the target
  scenario. Fixed by the `_wasOut` latch and the arm condition; pinned by the frame-sequence test.
- **M1 (major)** — no test touched the window, and the Runtime suite's answer test wired the answer's
  own lambda. The window is now a pure `ReentryWindow` with its own test class, and the Runtime test's
  doc says exactly what it proves.
- **m2** — `TrySendReentryRequest` now reports the transport's verdict (`PacketSender.TrySend`), so a
  refused link does not spend the window. **m3** — the per-sender cadence now runs BEFORE every refusal
  branch, so an in-world member's every-frame asks cost one line per window. **m4** — the arm condition
  implements "the host's exit is what took this member out" that the ticket and decision 266 claimed.
  **m5** — the answer's precondition is the committed kernel run baseline, the fact the instruction's
  promise reads. **m6** — the recipe's comment about the old blackout corrected. **m7** — the TickCount
  wrap restarts the window instead of stalling it.

## 4. Limits

- **A member stuck reporting `IsGenerating` is out of this window's reach.** The window only acts on a
  member that is out of the world; whether a generation can outlive its own transition was not read.
- **The adapter coordinator's Unity-side wiring is not unit-tested.** Its decision (`ReentryPolicy`), its
  window (`ReentryWindow`) and the Runtime channel are; its own call into `SendWorldJoinTo` is judged by
  batch `20261025-a`'s green half (the acceptance record names it when it runs).
- **The green half of batch `20261025-a` was still pending when this sheet was written**: the red is
  recorded above, and the acceptance record is the final verdict.
