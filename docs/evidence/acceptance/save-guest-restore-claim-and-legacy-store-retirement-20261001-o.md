# Acceptance record — S4.1 collision-safe restore claim + legacy store retirement

- Ticket: `save-guest-restore-claim-and-legacy-store-retirement` — verdict: **stays in `review/`**
  (rows 1–3 unproven: the IP-direct scenarios were not staged; rows 4–9 pass)
- Batch: `20261001-o` (the batch `20261001-m` plan's Run B) — siblings
  `save-multiplayer-restore-and-backups`, `save-restore-account-surface`,
  `save-new-player-starting-supplies`, `save-run-clock-not-sent`, `restore-account-arm-release`
- Commit: `c3e2c6fc` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+c3e2c6fcc5d5ae482c99177224cc95d83e8935cf`
- Run: 2026-10-01 11:42 → 12:01 · Host: physical machine (Steam) · Guests: the primary sandbox and the
  alternate sandbox (third client)
- Dependencies: `steam`, `game`, `deploy`, `sandboxie`, `sandbox-alt`, `hotrepl`, `input`, `capture`,
  `logs`, `artifacts`
- Artifacts: the ids below, in the directory named by `acceptance-artifacts-dir`

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | An IP-direct world restored while two present players share a display name | machine + residual | **unproven** | not staged: no IP-direct session was created in this batch (every `state` probe reads `transport: Steam`); the Online UI's Steam / IP-direct switch exists but was not driven |
| 2 | A Steam world opened over IP-direct (or the reverse) | machine | **unproven** | same gap: the run stayed on the Steam transport, so no cross-space session existed to judge |
| 3 | A cut taken while two present players share a display name | machine | **unproven** | same gap (needs the IP-direct duplicate-name roster) |
| 4 | A stored key nobody present claims | machine | **pass** | the first cut carried all three members (`o-host-save-cut1.json`); the third client then left the session (`o-alt-state-after-leave.json`) and the next restore bound only the two present peers — `Character steam-… has no claimant in this session; decision 162: that player joins as a new character. The file stays in the archive for a later claim.` and `Restored characters: 2 bound to present peers, 1 left unclaimed, 0 refused (key space Steam)` (`o-host-log-excerpt.txt`); the restore's account carries only the manifest-fallback damage, no claim loss (`o-host-console-after-restore2.json`); the player then joined as a NEW character with the run's supplies (`o-alt-console-after-rejoin-2.json`, `o-alt-local-tree-after-rejoin-2.json`) |
| 5 | Steam identity is not spoofable by a display name | machine | **pass** | this batch's focused suite (`o-s4-suite.txt`, 169 passed) carries `PlayerKeyResolutionTests.Claim_SteamSpace_ClaimsByAccountOnly`, `.Claim_IpDirectSpace_ClaimsByNameRegardlessOfPunctuation` and `.Claim_NonLatinDisplayName_StillRoundTripsItsOwnKey`; the ambiguity half is `Claim_TwoPresentPeersWithTheSameDisplayName_IsAmbiguousAndClaimsNobody` and `.Claim_OnePeerListedTwice_IsNotAmbiguous` |
| 6 | Restore, then reconnect, with no `character-data.bin` on disk | machine | **pass** | the third client was cold-relaunched and rejoined twice; both times its tree held exactly the character the restore had bound (`rosepod#26672826005` + `emergencylight#22377858709`, `o-alt-local-tree-cold-reconnect-2.json`, `o-alt-local-tree-clean-reconnect.json`) and its log shows `Received character restore (3 items)` (`o-alt-log-excerpt.txt`); the stale pre-retirement file was renamed aside as the declared setup and no new one appeared (`o-world-archive-listing.txt`, `run-b-log.txt`) |
| 7 | A new run, or a session end | machine | **pass** | `CharacterDataStoreTests.ClearSavedCharacters_NewRunStartsFresh` and `.SessionEnd_ClearsTheInMemoryTable` in `o-s4-suite.txt` |
| 8 | Terminal-state merges still reach the table a reconnect reads | machine | **pass** | `CharacterDataStoreTests.ApplyEnemyBite_MergesTheTerminalStateIntoTheSavedSnapshot`, `.ApplyEnemyEffect_MergesOnlyTheKindFieldsIntoTheSavedSnapshot`, `.ApplyEnemyLunge_MergesTheTerminalStateIntoTheSavedSnapshot` in `o-s4-suite.txt` |
| 9 | Every `CharacterDataMsg` field family survives the codec | machine | **pass** | `NetPacketTests.CharacterData_EveryFieldFamily_RoundTrips` in `o-s4-suite.txt` |

## Residuals for the user

- None; rows 1–3 are `unproven` for a missing setup, not judged by a person.

## Limits

- Rows 1–3 need an IP-direct session whose two present players carry the same display name. This batch
  did not build or exercise that setup; the ticket stays open with the gap named and is the honest
  input for a dedicated run.
- Row 4's in-game half is a refusal that is deliberately NOT damage: the account line above and the
  absence of a claim entry in the restore's itemized list are the machine half; the player-visible
  absence of a loss message is implied by the same account.
- Row 5's live half (a guest joining a Steam world by IP) was not staged either; the row is judged by
  the suite names above, which pin both key spaces.
