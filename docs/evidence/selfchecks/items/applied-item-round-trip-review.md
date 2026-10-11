# An applied item keeps its id — stage 2 review record (2026-10-13)

The independent adversarial review of stage 2 (`docs/evidence/selfchecks/items/applied-item-round-trip-selfcheck.md`,
ticket `docs/backlog/review/item-identity-across-transforms.md`), run in a fresh context against the FROZEN
uncommitted tree at HEAD `3cf42be3` with the prompt template in `docs/development/review-prompt.md`
(risk tier FULL: this change touches the protocol, the save format and player-visible behaviour). **The
reviewer modified nothing.** Findings are listed with their disposition; every one was fixed in the same
change, before the commit.

## What the review reproduced (its own runs)

- `dotnet test tests/CasualtiesUnknownOnline.NormativeGates.Tests` → 615/615.
- `dotnet test CasualtiesUnknownOnline.slnx` → gates 615/615, behaviour 5015/5015 (the count the change
  claimed at that moment; the fixes below take it to 5020).
- The 25-case claim, by counting `[Fact]`/`[Theory]` attributes across the test tree in the working copy
  versus HEAD: 4153 against 4128.
- Every line-count figure the self-check and the delivery checklist quote, re-measured with
  `Get-Content | Measure-Object -Line` — all exact.
- Every cited test name exists and asserts what it is cited for, with one exception (S4 below).

## Findings and dispositions

| # | Severity | Finding | Disposition |
|---|---|---|---|
| S1 | **blocker-class reasoning error, no live defect found** | `LimbItemTransformPatches`' own doc claimed a remote-proxy release is captured by `RemoteDragApplyWoundItemPatch` "which skips every postfix". **Harmony does not skip postfixes when a prefix returns `false`** — the repo already records this (`WorldTimeSync`: "not run, `__runOriginal` is false, and the postfix still fires"), so the sentence was wrong in a file later sessions read as the design. The reviewer could not construct a false report (both patches run synchronously around a call that changed nothing), so the defect was the missing guard and the false premise behind it. | Fixed: both postfixes now take `bool __runOriginal` and act on it — the apply reports nothing when the original did not run, and the take-off DROPS its armed record (`OnLimbComponentTakeOffEnd(bool nativeRan)`) instead of reporting a removal that never happened. The class doc states the Harmony rule correctly. |
| S2 | major | The deleted synthesized namespace left a stale explanation in `Runtime/Composition/ItemComposition.cs`: "the synthesized-namespace counter it owns is minted by the medical removal effect (`OtherMedicalRemovalApplier`)". The coordinator owns no such counter and the applier mints nothing. | Fixed: the comment now states the one-namespace rule and why the synthesized one went. |
| S3 | major | The cycle's claim that only "the native patch bodies" lack runtime coverage under-states the gap: `LimbItemTransform`'s three entry points all take `Item`, `Limb` or `GameObject`, so the class's decision logic (three refusals, the marker, the pending-take-off state machine, the `Awake` stamping) has no case that can run here either. | Fixed in the record: the self-check's §5 now names the whole class as the residual and says what IS pinned (both wire commands through the real host path). The class itself cannot be made testable in this harness (the game assembly is a reference, not a runtime), so no test was invented for it. |
| S4 | major | The self-check's "Local removal" evidence row pointed at `ItemIdCoordinatorTests`, whose case drives the CROSS-PLAYER removal through the medical session — a different path that never enters `LimbItemTransform`. | Fixed: the row now cites what pins the report that path sends (the end-to-end session case and the restored-world round trip) and points at §5 for the patch it cannot pin. |
| S5 | major | **A missing piece of the stage.** The applied row's `condition` was written once at the apply and never again, while the component keeps decaying (`SplintLimb.Update`) — so the durable row, which the design record's §6.1 assigns to "the 1 Hz character capture that already reads that field", diverged from the component for the whole life of the item, and an archive or a mod reading it would see a splint frozen at its installed condition. | Fixed: `AppliedItemConditionProjection` is that seam (host side, the capture's three call sites in `CharacterDataStore`), and it follows the component in steps of 0.01 so a slow decay costs one command per step rather than one per report. Five cases (`AppliedItemConditionProjectionTests`) plus the format page, decision 259 and the self-check. The removal deliberately still hands over the LIVE component (decision 184), which the projection does not touch. |
| N1 | nit | `ItemIdCoordinatorTests`' helper doc still said "the synthesized namespace's last removal … the one production consumer of that counter". | Fixed. |
| N2 | nit | `docs/backlog/README.md`'s index line still advertised "the synthesized counter" for the id-resume ticket. | Fixed. |
| N3 | nit | The sync-coverage matrix's I1 row gained the two payload types but its inline "Wire members:" list did not name them, while the vocabulary index did. | Fixed: both members are in the inline list as well. |
| N4 | nit | "Applying a splint is no longer the item's end" needs its qualifier: a splint that decays to zero still leaves the hands within two frames (relocation, then the item's own zero-condition destroy). | Fixed: the self-check's §2 says so explicitly, and calls out what the change does and does not alter for the player. |

## What the review could NOT falsify

- The premise mapping (its own A0 pass): the four entry points' native counterparts, the absence of any
  "most injured limb"/"first match"/limb-0 fallback, and that the two extra `TakeOff` callers (the
  component's own decay and `OnDismember`) really do go through the patched method.
- The gateway's two new rules: a member cannot install another member's item, install anything on
  somebody else's limb, or take a component off a limb that is not its own.
- The cross-player removal's ordering: the applied row is resolved and the operator's free slot is
  checked BEFORE anything is cleared, and the operator's `AwardedItem` id is the same id end to end.
- `RegisterTransferredItem`'s shape, and that the adopt/spawn calls it replaced did not drop a fact
  another reader needs.
- That `Item.Awake` cannot stamp the wrong object (the `ItemInstanceId` guard plus the per-gesture reset),
  and that two take-offs cannot nest.

## What the review could not check at all

- **Anything needing the running game**: the patch bodies and, as S3 records, the adapter class behind
  them. No process in this tree executes a Harmony patch body.
- `dotnet format` (forbidden during the review — it rewrites files). The cycle ran it after the fixes;
  the tree is clean.
- Its I1/I8 documentation cross-check and the `RequireLimbIndex` malformed-frame path were unfinished when
  the report landed; the parent completed both, and N3's finding is the one that check produced.
