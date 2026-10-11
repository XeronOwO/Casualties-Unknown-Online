# A medical operation state that names no kind is read as an injection

- Status: Done — the fix is verified: batch `20261011-b` accepted rows 1-3, and batch `20261011-c` accepted
  row 4 after the same-family defect that had failed it was fixed
  (`docs/evidence/acceptance/medical-state-names-no-kind-20261011-c.md`; the first run's record is
  `docs/evidence/acceptance/medical-state-names-no-kind-20261011-b.md`).
  **Acceptance, batch `20261011-b`, three clients: rows 1-3 PASS and row 4 FAILS.** The patient's OWN limb
  takes the authority's write (94.217 / 0.928 against a committed `skin -5.783163 bleed +0.9278045`), the
  authority's stored copy still holds it ~17 s later, and a third client's displayed limb advanced to the
  same values 8.7 s BEFORE the session's terminal. Row 4 — the Stage-3 family regression — failed on a
  different, pre-existing defect in the same family that its own drive exposed
  (`docs/backlog/done/medical-removal-limb-component-not-applied.md`); this ticket's fix is verified in
  `docs/evidence/acceptance/medical-state-names-no-kind-20261011-b.md` and returns to `done/` once that
  defect is fixed and row 4 re-runs. The defect was found 2026-10-11 by the acceptance run of batch
  `20261011-a`, as the open half of
  `docs/backlog/review/cross-player-item-use-random-determinism.md`'s row 1: the authority committed a grip
  slip's damage and the operator's screen agreed, while the PATIENT's own body still read its pre-slip
  values.
  The run recorded two candidates ("either that update addresses a different object than the local body,
  or the value is overwritten afterwards"). Neither explains the PATIENT's body, and the second one turns
  out to be true one machine over: what gets overwritten is the AUTHORITY's committed copy, by the
  target's own next report (see *Root cause*). **The cause was found offline the same day and is fixed
  here**: the shared shrapnel session's non-terminal state messages never named their operation kind, so
  every receiving client routed them down the INJECTION branch — where a shrapnel state carries no dose
  and therefore does nothing at all, without a log line.
- Priority: Critical — the defect silently discards the authoritative limb write of a whole operation
  family on every peer, and because the authority's copy of a guest body is report-driven, the target's
  own next 1 Hz report carries its un-damaged limb straight back over the authority's committed value:
  the divergence reaches the authoritative state (a limb fact), which is the parent ticket's own
  condition for this priority. No user report was needed to find it; a user would have seen the fragment
  pull "work" and do nothing.
- Category: Players / cross-player semantics
- Related: `docs/backlog/review/cross-player-item-use-random-determinism.md` (its row 1 is this ticket's
  acceptance), `docs/backlog/review/remote-medical-stage-2-shrapnel-multiplayer.md`,
  `docs/backlog/review/remote-medical-stage-3-other-actions.md` (the family's third builder, which always
  named its kind — that is what made the omission look like a working pattern),
  `docs/backlog/review/remote-medical-treatment-operations.md`,
  `docs/evidence/acceptance/cross-player-item-use-random-determinism-20261011-a.md`.
- Source: the agent's own two-client run, batch `20261011-a`.
- Acceptance record: `docs/evidence/acceptance/medical-state-names-no-kind-20261011-b.md` — rows 1-3 pass,
  row 4 fails; the failing half and the ticket that now owns it are named there.

## What the run showed

The authority's log, for the driven slip:

```
[Shrapnel] session 5 grip slip 2 on piece 0 by <host> (head=False, seed -790031595): derived skin 4.024037 bleed 0.9653509 pain 15.61053 brain 0.
[Shrapnel] session 5 grip slip 2 on piece 0: both sides derived the same values.
[Shrapnel] session 5 break-grasp applied to <guest> limb 1: skin -4.024037 bleed +0.9653509 pain +15.61053 brain -0.
```

The patient's own client, re-reading limb 1, returned `skinHealth 100, bleedAmount 0, pain 0` — the
pre-slip values. Its log carries that update only at the terminal: the acceptance record quotes one
`[MedicalOps] local target body updated (terminal=True)` line, 15 s after the slip, when the session ended.
**That it is the only one is not the record's observation but the code's consequence** — the branch the
non-terminal states took produced no log line at all (see *Root cause*), so there was nothing else to find.

**The pain reading is a red herring and the other two are not.** The native limb drives its own pain
every frame: `Limb.Update` moves it toward `15f - this.skinHealth * 0.15f + this.infectionAmount * 0.1f`
(`reversing/…/Limb.cs:507-508`), so 15.61 above a limb at skin 95.98 decays to that target in about
fifteen seconds with nobody touching it. Skin and bleed cannot do the same while the limb still carries
fragments: `SkinHealRate` is multiplied by `(this.hasShrapnel ? 0f : 1f)` (`:56`) and the bleed line
clamps and stops clotting under the same factor (`:524`). So `skinHealth 100 / bleedAmount 0` fifteen
seconds later is not decay — those two values never reached the patient's body at all.

## Root cause

`ShrapnelSessionStateWriter.BuildState` did not set `MedicalOperationStateMsg.Kind`, and that property's
initializer was `= MedicalOperationKind.Injection`. The builder's omission was therefore not "no kind" on
the wire; it was a positive claim to be an injection.

The receiving client routes on exactly that field — `MedicalOperationApply.ApplyProgress`:

- `if (targetLocal && kind == MedicalOperationKind.Injection)` → the injection half, which runs the
  game's native dose path only `if (appliedDose.Count > 0)`. A shrapnel state's `AppliedDose` is empty
  (`BuildState` never sets it), so this branch did NOTHING — no write, and no log line, which is why the
  run saw no non-terminal `local target body updated`.
- `else if (targetLocal && health is { } targetHealth)` → `CharacterDataSync.ApplyHealState(body, …)`,
  the branch that writes the target's own limbs. Unreachable for every non-terminal shrapnel state.
- `if (kind != MedicalOperationKind.Injection)` → the display/fact-table update was skipped for the same
  reason, so no third-party view advanced either.

**Only the terminal message ever named its kind** (`BuildTerminal` sets
`Kind = MedicalOperationKind.Shrapnel`), which is precisely the one log line the run found — and by then
the damage was already gone, because the host's copy of a guest body is report-driven:
`CharacterDataHandler` stores each report wholesale over the stored character
(`ctx.CharacterData.SaveCharacterData(sender, msg)`), and the target's own 1 Hz report still carried
100/0/0. The terminal therefore committed the pre-slip numbers a second time.

The whole chain is silent by construction, which is why the acceptance run could see the authority
commit and the target read back nothing: the write reached the authority's own store (the code half's
tests assert exactly that, and still pass), and the branch that would have delivered it was never taken.

## The fix

1. **The omission itself**: `ShrapnelSessionStateWriter.BuildState` now sets
   `Kind = MedicalOperationKind.Shrapnel`.
2. **The trap behind it — the family, not the site.** Five protocol messages carried the same
   `= MedicalOperationKind.Injection` default (`MedicalOperationStateMsg`, `MedicalOperationEndCommittedMsg`,
   `MedicalOperationStartRequestMsg`, `MedicalOperationStartAckMsg`,
   `MedicalOperationTargetCheckRequestMsg`), so ANY builder that forgot the field produced a plausible
   lie. `MedicalOperationKind` gains a `None = 0` sentinel and the five properties default to it: an
   omitted field is now a named omission instead of an injection.
3. **The two builders that had been relying on the old default** — `MedicalOperationInjectionApplier`'s
   state and terminal, where `Injection` was correct by accident — now name it like every other builder.
4. **The omission is loud at the receiver.** `MedicalOperationApply.ApplyProgress` takes the generic
   snapshot branch for a kind-less message and logs a warning naming the omission. It does NOT skip: the
   generic branch is the conservative one (it replays the authority's snapshot and runs no effect), and
   skipping is the very harm this ticket is about — just with a log line.
5. **The one reader where the sentinel would have made an omission SILENT instead of loud**, found in the
   review of this change rather than by the sweep: a start request's kind is routed by
   `MedicalOperationSessionService.HandleStartRequest`, and anything that is neither an injection nor a
   fragment pull falls through to the Stage-3 family, whose `IsStage3Kind` guard simply returned — no log,
   no ack, and an operator left waiting for one. A kind-less start request would therefore have gone from
   a wrong-but-answered branch (the old `Injection` default) to no answer at all. That guard now rejects
   by name through the family's own `RejectStart`, which logs AND answers; it was the only shape in the
   family that had been silent.

The kind-less branch is unreachable in a CUO build once the gate below is green; it exists so that a
message built outside the gate's reach is announced rather than obeyed. Two other readers were checked and
are already loud: the target's body check refuses an unknown kind by name
(`MedicalTargetBodyValidator`'s `default: reason = "Unsupported operation."; return false;`), and the start
ack's kind is only read for a log sentence.

## What the code half proves now

- The authoritative shrapnel state names its kind, and so does the terminal:
  `ShrapnelOperationApplicationTests.ShrapnelStateAndTerminal_CarryTheShrapnelKind`
  (RED before the fix — `Expected: Shrapnel, Actual: Injection`, 2 of the 2 state messages the session
  published). The case asserts the FIELD, not the patient's body: the write it enables is acceptance row 1.
- The omission cannot come back through a new builder: `MedicalOperationKindGateTests` reads the SOURCE
  and requires a `Kind =` assignment in every construction of the five messages, with a census floor, a
  per-type presence check, and the matcher pinned with the named, target-typed, unnamed, initializer-less
  and unrelated-message shapes. Mutation-checked: deleting the new assignment turns it red and names the
  file and the message.
- `ProtocolVersion.Current` stays at the frozen pre-release baseline (decision 241): no field was added,
  removed or renumbered — the wire already carried this one, and what changed is which value a builder
  writes into it.

## Independent review (2026-10-11, fresh context, frozen tree, read-only)

FULL tier, run before the commit. No blocker and no major: the root cause, the report-driven revert, the
family census, the gate's reach and the red-before evidence all survived, and every figure this ticket
states was reproduced from the frozen tree independently. Four things it did find were fixed in the SAME
commit: the Stage-3 start guard's silence (item 5 of *The fix* — the one reader the sentinel would have
made quieter rather than louder), a log placeholder in the adapter that reused the name `{Kind}` for a
state/end word beside the enum-valued `{Kind}` in the same logger category, a test name that promised more
than its assertion pins, and two sentences whose scope was wider than their evidence ("carries that line
exactly once", and "neither candidate is the cause"). The gate's documented blind spot was narrowed the
same way, to the three construction shapes it genuinely misses.

## Limits

- **The receiving half is not reachable at L0**: the behavioural test project references the Game Adapter
  with `ExcludeAssets="compile"`, so no test in this repository can prove that the patient's own body now
  takes the write. Acceptance rows 1-3 below are that proof, and until they run this ticket is
  code-complete rather than verified.
- The `None` sentinel's own branches (the adapter's warning, the Stage-3 guard's new rejection) are
  deliberately unreachable in a CUO build and are therefore **not** covered by tests either: they are
  backstops, and their claim is the narrower one — every reader of this field now ANNOUNCES an omission
  rather than obeying it. That claim is swept for the four readers named above and nowhere else.
- **A recorded sibling, not swept here**: `RuntimeEntityRejectedMsg.Reason` also defaults to a plausible
  value (`RuntimeEntityRejectReason.PrefabUnavailable`) in an enum with no zero member, so an omitted field
  reads as a credible reason. It is out of this ticket's scope because its only reader takes the SAME
  branch for every value and merely logs the reason, so it can print a wrong sentence but cannot route a
  message wrongly. Both senders name it. If a future reader branches on it, it becomes this same defect.
- The parent ticket's rows 2-5 and 8 are untouched by this change.

## Acceptance rows

| # | Row | Fails when |
|---|---|---|
| 1 | a driven grip slip on a guest leaves the guest's OWN limb 1 reading the values the authority committed (skin/bleed/pain, to the precision the driver prints), read back within a second of the slip | the patient's body still reads its pre-slip values |
| 2 | one report interval later the AUTHORITY's copy still holds those values | the target's own report carried them back over the committed write |
| 3 | a third client's view of the same limb advances from the session's state, without waiting for the terminal | its display only corrects at the terminal |
| 4 | the family regression: a Stage-3 action (bandage or splint removal) still writes the target's own body through the same receiving path | the sentinel change broke a family that names its kind |

Rows 1 and 2 close the parent ticket's row 1's open half. Row 4 is the regression the default change
could have caused and must be re-run rather than argued.

## Self-check table

| Mechanism | Change | Evidence |
|---|---|---|
| the omission | `BuildState` names `Kind` | `ShrapnelStateAndTerminal_CarryTheShrapnelKind` (red before, green after) |
| the plausible default | `None = 0` and five properties defaulting to it | the same case, plus the gate's census |
| the two builders that relied on the default | `MedicalOperationInjectionApplier` names `Injection` explicitly | build + the injection session suite (155 medical/shrapnel cases green) |
| the receiver's silence | a kind-less message warns and takes the generic branch; a non-Stage-3 start request is rejected by name instead of returning silently | source shape only — no L0 harness for the adapter; both named in *Limits* |
| recurrence | `MedicalOperationKindGateTests` over `src/`, census 16 | mutation-checked by deleting the new assignment |
| the verdict's owner | unchanged: the authority still derives and writes its own values, and the operator's numbers stay evidence | the parent ticket's implementation, untouched here |
