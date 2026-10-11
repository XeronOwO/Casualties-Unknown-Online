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

- [x] Entry mapping (a scope that crosses players or mirrors a native action only): every CUO entry names the native CALL SITE it mirrors; an action needing information its gesture cannot carry is refused or answered by the user, never given a fallback; the entries stay isolated instead of being arbitrated by order; and every user-visible behaviour change in the list is user-approved rather than recorded as a limit — evidence: ONE changed gesture, and its native call site is unchanged and still the classifier — the while-dragging `GetKeyDown(favourite)` branch on the first overlapping `InvButton` (`PlayerCamera.cs:1736-1747`); the gesture now writes the local player's own set instead of naming an item of the owner's, so the entry carries no information it cannot have (it is keyed by the proxy's own authoritative id, and an id-less proxy is refused with one line rather than given a fallback); the entry is isolated by construction, because it no longer shares the release dispatcher's ordering at all; and the one user-visible behaviour change — the mark is local, personal and saved — is the user's own ruling of 2026-10-11, quoted verbatim in the ticket rather than parked as a limit
- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: every touched mechanism carries its anchor in `docs/evidence/selfchecks/items/favourite-marks-local-and-saved-selfcheck.md` (mechanism × where × what) — the mark set, the character record it rides, the one projection writer, the gesture, the adoption and the deleted intent path each name our own file plus the quoted text, and the game's own code is cited by stable `reversing/` line (`PlayerCamera.cs:1736-1747`); what no test can reach is marked runtime-only in the same file's limits rather than assumed
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: the character-RECORD family was swept, because that is where a field can be dropped silently — every field-by-field rebuild of `CharacterDataMsg` under `src/` carries the marks (`CharacterDataCapture`, `PlayerCharacterAccess.CloneCharacter`, `TraderRecruitPolicy.PrepareRevive`, with `RespawnPolicy.PrepareRespawn` inheriting it) and the two display-only rebuilds (`CloneFactTable`, `RemoteCharacterPresentation.State`) are named as correctly carrying none; the two carrier sites have their own cases (`PlayerCharacterAccessTests.CloneCharacter_CarriesTheLocalFavouriteMarks`, `TraderRecruitPolicyTests.PrepareRevive_CarriesTheLocalFavouriteMarks`); the WRITER family was swept too — every `.favourited` write in the display domain is now an explicit, reasoned entry, pinned by `FavouriteMarkWriterGateTests`; and the deleted path was swept to its last arm (enum member, capture, applier, host validation, frequency comment, docs)
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: `docs/evidence/selfchecks/items/favourite-marks-local-and-saved-selfcheck.md` carries the mechanism table, the four design answers with their sources, and the limits; the behaviour half was seen RED before it was fixed — with `RemoteItemPresentation.SourceValues` restored to its old shape (the record that put the owner's answer on the operator's star) `SourceValues_CarryConditionAndLiquidsButNeverTheOwnersFavourite` failed 1/1, and it passes with the record reduced to condition and liquids
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: three layers, each with its reach named in the self-check — the Runtime ledger is pure and covered by `LocalFavouriteMarksTests` (14 cases: toggle, the dropped stale id, the spend, the replace-not-merge seed, the wire round trip and the old-sender degradation) plus `WorldCharacterFavouriteMarkTests` (2 cases: the archive cut→continue round trip of the marks, and a stored record without them coming back with none); the source gate plus the reflective display-surface case pin what no test body can reach (`FavouriteMarkWriterGateTests`, 15 cases — 3 shape rules and their matcher samples — because the adapter is compile-excluded and the seams are Harmony-bound to `Item`/`InvButton`); and the seven real-machine rows are the next batch's judgement, with the reading each needs named (the operator's star, the owner's untouched item and the third peer's view read from both clients' censuses, the rejoin and the save+load from the item/character census after each, the adoption from `[FavouriteMark] … became its own favourite flag`)
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: the handoff that opened this session names the work item AND its order ("先做本张 High(`favourite-marks-are-local-and-saved`)"), the behaviour was frozen by the user's 2026-10-11 ruling quoted verbatim in the ticket's *Source*, the ticket hands the four design points to the cycle to settle (settled and recorded, not re-asked), and no user-visible behaviour was parked as a limit — the storage home was decided by the repository's own named-consumer rule, which the ticket says is the deciding rule
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: `dotnet format CasualtiesUnknownOnline.slnx` exit 0; `dotnet test CasualtiesUnknownOnline.slnx` green — behaviour 4946/4946 (was 4944; `WorldCharacterFavouriteMarkTests` adds 2 and `LocalFavouriteMarksTests` 14 of them), gates 615/615 (was 609; `FavouriteMarkWriterGateTests` adds 6 cases over its first shape-only version, and the independent review's findings were fixed in this same change). The three gates this change broke were fixed inside it rather than around it: the frozen patch-bridge census (the two new remote-backpack report members), the sync-coverage evidence quote that named the old capture call, and the backlog index rows
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: the one file that grew was SPLIT rather than left near the line — `CharacterDataSync` went 553 → 586 when the marks' capture wrapper landed, so the wrapper moved out into `LocalFavouriteMarkSync` (its own responsibility: what a mark does is the Runtime ledger's, what carries it is the record's, and when they meet is that one place) and the coordinator is back to 566; the other touched files are `CloneInventoryRenderer` 554, `PlayerCameraHandleWhileDraggingPatch` 222 and `IRemoteBackpackPatchBridge` 69, all under the gate; the new types are single-purpose and small (`LocalFavouriteMarks` 145, `LocalFavouriteMarkSync` 56, `FavouriteMarkAdoption` 54, `CharacterFavouriteMarkMsg` 35, `FavouriteMarkOverlay` 29); the retired mechanism was DELETED rather than kept beside the new one (`ToggleFavourite`, its capture, its owner-side apply and the host's validation arm, with the enum keeping the hole at 16); no new state flag was added — the only new state is the mark set, owned by one ledger and bounded by what a player marks by hand
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
