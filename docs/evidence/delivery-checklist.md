# Delivery Checklist

Every development cycle runs through this checklist. Two gates run before the
cycle's final commit as part of `dotnet test`:
`DeliveryChecklistGateTests.DeliveryChecklist_NoIncompleteRequiredBoxes` refuses
the commit while any required box is unchecked, and
`DeliveryChecklistGateTests.DeliveryChecklist_DeclaresEveryRequiredBox` is the
census — it refuses a checklist that no longer declares one of the steps below,
naming the step that went missing, because a record whose boxes are gone must not
pass by having nothing left to fail on. Deployment and multiplayer acceptance are
the agent's actions, run after the commit per `docs/acceptance/` and outside this
gate; feature development verification uses simulation/static evidence. When a
release cycle lands, reset the checklist by manually unchecking every box so the
next cycle starts clean.

**Operating rule (user mandates 2026-08-10 / 2026-08-16)**: boxes are checked ONE LINE AT A
TIME with the Edit tool as each step completes. The checkbox edits do NOT get
their own commit per checkbox — fold the checklist changes into the normal
work commits (implementation/docs/verification steps). The process record is
the line-by-line Edit sequence, not one commit per box. BULK checking (sed / scripts / a single
catch-up pass) is FORBIDDEN: it fabricates the process record and voids the
gate (observed: the cycle was bulk-checked, never committed, then reset —
the user called it out). Only a deliberate cycle reset may touch multiple lines.

**Documentation-only cycles (added 2026-09-17)**: a cycle that changes no runtime or test behaviour
(backlog moves, evidence/citation updates, workflow documentation) still fills EVERY box and still
leaves the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked, but may write its
boxes in one pass. That is the deliberate
multi-line exception the paragraph above already allows for a cycle reset, applied to a cycle with no
implementation sequence to record; the BULK-checking prohibition keeps governing every cycle that
touches `src/`, `tests/` or `tools/`. `dotnet format` may be skipped (it only rewrites C#, and such a
cycle has none). The evidence run may use
`--filter "FullyQualifiedName!~DeliveryChecklist_NoIncompleteRequiredBoxes"` — but the focused
normative-gate run afterwards is NOT optional: it is the only thing that proves this checklist complete
before the commit.

**Evidence rule (added 2026-09-17)**: a checked box carries a short evidence suffix on the
SAME line — `- [ ] <item> — evidence: <command/file/result>` — because a bare checkmark
records that someone decided the step was done, not what proved it. Keep it to one clause
(a command, a file, or a measured result); the full detail belongs in the cycle's ticket or
evidence file.

**Input rule (added 2026-10-22)**: the Entry mapping box also records the exact input sequence the
player performs for a new or changed player-facing gesture — which mouse button is held, which window is
open, what can still be clicked or pressed while it is held — and walks it through the game's own control
flow. A gesture the controls cannot perform is a design defect, not evidence; a call seam a probe can
drive is not evidence that a player can reach it. An "impossible" verdict lists EVERY entrance that opens
the window the gesture needs and the button each one takes (a conflict on one entrance is not the control
scheme), and an entrance behind a hotkey the player cannot be expected to know is not the primary entry:
the gesture opens what it needs from the act itself.

- [x] Entry mapping (a scope that crosses players or mirrors a native action only): every CUO entry names the native CALL SITE it mirrors; an action needing information its gesture cannot carry is refused or answered by the user, never given a fallback; the entries stay isolated instead of being arbitrated by order; and every user-visible behaviour change in the list is user-approved rather than recorded as a limit — evidence: not applicable — the change is a config-data rule (the order of an acceptable-value list and the parser fallback), not a player gesture; it adds no key, window, click target or native call site, and the only user-visible effect is that a hand-edited invalid value reads as the declared fallback instead of `allow`
- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: the clamp mechanism is decompiled in BOTH BepInEx versions that matter (deployed core 5.4.23.4 and the referenced BaseLib 5.4.20: `AcceptableValueList<T>.Clamp` returns `AcceptableValues[0]` after an ordinal `IsValid`, and the file path is `ConfigFile.Reload` → `SetSerializedValue` → `set_Value` → `ClampValue`); the defect itself is read from the running pre-fix process (`.acceptance/20261024-c/clamp-red.log`: file `Deny`/`Require` read as `allow`/`Allow`, beside the batch's `verify.log` identity `0.1.0+77510106…`); the touched code paths are `HostRuleCensus.Row.ConservativeValue`, `HostRuleValueText.ChoicesForClamp`, the two `config.Bind` calls and the two plugin parse methods
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: the "a hand-edited file bypasses the clamp" sentence was fixed in every live place it existed — both configuration pages, the plugin's `[Save]` and `Apply` comments, `SaveOptions`' class doc, `docs/architecture/save-archive-format.md` §7 and both `*Text` docs' case-insensitivity claim; the other acceptable-value lists were audited (log level and language clamp to their own fallback, no fail-open; Boolean entries have no list and take their default) and recorded in the ticket's Limits; the two rules' bindings both go through the one helper and both parsers through the one census fallback
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: (1) the clamp mechanism — `HostRuleCensus.Row.ConservativeValue` declares `deny`/`warn`, `ChoicesForClamp` puts it first, both bindings use it, both parsers derive their fallback from the same value (tests: order/set, BepInEx `Clamp` on `Deny`/`unconcious`/`Require`/`strict`, the per-row census invariant, the throw for a bad census value); (2) the file path — `ConfigFile.Reload`/`SetSerializedValue`/`set_Value` IL in 5.4.23.4 + 5.4.20, and the red log from the running pre-fix host; (3) the prose — every named location rewritten and the pair's hashes re-recorded; (4) the tests — 38/38 focused (`.acceptance/20261024-c/clamp-focused3.log`) and 6343/6343 full (`.acceptance/20261024-c/clamp-full.log`); (5) the green acceptance is `accept-config-clamp.ps1` on the deployed artifact, which prints the artifact identity
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: L0 is the unit suite (the BepInEx `AcceptableValueList.Clamp` case is the seam itself, not a re-implementation) and the gate suite; L1 is a single-host run per case with the config hand-edited to the capitalised spelling and to a typo, reading `host-rules-read` from the RUNNING process (not from the file) through the driver and printing `verify-deploy … delivered artifact …`; the red is the same probe on the pre-fix artifact (`clamp-red.log`), so the before/after pair is one method on two artifacts
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: the ticket's own three shapes and its acceptance freeze the requirement (an invalid value must fail closed, the lists stay census-bound, the numeric range stays), and shape 1 is the one that satisfies them without undoing the `/gamerule` change's accepted census binding; the behavior change (invalid → `deny`/`warn`, never `allow`) is the ticket's stated acceptance, not a new user-visible choice
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: measured — `PluginDependencyRegistrar` 337, `HostRuleValueText` 140, `HostRuleCensus` 110, `HostRuleValueTextTests` 236, all ≤600; no mutable state added (the census value is a constant declaration; the helper is pure); the SECOND declaration of the fallback — the two typed literals in the plugin — was deleted in the same round and both parsers now read the census value; one source per fact: the fallback in the census, the order in `ChoicesForClamp`, the console's canonical order still `Choices`
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: `dotnet build CasualtiesUnknownOnline.slnx` 0 warnings 0 errors (`.acceptance/20261024-c/clamp-build3.log`); `dotnet format CasualtiesUnknownOnline.slnx` exit 0 (`.acceptance/20261024-c/clamp-format2.log`); the final full run after this checklist was completed is `.acceptance/20261024-c/clamp-final-full2.log` — behaviour 6343/6343 and gates 777/777 green
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
