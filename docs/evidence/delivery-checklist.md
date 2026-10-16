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

- [x] Entry mapping (a scope that crosses players or mirrors a native action only): every CUO entry names the native CALL SITE it mirrors; an action needing information its gesture cannot carry is refused or answered by the user, never given a fallback; the entries stay isolated instead of being arbitrated by order; and every user-visible behaviour change in the list is user-approved rather than recorded as a limit — evidence: the second entry into the same feature is a member-card button beside the console command, and it mirrors the same native call sites the command does (the game's own camera fields). Neither needs information its gesture cannot carry: the card names the member by construction, and the one thing it cannot know — whether that member's body exists on THIS client — is refused by the camera and reported by the console rather than guessed. The two entries stay isolated: both read one rule, and the row being watched decides which of the two buttons a card shows
- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: the newly touched mechanisms are three, and the review moved two of them. (1) The member-card action family — an existing pattern (`OnlineUiMemberAction` plus `InteractionActions`), read before it was joined; (2) **the watched id** — first implemented as the plugin's own memory, which the review showed is a second source for a fact the camera owns and gives up on its own, so it is now published by the port (`ISpectatorControl.WatchingSteamId`) and refreshed every frame like the IP-direct fact beside it; (3) **the eligibility rule** — first a second copy of the camera's terms, which the review showed had already drifted (it ignored the host's unconscious rule), so the card now reads the camera's own two terms. UNVERIFIED and in the ticket: whether the target's body exists on this client is adapter-only, so a card click can still be refused with nothing on screen saying so
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: the family is every surface that offers a member action, and the sweep found THREE of them rather than the two the code's own comment claimed: the Players page, the quick panel (both through the member list) and the in-world right-click menu, which builds its own list and had no watch entry at all. All three carry it now, and the class comment that claimed two was the reason nobody had looked. The family's second axis is the id key space: the roster is fed session ids while the local row was compared against a Steam id, which in IP-direct sessions marks no row as local — the same defect every other row action in that mode already had, fixed at the source the review named
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: the port (`WatchingSteamId` added, pinned in `AdapterCapabilityPortShapeTests` — the count moved to twenty-four and an acceptance record citing the old name moved with it); the projection (two new facts, `CanSpectate`/`IsWatched`, five unit cases green); the plugin (two delegates, one published id refreshed every frame, two forwarders — the stop forwarder answers false when nothing is watched, matching the console); the member list (two buttons, the rule read from the camera's own terms); the right-click menu (the same two entries, because it is the third card surface); the labels (two keys, both languages, both with real readers). The camera half still has no test and no runtime reading, and that is the ticket's own next step
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: this half is covered by unit cases at the projection (which is pure by construction: it takes plain values and returns rows), including the two facts the card renders, and the console's own seven cases already cover the port end. What a card click does that no unit case can reach is the Unity path from the button to the intent to the port: the ticket's required work 5 carries it, together with the rows the review added — the watched row losing its mark when the camera gives the watch up by itself, and a console `/spectate` being reflected on the card. Both surfaces read the same published id precisely so that those two cases have one answer to check
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: the ticket is the user's own request and it names BOTH choosers, the member list first ("the Online UI's member list is the natural chooser, with the console as the keyboard path"). The previous cycle built the console half and the ticket says so; this one builds the half the ticket named first. Nothing here is a work-item or design choice the user has not already made
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: no touched class is near the limit (`OnlineUiMemberListDrawer` is 296 lines before this change, `OnlineUiActions` 187, `OnlineUiMemberProjection` 209). State went DOWN rather than up: the plugin's `WatchingSteamId` property was deleted in the same round the review showed it was a second source for the camera's own fact, and the port's published id replaced it. The doubled-apostrophe family the previous review filed a nit against was swept across all four files that carried it rather than re-filed, and the one sentence in `InteractionActions` that recorded the row's order was corrected to match what the method now does
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: `dotnet build CasualtiesUnknownOnline.slnx` 0 warnings / 0 errors; `dotnet format` exit 0 after the last edit; `dotnet test … --filter "FullyQualifiedName!~DeliveryChecklist"` green at **5515/5515** behaviour cases and **662/662** gates. Three gates caught this change's consequences: the port-shape gate (the pinned member list and the composition count, both moved deliberately), the documented-anchor gate (an acceptance record citing the renamed count test — the FOURTH time this cycle that one record has been repointed, which is itself a finding about naming a count into a test), and the localization catalogue gates. The independent adversarial review ran against the FROZEN tree BEFORE this line and found **1 blocker, 3 major, 4 minor and 4 nits**: the blocker was that the watched id was copied once at construction, so the feature's only way out could never render — the exact defect the review prompt's "two sources for one fact" question was written to catch. It, both of its majors and the cheap minors are fixed here; what remains is named in the ticket
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
