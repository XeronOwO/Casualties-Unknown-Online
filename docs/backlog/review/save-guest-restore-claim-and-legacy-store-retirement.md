# S4.1 — Collision-safe restore claim + retirement of the legacy reconnect store

- Status: Review (landed 2026-09-14; awaiting the final unified acceptance pass)
- Priority: High
- Category: Persistence / save system
- Source: `docs/backlog/in-progress/save-multiplayer-restore-and-backups.md` scopes 1 and 6
- Related: decision 162 (transport-scoped identity), decision 163 (repair mode), decision 164 (the
  world repository), decision 177 (the claim verdict), decision 178 (the reconnect table's single
  source of truth), `docs/architecture/save-archive-format.md` §2/§6.1,
  `docs/backlog/in-progress/save-system-mid-run-and-layer-end.md` (stage table)

## The gap

Decision 162 keys a stored character by its transport: `steam-<steamId64>` for Steam,
`name-<sanitized display name>` for IP-direct, because that mode has no account identity. The
resolution rule shipped with S2 walked the present peers and returned the FIRST match. IP-direct
**deliberately allows duplicate display names** (`docs/backlog/resolved/ip-direct-duplicate-names.md`:
"identity is always the logical peer id / SteamID, display names are cosmetic"), so two present
players can both match one stored key — and the first one in the member list silently received the
other player's character. That is the exact outcome scope 1 forbids ("must never silently hand over
another player's character"), and the collision was only LOGGED as "no claimant", i.e. reported as
the benign case.

The second half is ownership, not logic: `CasualtiesUnknownOnline.character-data.bin`
(`Session/CharacterData/CharacterDataFileStore` + `CharacterDataFile`) kept the host's last report per
SteamID on disk across process starts. Once the world archive holds one `characters/<playerKey>.json`
per member present at the cut, that file is a second persistent copy of the same facts — the thing
decisions 162/164 exist to remove. The module is unreleased, so the user decided on 2026-09-10 that
there is no migration burden and the store is DELETED rather than kept in sync.

## What landed

- `PlayerKeyResolution.Claim` replaces `TryResolve` and answers three ways (`PlayerKeyClaim`):
  `Unclaimed` (decision 162's benign absent player), `Claimed`, or `Ambiguous`. One loop serves both
  key spaces and counts matches instead of short-circuiting, so two DIFFERENT peers can never resolve
  to one stored character (a roster listing one peer twice is not a conflict; in the Steam space the
  match IS the account id, so ambiguity there needs two distinct peers under one id).
- `WorldCharacterBinder` turns a refused claim into a named loss: `WorldCharacterBindResult` gained
  `ClaimRefusals`, which `WorldRestoreApplier` folds into the restore's damage account, so an ambiguous
  claim and a key-space mismatch reach `WorldContinueOutcome.Summary` instead of the log alone. An
  ABSENT player's file is deliberately not part of that account (decision 162 makes it a new player,
  not a degradation).
- The key-space mismatch is now a refusal too (it drops every stored character of a Steam world opened
  over IP-direct) — `LogInformation` became `LogWarning` and it travels with the account.
- **The CUT side obeys the same rule.** `WorldCharacterBinder.Collect` returns a
  `WorldCharacterCutSet` (the files, plus the players it could not carry) and a key that two present
  players WHO BOTH CARRY A SNAPSHOT map to is carried by NO file; `WorldSaveService` folds those lines
  into the cut report's "NOT carried" account. Claimants are counted per PEER ID, so a duplicated
  roster row still yields one file — and a same-named player who reported no snapshot is not a sharer,
  so the one character that does exist is still written.
- **The legacy store is gone**: `CharacterDataFileStore`, `CharacterDataFile`,
  `CharacterDataFileStoreTests` (6 cases) and `CharacterDataPersistenceTests` (8 cases) deleted, and
  with them the `characterDataFile` parameter of `CuoBootstrap.BuildServiceProvider`, its DI
  registration, the `Plugin.cs` path argument and `TestNode`'s pass-through. `CharacterDataStore` is
  purely in-memory and session-scoped; its reconnect table starts EMPTY and is fed by the live 1 Hz
  reports plus a restore's claim (`WorldRestoreApplier` binds the archive's stored characters into it).
- Coverage the deleted disk suite carried was re-expressed rather than dropped:
  `CharacterDataStoreTests.ApplyEnemyLunge_MergesTheTerminalStateIntoTheSavedSnapshot`,
  `.SessionEnd_ClearsTheInMemoryTable`, and the full-field-family protobuf round-trip — the only place
  a whole `CharacterDataMsg` (skills, limb components, nested contents, liquid stacks, the
  zero-is-valid `HandSlot`) went through the codec — which moved to
  `NetPacketTests.CharacterData_EveryFieldFamily_RoundTrips`. Stale contract comments in
  `CharacterDataStore`, `HandshakeHandler`, `CharacterDataMsg`, `ModStateFileStore` and
  `WorldRestoreApplier` are corrected in the same round.

## Defect the adversarial pass found in the first cut of this change, fixed in the same cycle

The claim side was fixed and the WRITE side was not, and the missed half was worse than the reported
one: with two same-named IP-direct players present, `Collect` produced two entries under one key, the
encoder emitted `characters/name-bob.json` twice, and `SaveArchiveWriter`'s duplicate-path guard threw
— so **every** cut was refused with an internal-invariant message ("the same snapshot path appears more
than once in the payload") that named no cause at all, and no test covered it. The same pass found that
`ClaimRefusals` had NO test (deleting the line that folds it into the account left the suite green)
and that the deleted disk suite was the only full-field-family codec round-trip. All three are fixed
above, and the two defect fixes were taken through a recorded red first (three cases failed against the
pre-fix behaviour: the L0 claim verdict, the cut-side collision, and the ambiguous claim's account line).

## Why nothing reachable is lost by the retirement

Every reader of the table is either the live 1 Hz path or a restore. A host restart can only re-enter
a run by clicking Continue (which refills the table from the archive through the claim) or by starting
a new run (which clears it) — and `SendSavedCharacter` refuses while the host is not in a world, so the
disk copy was never reachable before one of those two clicks anyway. The disk copy's only distinct
behaviour was resurrecting a character the package had since ceased to contain, which is the bug
decision 162 names.

## Acceptance

| # | Scenario | Expected | Evidence |
|---|---|---|---|
| 1 | An IP-direct world restored while two present players share a display name | Nobody is handed the stored character; both join as new players and the refusal is named in the restore account | `PlayerKeyResolutionTests.Claim_TwoPresentPeersWithTheSameDisplayName_IsAmbiguousAndClaimsNobody` (red→green, both list orders), `Claim_OnePeerListedTwice_IsNotAmbiguous`; `WorldSaveContinueTests.TryContinue_TwoPresentPlayersSharingADisplayName_HandsTheCharacterToNobodyAndNamesIt` (red→green: asserts `outcome.Summary` names `name-bob` and "more than one player present", and that NEITHER claimant got a character) |
| 2 | A Steam world opened over IP-direct (or the reverse) | No stored character is claimed in the session, and the mismatch is a named loss rather than an information-level log line | `Claim_IpDirectSpace_ClaimsByNameRegardlessOfPunctuation` / `Claim_SteamSpace_ClaimsByAccountOnly`; `WorldSaveContinueTests.TryContinue_OverSteamMode_DoesNotClaimANameKey` now asserts the account carries "key space" and "none of its 1 stored character(s) was claimed" |
| 3 | A CUT taken while two present players share a display name | The cut is WRITTEN (not refused), carries no file for the shared key, and its report names both players under "NOT carried"; the archive it wrote hands nobody a character | `WorldSaveCutSeamTests.Cut_TwoPresentPlayersSharingADisplayName_CarriesNeitherAndNamesThem` (red→green: before the fix `SaveArchiveWriter` refused every such cut with the duplicate-path invariant, so `report.Captured` was false) |
| 4 | A stored key nobody present claims | That player joins as a NEW character (decision 162); the file stays in the archive and is NOT reported as damage | `Claim_IpDirectSpace_ClaimsByNameRegardlessOfPunctuation` (unclaimed rows); the binder's `unclaimed` branch keeps `LogInformation` and adds nothing to `damages` |
| 5 | Steam identity is not spoofable by a display name | A `name-` key never resolves in the Steam space and a `steam-` key never resolves in the IP-direct space | `Claim_SteamSpace_ClaimsByAccountOnly`, `Claim_IpDirectSpace_ClaimsByNameRegardlessOfPunctuation`, `Claim_NonLatinDisplayName_StillRoundTripsItsOwnKey` |
| 6 | Restore, then reconnect, with no `character-data.bin` on disk | The character comes from the world archive; the process writes no other character file | the store's deletion (`grep` → zero references outside historical docs) + `CharacterDataStoreTests.SendSavedCharacter_ReachesTheReconnectingGuest`, `SavedData_SurvivesReentry_SameRunRestores` |
| 7 | A new run, or a session end | The in-memory table clears; nothing survives on disk because nothing is written to disk | `CharacterDataStoreTests.ClearSavedCharacters_NewRunStartsFresh`, `.SessionEnd_ClearsTheInMemoryTable` |
| 8 | Terminal-state merges (bite / lunge / effect / limb latch) still reach the table a reconnect reads | The merged snapshot is in memory, unchanged | `CharacterDataStoreTests.ApplyEnemyBite_MergesTheTerminalStateIntoTheSavedSnapshot`, `.ApplyEnemyEffect_MergesOnlyTheKindFieldsIntoTheSavedSnapshot`, `.ApplyEnemyLunge_MergesTheTerminalStateIntoTheSavedSnapshot` (new) |
| 9 | Every `CharacterDataMsg` field family survives the codec | Skills, health latches, limb components, nested container contents, liquid stacks, `HandSlot` and position all round-trip | `NetPacketTests.CharacterData_EveryFieldFamily_RoundTrips` (moved from the retired disk suite, which was the only full-field round-trip) |

## Verification limits

Machine-verified: the claim verdict for every input shape (L0 Runtime suite), the account assembly
(`WorldRestoreApplier`'s damage list, pinned through `WorldContinueOutcome.Summary` in
`WorldSaveContinueTests`), the cut-side collision rule through a real cut + a restart continue, the
store's in-memory lifecycle, the field-family codec round-trip, the full suite and the
deployed-artifact hashes.

NOT machine-verified, and NOT claimed here: the multi-client rows. Two real clients sharing a display
name in an IP-direct session, a reconnecting guest after a host process restart, and the in-game
appearance of the refusals all need the user's dual-client pass. A restore refusal is in the CLICK-TIME
account (`WorldContinueOutcome.Summary`, logged by `RunSaveCoordinator`) and today reaches `CUO.log`,
not the console — S4.2 owns making the whole click-time account (damage, claim refusals, native-field
gaps) player-visible. The CUT-side refusals this stage added ARE player-visible already: they ride the
cut report's "NOT carried" list, which `CommandConsoleService.OnCutReported` prints for a
player-initiated cut (layer-end and autosave cuts stay log-only, the pre-existing policy for every
dropped class). The full-suite and deployed-hash numbers in this file are the ones recorded in the
S4.1 commit; the artifact hashes are re-verified after that commit.
