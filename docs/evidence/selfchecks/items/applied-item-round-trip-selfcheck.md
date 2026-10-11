# An applied item keeps its id — stage 2 self-check (2026-10-13)

Ticket: `docs/backlog/todo/item-identity-across-transforms.md`. Stage 1's page is
`docs/evidence/selfchecks/items/applied-item-location-selfcheck.md`; the design record the ruling rests on
is `docs/evidence/selfchecks/items/item-identity-across-transforms-design.md`. **Scope of this cycle:
stage 2 of the ruled option 1** — the PRODUCTION call sites that create and end the applied fact (the
adapter's apply and removal commits, cross-player and local), the limb-component binding with its
divergence report at the restore seam, and the deletion of the synthesized id namespace that the removal
no longer needs. The mod-facing page and the by-id read are stage 3. The rule is now true in a running
game: the four paths that used to answer "what happened to this item" differently answer with one
relocation.

Citations: `reversing/` paths carry line numbers (that tree is never edited); our own `src/` and `docs/`
are cited by path plus quoted text.

## 1. What landed

| Artifact | Change |
|---|---|
| `WireCommandKind` / `WirePayloadType` | `ItemApplyLimb = 27`, `ItemRemoveLimb = 28` — the applied family's member half. |
| `KernelLimbItemWireMapper` | Both directions' operand parsing, split out of `KernelWireMapper` at the 600-line gate: the limb rides the applied LOCATION a committed row already uses, and a limb-less or foreign-family location is refused through the same `FromWireLocation` guard. |
| `ProtocolFrameValidator` | The two payload types map to their command kinds — without the mapping the frame is refused as a protocol violation and the report never lands. |
| `KernelCommandGateway` | Two eligibility rules: a member may install an item on its OWN limb only when the item is its own carried one (a re-assertion of the same applied fact is admitted), and may take a component off its OWN limb only when the item really is applied there and the destination is that same member. Both are `Ignore`, the destroy rule's own shape. |
| `KernelProtocolCommandHandler` | Both kinds are item operations (creation-before-operation) and both resolve their revision on the host, so a client's stale view cannot fail the report. |
| `ItemKernelAuthority` | `TryApplyLimb`, `TryRemoveLimb`, and `FindAppliedLimbItem` — the `(owner, limb, definition)` lookup every removal gesture resolves its id through. |
| `ItemProjection` | `ApplyLimbItem` / `ApplyLimbItemRemoval`, the host's own-observation entry points; neither writes the world projection beyond dropping a stale world row. |
| `IItemControl` / `ItemService` / `ItemMessageFlowService` | `SendItemAppliedToLimb`, `SendItemRemovedFromLimb` (host records through the kernel, guest reports the command) and `FindAppliedLimbItem` (the read the take-off needs). |
| `ItemActionGrants` | The limb rides the ADMISSION rather than the report: the host sent that number in the use result, the affected side resolves exactly it or refuses, so echoing it back would give one fact two carriers. |
| `PlayerItemActionOutcomeMsg` | `BecameLimbComponent` — the affected side's observation that the delegate did not merely end the item: it installed it on the limb. |
| `PlayerItemUseCommit` / `PlayerItemActionOutcomeService` | `CommitItemAppliedToLimb`: the owner's inventory loses the item exactly as a consumed one's does, and the kernel row RELOCATES instead of going terminal. |
| `OtherMedicalRemovalApplier` | The cross-player removal resolves the applied row by `(target, limb, template)`, MOVES it to the operator (same id), and refuses by name — before anything is cleared — when the limb carries a component no row names. The synthesized-id mint and its `ItemIdCoordinator` dependency are gone. |
| `ItemArbitration` / `IItemControl` | `RegisterTransferredItem`: the removal's hand-over records the operator's transfer-table row WITHOUT issuing a second kernel relocation, because the row is already carried by them. |
| `LimbItemTransform` (new) | The two native transforms observed on the client that performs them: the local apply (report + marker) and the take-off (id resolution, stamping, report). |
| `LimbItemTransformPatches` (new) | `PlayerCamera.ApplyWoundItem` (before/after component comparison, per type), `SplintLimb.TakeOff` and `TourniquetScript.TakeOff` (prefix/postfix `__state`). Both postfixes ask `__runOriginal`: a prefix that skips the original still leaves a postfix running, so an apply that never ran reports nothing and a take-off that never ran drops its armed record. |
| `AppliedItemConditionProjection` (new) | The ONE writer of an applied row's durability after the apply: the body's own 1 Hz character capture, which already reads the limb component's live `condition` (the design's §6.1 seam). The row follows in steps of 0.01, so a slow decay costs one command per step rather than one per report. |
| `LimbAppliedItemOrigin` (new) | The marker that keeps the generic `Item.OnDestroy` hook from reporting an apply's deferred destroy as the item's end. |
| `ItemPatches` | The `Item.Awake` hook adopts the take-off's id (the one moment the object exists inside the native call); `Item.OnDestroy` stays silent for a marked apply. |
| `RestoredAppliedItemAudit` (new) | The restore seam's read-only comparison of the two carriers, both directions, and its named divergence. |
| `WorldCharacterBinder` / `WorldCharacterBindResult` | The bound (peer, character) pairs are returned, because `OwnerSteamId` is not the binder's to assume and the audit needs the owner. |
| `ItemIdCoordinator` / `ItemInstanceIds` | The synthesized namespace is DELETED: `NextAwardedItemId`, its counter, `ResumeAwardedIds`, `AwardedHighBit`, `IsAwarded`, `AwardedCounterOf`. The resume rule now describes one namespace. |
| `docs/architecture/save-archive-format.md` | §6.1's synthesized-namespace paragraph replaced by the one-namespace rule and by the two-carrier divergence the restore performs. |
| `docs/evidence/sync-coverage-matrix.md` | Row I1 gains both wire members and the applied family's sentence; row I8's synthesized half is gone; the vocabulary index gains both members. |
| `docs/decisions/active.md` | Decision 259 — the ruling, its boundary, its two directions and the mechanism it deleted. |

## 2. The four paths, now one commit each

The stage-1 page's §4 mapped the two kernel commands to the native call sites and noted that neither had a
production caller. This cycle gives each its caller, and the two halves that used to disagree
(`Consumed` cross-player vs `Destroyed` local; a synthesized mint cross-player vs the plain allocator
local) now commit the same relocation:

| Path | Entry (native call site) | What commits | Evidence |
|---|---|---|---|
| Cross-player apply | the affected body's own `useLimbAction` (`NativeLimbToolApply`, entered from `PlayerCamera.ApplyWoundItem`'s counterpart on the side the effect lands on) | the outcome report carries `BecameLimbComponent`, and the host relocates the owner's row onto the limb the admission named | `MedicalToolApplicationTests.AffectedSideOutcome_ComponentToolThatBecameTheComponent_KeepsTheIdAndInstallsItOnTheLimb` |
| Local apply | `PlayerCamera.ApplyWoundItem` (`PlayerCamera.cs:739-763`, the choke point both branches enter) | the applying client reports `ItemApplyLimb`; host/solo records it directly, a guest's command is judged by the gateway | `LimbItemTransformSessionTests.TheHostInstallingItsOwnItemOnItsLimb_RelocatesTheRowWithoutMintingAnId`, `.AGuestsReportOfItsOwnLimbInstall_ReachesTheHostsKernelAndKeepsTheId` |
| Cross-player removal | `OtherMedicalOperationSession` → `OtherMedicalRemovalApplier` | the applied row MOVES to the operator, the same id, and the same id reaches the operator's character file | `MedicalOperationOtherActionsSessionTests.SplintRemoval_GivesItemToOperatorAndClearsLimb`, `.SplintRemoval_HostOnGuest_AwardsItemToHostOperator`, `MedicalOperationConcurrencyTests.SplintRemoval_TwoOperators_TheItemIsAwardedOnce` |
| Local removal | `SplintLimb.TakeOff` / `TourniquetScript.TakeOff` (`SplintLimb.cs:19-26`, `TourniquetScript.cs:25-35`) | the component's own object wears the applied id (stamped in `Item.Awake`) and the client reports `ItemRemoveLimb` | the REPORT this path sends is pinned end to end (`LimbItemTransformSessionTests.AGuestsReportOfItsOwnLimbInstall_ReachesTheHostsKernelAndKeepsTheId`, and the restored-world round trip in `ItemIdCoordinatorTests`); the patch that decides to send it is native-facing and has no case that can run without the game (see §5) |

What the adapter deliberately does NOT observe: the two `TakeOff` callers it does not hook are the
component's own decay (`SplintLimb.cs:37-41`) and `OnDismember` — both go THROUGH `TakeOff`, so both are
covered by the same patch, which is why decay to zero now reads as the item's own `Destroyed` end after an
honest relocation rather than as a mint followed by an immediate destroy. What "the item no longer ends"
therefore does NOT mean: a splint that decays to zero still leaves the player's hands within two frames
(the relocation lands, then the item's own zero-condition update destroys it) — the difference is that the
id, the location and the mod-visible history are honest for those two frames instead of a tombstone.

The item's DURABILITY after the apply comes from the body's own 1 Hz capture
(`AppliedItemConditionProjection`), which is the design's §6.1 seam: without it the row would keep
whatever the apply wrote while the component decayed, and a mod reading the item — or a later archive —
would see a splint frozen at its installed condition. What the removal HANDS OVER stays the affected
side's live component, which is decision 184's rule unchanged.

## 3. The limb-component binding, and where a divergence is named

The item row says WHICH item is installed; the limb row says the component is there; the two are bound by
`(owner, limb, definition)`, so one limb legally carrying a splint and a tourniquet at once is two facts
(`RestoredAppliedItemAuditTests.ADifferentLimbOrDefinitionOnTheSameBody_IsNotTheSamePair`). Three places
read both, and each names rather than guesses:

- the restore (`RestoredAppliedItemAudit`, after the kernel restore and after the characters are bound —
  the one moment both carriers are complete): a component with no row and a row with no component are each
  logged by owner, limb and definition, and NOTHING is moved or ended;
- the cross-player removal (`OtherMedicalRemovalApplier`): resolves the row first and refuses, leaving the
  component, the row and the operator exactly as they were
  (`MedicalOperationOtherActionsSessionTests.SplintRemoval_WithNoAppliedRow_IsRefusedAndTheComponentStays`);
- the local take-off (`LimbItemTransform.OnLimbComponentTakeOffBegin`): resolves the row before the native
  call creates the object, logs the divergence and lets the object take an ordinary new id — the honest
  answer for a component CUO never saw installed, never an invented id.

## 4. Entry mapping and mechanism inventory

No new CUO gesture, UI or player-visible behaviour is introduced, and every entry is a native call site the
design record already mapped. What a player sees changes in one way the ruling asked for: applying a
splint is no longer the item's end, so a mod (and the favourite-mark ledger) can follow the id onto the
limb and back.

- `OnItemAppliedToLimb` → `PlayerCamera.ApplyWoundItem`'s postfix. `OnLimbComponentTakeOffBegin/End` →
  `SplintLimb.TakeOff` / `TourniquetScript.TakeOff`'s prefix/postfix. `TryAdoptLimbRemovalItem` →
  `Item.Awake`.
- Mechanisms ADDED, each with its reader: the two wire commands (read by the host's command handler and
  gateway), `FindAppliedLimbItem` (read by both removal gestures), `RegisterTransferredItem` (read by the
  reconnect merge and arbitration), the restore audit (read by the operator's log — its verdict reaches no
  player surface, deliberately: neither carrier is authoritative enough to change the world).
- Mechanisms DELETED: the synthesized id namespace and its resume half, and with them the last host-side
  mint.

## 5. What this cycle does NOT prove

- **The adapter's Unity-facing decision code has no case that can run here, and that is a wider set than
  "the patch bodies".** `LimbItemTransform`'s three entry points all take `Item`, `Limb` or `GameObject`,
  which no test host can construct (the game assembly is a reference, not a runtime), so what is unproven
  is the whole of that class: the three refusal branches, the marker attachment, the pending-take-off state
  machine and the `Item.Awake` stamping — not merely the Harmony patch bodies that call them. What IS
  pinned is everything the class REPORTS (both wire commands through the real host path, in
  `LimbItemTransformSessionTests`), and the runtime half of the class is a named residual rather than a
  claim.
- **The local take-off has one race, and it is named rather than papered over.** The id comes from the
  applying client's own kernel, which learns the applied row from the host's committed batch: a take-off
  within one round trip of the apply finds no row and is reported as the divergence above (the new item
  takes a fresh id). The alternative — remembering the id locally between the two gestures — was rejected
  as a second carrier of an identity the item row owns, and it would disagree with the kernel silently.
- **No two-client run has observed the round trip yet.** The end-to-end halves run in the simulation
  harness (guest report → wire → gateway → kernel → batch), and the mod-facing answer a real session would
  give is stage 3's. The ticket carries no acceptance row.
- **The deferred destroy is still the seam's reason to exist.** The apply reports BEFORE Unity's
  `Object.Destroy` lands, and the marker is what keeps the generic hook silent; if a future cycle changes
  when the delegate destroys its object, the marker — not the report — is what breaks first.
- **The applied row's durability follows the component in STEPS, not continuously.** The 1 Hz capture
  writes it only once the component has moved by 0.01, so the row trails the live component by up to that
  step; the removal is unaffected (it hands over the live component), and what trails is what a mod reads
  and what an archive stores.
- **A peer's clone of an applied item is not removed by an event.** `KernelBatchItemProjection` emits no
  carried fact for `Applied`, so the clone converges from the owner's 1 Hz character snapshot exactly as a
  consumed item's does (the design record's §3 model), and a viewer may log the pre-existing "left the
  inventory without an event sync" divergence. Unchanged by this cycle, and now the applied family's
  documented behaviour.
- **An in-flight container report can still claim an applied item** (`DecideSyncContainer` relocates any
  child id its report names). The class is not new — the same report can claim an item another player now
  carries — and it stays with `todo/id-carriers-can-disagree-about-placement.md`.
- **A wire checkpoint whose applied row names no limb still fails the whole chunk set**, as every unknown
  location kind does on that path; the per-entry NAMED skip is the archive's own rule. Stage 1 recorded
  it; stage 2 did not change that path.
- **The restore audit's verdict is a log line.** It reaches no restore report and no player surface: the
  damage account is for losses a player would otherwise never be told about, and a divergence between two
  archive carriers is an operator-level fact until a cycle decides which carrier wins (that decision is
  `todo/id-carriers-can-disagree-about-placement.md`'s).
- **No protocol number moved**, deliberately: `ProtocolVersion.Current` is frozen at `UnreleasedBaseline`
  before the first release (decision 241), so these two wire members are recorded by this commit, this
  page and decision 259, not by a number.

## 6. Verification as run

- Focused: the new and rewritten cases across `KernelWireMapperTests`, `KernelCommandGatewayTests`,
  `MedicalToolApplicationTests`, `MedicalOperationOtherActionsSessionTests`,
  `MedicalOperationConcurrencyTests`, `ItemIdCoordinatorTests`, `LimbItemTransformSessionTests`,
  `RestoredAppliedItemAuditTests` and `AppliedItemConditionProjectionTests` — the cycle adds 30 cases to
  the 4990 it started from.
- Full: `dotnet test CasualtiesUnknownOnline.slnx` green at gates 615/615 and behaviour 5020/5020 on the
  frozen tree, plus `dotnet format` clean.
- The three gates this cycle had to move with the code, each because the census they pin is the review
  point: `PatchBridgePortShapeGateTests` (four new bridge members), `SyncCoverageGateTests` (two new wire
  discriminators and the deleted quote), `AdapterCapabilityCatalog` (the three new patch classes, which is
  also what makes the game-probe install them).
