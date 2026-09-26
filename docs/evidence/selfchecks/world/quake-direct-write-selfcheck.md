# The earthquake's direct clock write — self-check (2026-09-26)

Ticket: `docs/backlog/review/world-acceleration-quake-direct-write.md` (Medium; the remaining sibling of
the movement-routing fix, decision 223). Cycle scope: the game's own DIRECT `Time.timeScale` write is
recorded as owning the shared clock — the owner ruled "follow vanilla", so an earthquake's reset ends a
standing session acceleration and CUO never suppresses it — and the screen that owns the clock now also
re-states the game's own speed state, so the HUD follows the actual clock. No deployment this cycle: the
rendered frame and the real quake are the unified acceptance pass's rows.

## 1. Mechanism inventory — what the direct write was, and what read it

| # | Mechanism | Evidence (quoted) |
|---|---|---|
| 1 | The write | `WorldGeneration.Update` sets the clock back at a quake start: `Time.timeScale = 1f;` (reversing/Assembly-CSharp/Assembly-CSharp/WorldGeneration.cs:870, inside `if (this.earthquakeDelay < 0f && this.biomeOverride == ... && !this.body.sleeping)`) — a field write with no `PlayerCamera.SetTimeScale` call, so `WorldTimeScaleCall` cannot classify it and no native flag exists |
| 2 | The other three writers | The census (`%TEMP%/cuo-census-quake-direct-write.txt`): `ConsoleScript.cs:815` (the console's `timescale` command), `PreRunScript.cs:64` (run start), `WorldGeneration.cs:1036` (`ReloadScene`); every other `Time.timeScale` mention in the assembly is a READ (the remaining mentions are comparisons and multipliers) |
| 3 | Who sees it | `WorldTimeSync.Update` → `AdoptDirectTimeScaleWrite` on the host (adopt + broadcast) and `EnforceAppliedSpeed` on a guest (back to the applied speed). A guest never runs the quake write at all: `WorldGenerationUpdatePatch`'s Prefix caps `earthquakeDelay` at `float.MaxValue` for a guest, so the `delay < 0` trigger "never fires and a guest quake never STARTS locally; the guest's earthquakeTime only ever turns positive through the host's broadcast" |
| 4 | The HUD surface | `PlayerCamera.HandleTimescaleIcons` (PlayerCamera.cs:2146-2159, called from the camera's Update at :1333): the TEXT follows the live clock (`this.timescaleText.text = "x" + Mathf.RoundToInt(Time.timeScale).ToString();`) while the ICON row follows the field (`if (i != (int)this.curTimeScale) ... this.speedImages[i].color = ...`) |
| 5 | The field's only writer | `PlayerCamera.SetTimeScale` assigns it (`this.curTimeScale = speed;`, PlayerCamera.cs:659) and the census shows exactly four mentions of `curTimeScale` in the whole assembly (that assignment, the switch on it, the icon comparison, the declaration) — no other writer, no other reader |
| 6 | Why the staleness persisted | Vanilla heals it: a movement key calls `SetTimeScale(Normal, false, false)`, which assigns `curTimeScale`. Decision 223 swallows that call on BOTH sides (the patch's prefix returns false), so the native assignment no longer runs — a movement no longer repairs the icon row, which is why the host kept showing the ended acceleration |
| 7 | The clock seam | `PlayerCameraSetTimeScalePatch` lets CUO's own applies through (`if (origin == CallContext.Origin.WorldTimeApply) { return true; }`) and excludes them from the postfix, so a re-state through `ApplyLocalTime` runs the native method exactly once and cannot re-enter the router |
| 8 | Session / wire | None: the change reads the live clock and the game's own speed field and writes the same value back SILENTLY; no message, protocol, save or gameplay state is touched |

## 2. What landed

- **`WorldTimeDirectWrite`** (`Runtime/Session/World`, new, 60 lines, pure like its
  `WorldTimeScaleCall` sibling): `Classify(liveClock, appliedSpeed, isHost)` answers `Verdict.None`
  when the live value is not a domain speed (`WorldTimeSpeedScale.FromTimeScale` answers null for
  Paused, Slowmo and every value between two speeds) or already is the applied speed, `Verdict.Adopt`
  on the host and `Verdict.Restore` on a guest. There is deliberately NO suppression member: the
  ruling closed that branch, and the census test pins the member NAMES, so re-opening it is a
  deliberate act.
- **`WorldTimeSync.AdoptDirectTimeScaleWrite`** takes its verdict from the rule and, after adopting
  the value as the standing request, re-states it through the normal apply path with
  `switchSound: false, force: false`. The native write moved the clock but not
  `PlayerCamera.curTimeScale`, so the HUD's icon row followed the ended acceleration; the re-state
  writes the value the clock already runs (no dip), plays no sound (the native reset was silent) and
  is skipped by the game's own paused/death-screen guard, which is what `force: false` asks for.
- **`WorldTimeSync.EnforceAppliedSpeed`** takes the same verdict for the guest side; its behaviour is
  unchanged (the rule's predicate is HEAD's predicate, moved into the Runtime).
- **No wire, protocol, save or gameplay change**; the sleep gate, the start gate, the routing rule and
  the console's authority are untouched.
- **The sound is unchanged and recorded** (§7).

## 3. Family audit — every side of the direct-write family

| Surface | Verdict |
|---|---|
| Quake start (`:870`) | Host: adopted, and the silent re-state now keeps the HUD honest; the acceleration ends session-wide, which is vanilla. Guest: never runs (its timer is frozen and the host's broadcast drives `earthquakeTime`) |
| Console `timescale` (`ConsoleScript.cs:815`) | Host: adopted — the admin's value becomes the shared speed, which is what keeps the command's meaning. Guest: the pump puts this screen back on the host's speed (unchanged), because the shared clock is the host's |
| Run start (`PreRunScript.cs:64`) | Never reaches the rule: `WorldTimeSync.Update` returns while there is no session body, and the main menu owns its own clock |
| Scene reload (`WorldGeneration.cs:1036`) | Never reaches the rule: the start gate owns `timeScale` 0 across a reload and the pump returns while `WaitingForReady` |
| The routed `SetTimeScale` family | Unchanged (decision 223): announced changes own the clock, silent resets are swallowed and keep the session speed |
| `KeepSessionSpeed`'s silent restore | Unchanged, and deliberately the same shape this cycle uses for the adoption (silent, `force: false`) — the two silent corrections now read the same way |
| The sleep policy | Unchanged; its own step can restore a sleep speed in the same pump frame the quake reset was adopted, which is the gate's documented authority (recorded in §7) |
| The HUD (icons + text) | The icons now follow the actual clock on the host; the text always did. The guest's icons already followed (its clock is written through `ApplyLocalTime`) |
| The sound | Unchanged: the host stays silent (vanilla's reset is silent), the guests receive the session's standard speed-change cue (§7) |
| `reversing/` reads of `Time.timeScale` elsewhere (`BasicCourse.cs:230`, `Body.cs:3564`, `BuildingEntity.cs:54`, `FluidManager.cs:409`, `GrabberPlant.cs:42-48`, `Item.cs:150`, `TraderScript.cs:428`, `WorldGeneration.cs:922`) | Read-only consumers of the clock (tutorial waits, XP scaling, rigidbody simulation gates); no writer hides among them — the census greps for the assignment, and every one of these is a comparison or a multiplier |

## 4. Self-check table — claim × evidence

| # | Claim | Evidence |
|---|---|---|
| 1 | The rule's contract: domain-speed drift → `Adopt` on the host / `Restore` on a guest; paused/slowmo/ramp values and an already-applied speed → `None`; the two sleep speeds behave like any other domain speed | `WorldTimeDirectWriteTests`: 13 matrix rows (including `UnconsciousFast` and `DyingFast`) + the member-name census |
| 2 | The ruling is executable and its rejected branch cannot be re-entered silently | `TheRulingHasNoSuppressionVerdict` (exactly `None`, `Adopt`, `Restore` — names, not a count) and the pin's suppressed-adoption negative sample |
| 3 | The host's adoption re-states the game's own speed state, silently, in the right ORDER | the mechanism pin requires the ruled verdict, both speed assignments, exactly one re-state and one broadcast, and the order stored → re-stated → broadcast; the no-re-state, suppressed, old-applied-speed, re-ordered-re-state, fixed-speed-re-state and second-broadcast mutations are all asserted rejected |
| 4 | The guest's correction goes through the rule | the pin's guest matcher (the ruled verdict plus exactly one apply); the rule's `Restore` rows |
| 5 | The re-state reaches the game's own state and nothing else | `PlayerCameraSetTimeScalePatch` passes `Origin.WorldTimeApply` through to the native method; `switchSound: false` means no `PlayUISound` (PlayerCamera.cs:662-696 plays it only when the flag is set); the value written is the one the clock already shows |
| 6 | The census is complete | `%TEMP%/cuo-census-quake-direct-write.txt`: four assignment sites, the routed `SetTimeScale` bodies, and every other mention a read |
| 7 | Structure and gates | new Runtime type is one top-level type per file, 60 lines, pure; `WorldTimeSync` 511 → 526 lines (under the 600 gate); no state boolean added; the source-shape, backlog, decision and checklist gates are the ladder's own runs |

## 5. Red, ladder and the numbers

Red: the mechanism pin against a Game Adapter whose two methods carry HEAD's bodies while the rule type
is present — `8 failed / 16 passed / 24 total` (`%TEMP%/cuo-red-quake-direct-write.txt`). The eight are
the pin's: the two positive cases cannot find the ruled adoption or the rule-driven enforcement, four
sample guards find that HEAD's body still IS the sample (`Assert.NotEqual` fails — the "the mutation did
not silently no-op" guard), the reorder sample's precondition fails because HEAD has no re-state at
all, and the fixed-speed sample cannot find its anchor. The 16 that pass are the rule matrix's 14 cases
(they exercise the pure Runtime type directly, so HEAD's adapter cannot affect them), one vacuous pin
case (HEAD's body already lacks the re-state, so that mutation has nothing to remove) and the
second-broadcast sample. The red was captured by copying the fixed `WorldTimeSync.cs` aside, reverting
that one file to HEAD, running the filter and restoring the file immediately; it is NOT reproducible on
the frozen tree without that temporary revert, and the frozen-tree form of the same evidence is the
in-test pre-fix bodies plus the mutation samples.

Ladder on the frozen tree: focused `FullyQualifiedName~WorldTimeDirectWrite` 24/24, exit 0
(`%TEMP%/cuo-focused-quake-direct-write.txt`) → full suite WITH build 4027/4027 plus normative gates
208/208, exit 0 (`%TEMP%/cuo-full-quake-direct-write.txt`; the gate run filters
`DeliveryChecklist_NoIncompleteRequiredBoxes` because this cycle's checklist is reset while it is
filled — the unfiltered gate project run afterwards is what proves it complete) → `dotnet format`
exit 0 (`%TEMP%/cuo-format-quake-direct-write.txt`), with the working tree verified unchanged by it.
Mid-cycle gate runs: 208/209 with the checklist gate as the single failure (the expected mid-cycle
state, `%TEMP%/cuo-gates-mid3-quake-direct-write.txt`), and an earlier 207/209 while decision 226 still
pointed at the ticket's `todo/` path — the backlog-reference gate caught the move, and decision 223's
stale path was corrected in the same change. The final UNFILTERED run, after the checklist was complete:
main 4027/4027 and normative gates 209/209, exit 0 (`%TEMP%/cuo-full-final-quake-direct-write.txt` and
`%TEMP%/cuo-gates-final-quake-direct-write.txt`).

## 6. Independent adversarial review and dispositions

A fresh-context reviewer, read-only against the frozen tree, reproduced every number (focused, the full
suite with build, the gates, and `dotnet format` with the tree verified unchanged) and every census
claim, and reported **1 major, 5 minor, 2 nits, no blocker**; the full report and its interim section
are in `%TEMP%/cuo-review-quake-direct-write.md`. Every finding landed in the same commit:

| # | Severity | Finding | Disposition |
|---|---|---|---|
| F1 | major | Decision 226 and the ticket cited this self-check, which did not exist yet — the only one of the 18 evidence paths in `active.md` that failed to resolve, and gate-invisible because the link gate strips code spans | landed: this file, in the same commit, with the numbers and the limits the citations delegate |
| F2 | minor | The text pin accepted broken shapes: a re-state primed with a constant, an early `return;` that makes the whole body dead, and a duplicated broadcast | landed for everything a text pin can reach: the matcher now requires the ORDER (stored → re-stated → broadcast), a one-re-state/one-broadcast census, and three new negative samples (re-ordered re-state, fixed-speed re-state, second broadcast); the two positive tests were renamed so their names no longer claim reachability, which stays a declared limit |
| F3 | minor | The ticket's row 5 still said "focused 18/18" (the pin had gained a case since) | landed: the row no longer restates a count and points at §5 |
| F4 | minor | `world-acceleration-survives-movement-selfcheck.md` still named the ticket's old `todo/` path — the only remaining occurrence in the tree | landed: the reference moved with the ticket, and the sentence now records that the sibling cycle answered the question |
| F5 | minor | The `Classify` matrix never exercised the two sleep speeds, although both are reachable live values through the same mapping | landed: `UnconsciousFast` and `DyingFast` rows added, with the note that the sleep POLICY decides what stands |
| F6 | minor | The red's description did not say that the rule type stayed present, so a reader could take "reverting the adapter to HEAD" as "the rule tests must fail too" | landed: §5 now decomposes the captured state and its 8/16/24; the reviewer re-derived it from the capture and withdrew its earlier alternative reading |
| F7 | nit | The no-suppression contract was pinned only as an enum COUNT, so a renamed member would pass | landed: the census asserts the member NAMES in order |
| F8 | nit | `Verdict.None`'s doc named "a correction ramp", a case the rule's matrix never exercises | landed: the doc attributes the mapping to `WorldTimeSpeedScale.FromTimeScale` (whose own tests pin the ramp's conversion), and the matrix's null rows carry the rule's side |

The reviewer's stated-limits pass also produced one addition: the all-unconscious sleep policy can
restore a sleep speed in the same pump frame the adopted write landed, and it was stated nowhere. It is
now in §7 and in the ticket's non-goals, with the two bounding facts — the restored speed goes through
the ordinary apply path, so the HUD follows it, and the quake trigger keys on the body's own BED flag
rather than the consciousness the policy reads, so the two conditions can coexist. What the reviewer
could not falsify is the load-bearing half: the HUD mechanism, decision 223 having removed vanilla's
heal, the fix reaching the game's only writer with no re-entry path, the guest predicate's exact
equivalence, and a complete four-writer census.

## 7. Limits — what this cycle does not prove

- **No rendered pixels and no game run.** Neither the quake nor the corrected icon row has been seen:
  the repository has no Unity runtime in its verification path. The acceptance run is the judge, and it
  should confirm that after an earthquake the `x1` text and the lit speed icon agree, at 5× and at 20×.
- **The sound asymmetry is recorded, not changed.** The host stays silent (the game's own reset plays
  no sound, and the re-state asks for none), while the guests' clock is written by `OnTimeReceived`
  and therefore plays the session's standard speed-change cue. Removing that cue would need the wire
  to distinguish an adopted direct write from an announced change; this cycle did not judge that worth
  a protocol change for a sound, and the acceptance run is where the ear can overrule it.
- **`force: false` is deliberate and bounded.** If the game's own paused/death-screen guard refuses
  time-scale writes, the re-state is skipped and the icon row stays stale for that frame; the clock is
  not advancing there, and forcing the write would override a guard the game owns.
- **The sleep policy keeps its authority.** If the host's all-unconscious gate stands on a sleep speed,
  its own step can restore that speed in the same pump frame the quake reset was adopted: the policy
  owns that decision, the restored speed goes through the ordinary apply path (so `curTimeScale`
  follows it), and the quake trigger keys on `!this.body.sleeping` — the body's own BED flag, not the
  consciousness the policy reads — so the two conditions can coexist.
- **The pins read source text.** They prove that the ruled statements are present, in order, once each,
  and that six broken shapes are rejected — but not reachability: an early `return;` inserted inside the
  body, or a value primed from somewhere else, still carries every required statement. The behaviour
  itself has no probe in this tree.
- **No deployment, no dual-client run** — the development-period standard.
