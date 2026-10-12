# Acceptance record — Native-binding parity in the session handshake

- Ticket: `mod-native-binding-handshake-parity` — verdict: **stays in `review/`** — the ticket's three acceptance rows pass; one further row, the Online UI's parity row, is unjudged and belongs to the session batch (see *Residuals*)
- Batch: 20261012-c — offline batch; scope and plan: docs/evidence/acceptance/20261012-c-scope.md
- Commit: `3bacc6fc` — the tree's HEAD; this batch changes no `src/`, `tests/` or `tools/` file
- Deployed artifact: CasualtiesUnknownOnline.dll 0.1.0+3bacc6fcd5574b3a0b9137b11c8efb5e91163d91 — deployed and hash-verified in this run (tools/verify-deploy.ps1 exit 0, "Deployment matches this tree's build output"); no runtime row is exercised
- Run: 2026-10-12 — preflight and session-environment captured before the build; build, gate suite, behavioural suite, format, deploy and deploy verification after; no client was started, so host/guest do not apply
- Dependencies: `tools/acceptance/preflight.ps1` (exit 0; its own last line reads `RESULT: OK - a full two-client run is possible`) and `tools/acceptance/session-environment.ps1 -Mode status` (`active=cuo`, `game-running=false`, `swap-needed=false`), both captured into the batch directory before the build; no client capability (steam, sandboxie, sandbox-alt, hotrepl, capture, input, logs) is consumed by this run
- Artifacts: none — the run's logs and TRX results are under the directory named by acceptance-artifacts-dir

This ticket states its acceptance in prose, not as a table. The three rows below are written from those three
acceptance bullets, in their order, before judging them; the row text is the bullet's own claim.

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | A guest whose declared bindings differ is reported or refused according to the host policy, and the reason names the mod | machine | pass | `CasualtiesUnknownOnline.Tests.Mods.ModHandshakeTests.DifferingDeclarations_RequirePolicy_RefusesAndNamesTheModAndBothDeclarations` — `Passed` in this run; the other policy levels are the same suite's `DifferingDeclarations_WarnPolicy_AdmitsAndRecordsTheMismatch` and `DifferingDeclarations_AllowPolicy_AdmitsWithoutRecording`, and the boundaries are `EqualNativeBindings_Accepted`, `NoDeclarationOnEitherSide_Accepted`, `BlankGuestBinding_NormalizesToUndeclared_RequirePolicyAccepted`, `BindingComparison_IsTrimmedAndCaseSensitive` and `ModOnlyTheGuestLists_IsNotJudgedByParity` — all `Passed` |
| 2 | The handshake matrix in the mod API page gains the declaration column, and the handshake section states what parity does and does not prove | machine | pass | `file:docs/en/reference/mod-api.md` — "a **different declared native binding** (a declaration against none included)": the handshake matrix row that carries the parity verdict, in this run's read of the page. Its second half is the paragraph on the same page beginning "What parity proves: for a mod both sides list, the two sides' declarations agree" and its `not` list (an undeclared binding is undetectable; an equal declaration does not prove equal behaviour). The Chinese pair carries the same row in its own handshake table |
| 3 | Tests: the mod handshake and handshake-protocol suites cover the new rows, and the wire round-trip covers the new field | machine | pass | `CasualtiesUnknownOnline.Tests.Protocol.ModHandshakeProtocolTests.HandshakeWithMods_RoundTripsExactly` — `Passed` in this run (the wire round-trip over the field this change added); the entry without the field decodes to null in the same suite's `ModInfoWithoutNativeBinding_DecodesToNull`, the handshake suite is row 1's `ModHandshakeTests`, and the discovery → wire carrier itself is `CasualtiesUnknownOnline.Tests.Mods.ModNativeBindingDeclarationTests.DeclaredBinding_RidesTheHandshakeInfo` with `Declaration_TravelsOnTheWireShape` — all `Passed`. The rule's own text mapping is green too: `CasualtiesUnknownOnline.Tests.Session.HostRulesPolicyTests.DefaultNativeBindingParity_IsWarn`, `...NativeBindingParityText_ParsesTheThreeLevelsAndRoundTrips` and `...NativeBindingParityText_UnknownValue_FailsAndFallsBackToWarn`, all `Passed` |

## Residuals for the user

None: the row this record first put here is not a person's to judge. The ticket's `Limits` place the parity
rule's row on the Online UI's admin page (one row, three levels) and a hand-edited config value with the user
because the Plugin layer has no test host — but what decides both is a *live client*, and this offline batch
started none. Calling them residuals was wrong: the acceptance machine is free, a two-client run reaches the
same surface, and this run's own preflight said so (`RESULT: OK - a full two-client run is possible`). They are
therefore **unjudged rows of the next session batch**, and the ticket stays in `review/` until that run reads
them. The one half a suite does decide — that an unparseable config value falls back to `warn` — is row 3's
`HostRulesPolicyTests.NativeBindingParityText_UnknownValue_FailsAndFallsBackToWarn`, green in this run.

## Limits

- **No client was started**, so this run produced no rendering, no frame, no live log and no in-process probe,
  and no two-client handshake was observed. Rows 1 and 3 are suite outcomes from this run; row 2 is an
  inspection of the committed page.
- **Nothing here observes a real session's admission decision.** The host's `warn` log line, a `require`
  refusal before the member is created, and the protocol bump's effect on a live handshake are read from the
  suite's own cases, not from a client's log.
- **The Online UI row and the config entry are not covered by automated tests** (the Plugin layer has no test
  host), exactly as the ticket's `Limits` say; that surface is named in *Residuals* rather than judged here.
- The ticket's parent declaration ticket's own limit — an undeclared binding is undetectable — is carried by
  the page's honest-boundary paragraph and is not re-argued in this record.
