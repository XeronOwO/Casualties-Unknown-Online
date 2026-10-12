# Delivery Checklist

Every development cycle runs through this checklist. The gate
(`RepositoryGateTests.DeliveryChecklist_NoIncompleteRequiredBoxes`) runs before
the cycle's final commit as part of `dotnet test` and refuses it while any box
is unchecked. Deployment and multiplayer acceptance are the agent's actions,
run after the commit per `docs/acceptance/` and outside this gate; feature
development verification uses simulation/static evidence. When a release cycle
lands, reset the checklist by manually unchecking every box so the next cycle
starts clean.

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
leaves item 8 and FORBIDDEN unchecked, but may write its boxes in one pass. That is the deliberate
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

- [x] Entry mapping (a scope that crosses players or mirrors a native action only): every CUO entry names the native CALL SITE it mirrors; an action needing information its gesture cannot carry is refused or answered by the user, never given a fallback; the entries stay isolated instead of being arbitrated by order; and every user-visible behaviour change in the list is user-approved rather than recorded as a limit — evidence: no CUO entry, gesture or behaviour change: the one product-visible surface touched is a DIAGNOSTIC remediation string (`SteamSendFailureClassifier.Remediation`), which keeps the same advice in the same order and only stops naming the author's own proxy, and the user ordered exactly this on 2026-10-12 ("<proxy> 和 localhost:<proxy-port> 代理属于本机信息，不得写入，写进生产代码更是红线中的红线"), so the one user-visible edit is the one they directed
- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: one mechanism with two halves, both read at the source: the remediation pair that names a proxy (`SteamSendFailureClassifier.Remediation`, the `BadCert` and `Rendezvous` arms — the two families where a local proxy is the first known trigger), and the new gate's own matcher, whose first draft was OBSERVED producing a false positive on ordinary English ("a namespace clash" in `ModContentDeclarationScanner.cs`) and was narrowed to the capitalised proper noun before it was allowed to pass
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: the family is "a shipped string that names this machine", swept rather than patched where the user pointed: `git grep` over `src/` for the proxy name/port, the acceptance-harness ports and the Sandboxie instance names answers ZERO files now (it answered one before this commit), the test that pinned the old wording is replaced by one that pins the generic contract for BOTH proxy families, and the durable half is a gate rather than this sweep — `RepositoryGateTests.NoLocalEnvironmentNames_InProductionCode`, whose own failure message names the offending file and line
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: three rows, each observed rather than inferred: (1) the product string → the machine proxy removed from `BadCert`'s remediation → `ProxyRemediations_NameTheLocalProxyGenericallyNeverThisMachines` (a `[Theory]` over `BadCert` and `Rendezvous`) green, the classifier class 10/10; (2) the rule's durability → a new gate over `src/` → OBSERVED RED first, and the red was a real report it produced (`ModContentDeclarationScanner.cs:352`), then a false positive it produced on its own first pattern, then green after both were dealt with; (3) the product's reachability → the remediation strings are read by the transport's log path, unchanged by this edit, so nothing else in the family needed a case
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: the product half is proven at the unit level the class was built for (`SteamSendFailureClassifier`'s own doc comment says it is "L0-testable" precisely because it touches no transport state), and the rule half is proven by the gate reding on a sample and passing on the tree; nothing here needs a running client, because no code path or wire shape changed — the strings are diagnostic text
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: the user named this exact defect and its severity in the session ("写进生产代码更是红线中的红线"), so the fix is their order rather than a proposal; the two questions this cycle could have asked — whether the generic sentence is enough and whether the scan becomes a gate — were both answered by them in the same message, and the only judgement left to me was the gate's scope and its pattern's precision
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: `dotnet format CasualtiesUnknownOnline.slnx` exit 0 after the last C# edit; `dotnet build CasualtiesUnknownOnline.slnx` 0 warnings / 0 errors; `dotnet test CasualtiesUnknownOnline.slnx` green at 5068 behaviour cases and 616 normative gates (615 + this cycle's new gate; the behaviour count moved 5067 → 5068 because the replaced `[Fact]` became a `[Theory]` with two rows, pinning both proxy families instead of one) — the new gate's own runs are part of the record: red on its first pattern with a REAL report, then red on a false positive, green once the pattern was the proper noun. Earlier cycles' continuity: the stage-1 fix is deployed and verified as `0.1.0+22ae381c86cf63a7b3da90d61378c48c629b0be6`
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: measured with UTF-8-aware reads after the last edit — `SteamSendFailureClassifier.cs` 63 lines and `RepositoryGateTests.cs` 377 (both far under the 600-line class gate), `SteamSendFailureClassifierTests.cs` 105; the edit REMOVED text rather than adding state — no field, no state bool and no new top-level type anywhere in the diff, the test method it replaces is deleted rather than left beside the new one, and nothing became dead: the gate's pattern list is the only new mechanism and its consumer is the gate itself, which fails when it matches
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
