# S3.4b — Native character fields (lastHappiness, caloriesConsumed, WoundView.cInfo)

- Status: Review — implemented, tested, documented and independently reviewed on 2026-09-11. The
  in-game read of the three values (the wound window's text, the death-stats calorie number, the
  tooltip's happiness) is the user's dual-client acceptance, per the project's verification boundary.
- Priority: Medium-High
- Category: Persistence / save system
- Source: Stage S3.4b of `review/save-system-mid-run-and-layer-end.md`, split out of
  `review/save-native-run-field-parity.md` (S3.4a landed the run-level fields there).
- Related: `docs/architecture/save-archive-format.md` §3.4/§6.1, `docs/decisions/active.md` 165/166/170/171,
  `review/save-mid-run-consistent-cut.md` (S3 umbrella), `review/save-native-run-field-parity.md` (S3.4a)

## Why

The native `SaveSystem` used to load three character-level fields that no CUO domain owns
(`SaveSystem.cs:172-180` write, `:438-441` read). CUO stopped reading `save.sv` (decision 165), so a
continued run started them from the game's defaults — and because the game SKIPS its own fresh
character-details roll when a run is continued (`PlayerCamera.Start`, `PlayerCamera.cs:726-729`), the
wound window showed `0cm | 0y | #0`, the happiness history (pause tooltip, last-chance evaluation) was
an all-zero window and `caloriesConsumed` (death stats) was zero. Visible defect, not a cosmetic gap.

## Landed

| field | where it lives now | how it gets there |
|---|---|---|
| `Body.lastHappiness` (the game's ten-value window) | `CharacterDataMsg.NativeFields` (`CharacterNativeFieldsMsg.LastHappiness`), one file per player in `characters/<playerKey>.json` | read off the body at the same instant as the rest of the snapshot — the host's own at the cut, a guest's at its 1 Hz report — and written back by the local restore path's second pass |
| `PlayerCamera.caloriesConsumed` | same sub-message (`CaloriesConsumed`) | same read; applied through the live camera |
| `WoundView.cInfo` (height/age/id/version) | same sub-message (`CharacterInfo`) | same read; applied through `WoundView.SetCharDetails` — the game's own entry point, so the window text and the two statics it updates (`firmwareVer`, `specimenId`) move together (`WoundView.cs:54-68`) |

The read is ALL-OR-NOTHING across the body and the two scene statics: a read that met a body but no
camera describes no coherent instant, so nothing is captured rather than zeros — zero is a real value
here (a new character's window IS `0cm | 0y | #0`), and a snapshot of a failed read would be
indistinguishable from a genuine one. That rule lives in the Runtime
(`CharacterNativeFieldPolicy.TryCapture`), the live reads and writes in the adapter
(`CharacterNativeFields` + `ICharacterNativeSystem`).

The apply is the restore path's SECOND pass — after the wipe and after the items, i.e. the point the
native load wrote them. A value the native contract cannot hold is refused BY NAME and keeps the live
value: a happiness history longer than the game's own ten-value window (the body's array is the game's
own and `LastHappinessUpdater` shifts that array in place), or a details array that is not the four
`SetCharDetails` takes. A snapshot that carries none of the three is named as damage in the restore
report (`CharacterNativeFieldPolicy.Missing`, fed by `WorldRestoreApplier` into the same summary the
run-level gaps already use), and the local half is logged at the restore.

**Family sweep done in the same cycle** (the fix-the-family rule): `TraderRecruitPolicy.PrepareRevive`
builds a fresh message field by field, so a death → revive cycle would have dropped the new fields
silently and produced a snapshot the restore then names as a gap. It now carries `NativeFields`
through, with a test. `CloneFactTable`'s entries are display-only and correctly carry none;
`RemoteCharacterPresentation.State.From` builds a normalizer over a snapshot it does not own.

**Architecture gate**: `CharacterDataSync` was at 596 lines (the watchlist had it at the split line),
so the demanded split happened first: the restore's write half moved to `CharacterRestoreApplier`
(stats + wipe, items, native fields) and the worn-item write to `WearableRestorer` (both the restore
and `PlayerInteractionApply` use it). The coordinator now owns only WHEN a restore runs — queue,
position gate, two-frame rhythm, 1 Hz report, capture. 596 → 508 lines.

## Self-check table (mechanism × change × evidence)

| mechanism (decompiled) | change | evidence |
|---|---|---|
| `SaveSystem.cs:172-180` writes the three fields from `body.lastHappiness` / `PlayerCamera.main.caloriesConsumed` / `WoundView.view.cInfo` | the snapshot carries the same three values, read from the same objects | `CharacterDataCapture` + `CharacterNativeFields.TryCapture`; `NetPacketTests`; `WorldCharacterNativeFieldTests` (archive round trip) |
| `SaveSystem.cs:438-441` applies them on load (through `SetCharDetails`) | the restore path writes them on its second pass, through the same entry point | `CharacterRestoreApplier.ApplyNativeFields` → `CharacterNativeFields.Apply` → `LiveSystem.SetCharacterDetails`; `CharacterNativeFieldPolicyTests` pins which values may be written |
| `Body.cs:4283` — `lastHappiness` is `float[10]`, and `Body.cs:1082-1092` shifts THAT array in place | a longer archived row is refused by name instead of replacing/truncating the array | `CharacterNativeFieldPolicyTests.Plan_HappinessHistoryLongerThanTheGameWindow_IsRefusedByName` |
| `WoundView.cs:54-68` — `SetCharDetails` takes exactly four ints and also sets `firmwareVer` / `specimenId` | a details array that is not four is refused by name; the write goes through `SetCharDetails`, never `cInfo` directly | `CharacterNativeFieldPolicyTests.Plan_CharacterDetailsNotTheNativeFour_AreRefusedByName`; the live call is the native one (`LiveSystem`) |
| `PlayerCamera.cs:726-729` — `Start` skips the fresh roll when `SaveSystem.loadedRun` | the continued body keeps the live defaults unless the archive's values land, which is why the gap is NAMED | `WorldRestoreApplier` feeds `CharacterNativeFieldPolicy.Missing` into the restore summary; `WorldCharacterNativeFieldTests.Continue_WhenAStoredCharacterCarriesNoNativeFields...` |
| `TraderRecruitPolicy.PrepareRevive` rebuilds the message field by field | the revive clone carries `NativeFields` through | `TraderRecruitPolicyTests.PrepareRevive_CarriesTheNativeCharacterFields` |
| 1 Hz report / cut / `NotifyBodyLeft` are the three snapshot producers | all four capture sites (the throttled report, the immediate inventory re-report, the cut's `CaptureLocal` and the leave report) pass the live-scene port, with no path that silently captures without it | `CharacterDataSync.cs:226`, `:235`, `:283`, `:323` |
| the interaction services' `CloneCharacter` (a field-by-field copy that is then SAVED over the stored snapshot) | carries `NativeFields` through, like `TraderRecruitPolicy.PrepareRevive` | `PlayerCharacterAccessTests.CloneCharacter_CarriesTheNativeCharacterFields` |
| `Body.cs:643-650` averages the whole ten-value happiness window and `:957` reads slot 9 | a history that is not the game's ten values is refused by name, never prefix-written | `CharacterNativeFieldPolicyTests.Plan_HappinessHistoryShorterThanTheGameWindow_IsRefusedByName` (1/5/9) and `...LongerThan...` |
| `WorldCharacterBinder.Apply` is what decides which stored key a present peer claims | the restore report describes only the characters actually BOUND (`WorldCharacterBindResult`) | `WorldCharacterNativeFieldTests.Continue_DoesNotBlameACharacterNoPresentPeerClaims` |

## Independent adversarial review (fresh subagent) and its outcome

One blocker, two majors, three minors and two nits; all resolved in the same cycle. The reviewer could
NOT falsify the proto member/route registration, the extraction's behaviour equivalence (compared
line by line against `git show HEAD:...`), the zero-versus-failed-read distinction, the four capture
sites, the revive path, or the "the report names the absent run fields" wording; it explicitly could
not run a real Unity scene, which is the boundary this ticket already records.

| # | severity | finding | fix |
|---|---|---|---|
| 1 | blocker | `PlayerCharacterAccess.CloneCharacter` dropped `NativeFields`, and that clone is what the interaction services SAVE over the stored character (`PlayerHealService`, `PlayerInventoryTakeService`, `MedicalOperationInjectionApplier`) — the next cut, reconnect or continue would silently lose the fields | copy it, + tests. **Red→green record is real**: with the line removed, both `PlayerCharacterAccessTests` cases fail (`Assert.IsType` → Actual: null) |
| 2 | major | a 1–9 element happiness row was accepted and prefix-written into the ten-slot live window, mixing restored values with the fresh body's zeros while the game averages all ten slots | `TryCapture` and `Plan` now require EXACTLY the game's window. **Red→green record is real**: with the previous `Count == 0 \|\| Count > 10` rule, the three short-history cases fail |
| 3 | major | the report could under-report (a non-null but malformed sub-message contributed no damage) and over-report (characters no present peer claims were described as continuing with defaults) | `Missing` is derived from `Plan` so every refusal is named, and `WorldCharacterBinder.Apply` returns the keys it actually bound (`WorldCharacterBindResult`), which is what the report walks; two new archive tests |
| 4 | minor | `SetCharacterDetails` was a silent no-op when the wound window vanished between the read and the write, while the plan still reported that field as applied | the port returns `bool`; the refusal is named like the other two |
| 5 | minor | the capture discarded its failure reason (`out _`), so a cut that lost the fields could not say why | the reason travels back and the coordinator logs it (the 1 Hz reports and the cut's own snapshot) |
| 6 | minor | an explicit JSON `null` list would throw inside the restore's second pass and leave its phase uncleared — the next frame would re-apply the items | `Plan` treats a null list as the empty one it already refuses by name |
| 7 | nit | `body is null` on a Unity object, against the project's `== null` rule (the adjacent comment said so) | `body == null` |
| 8 | nit | the new tests never exercised the adapter's own capture/apply wiring | recorded as a verification limit below: the adapter's capture sites and live reads sit behind statics no test host can materialize; the tests assert the plan's applied/refused shape instead |

## Evidence

- `CharacterNativeFieldPolicyTests` (15 cases): a whole read succeeds; every incoherent read reports
  WHY (no live scene, a happiness window that is not the game's ten, wrong detail count); the plan
  covers all three fields in order; a snapshot without native fields plans nothing; a history that is
  short, long or empty and a details array that is not four are each refused by name while the other
  fields stay applicable; an explicit JSON `null` list is refused instead of throwing; `Missing` names
  the whole absence, names only the unusable field of a partly usable snapshot, and stays empty for a
  usable one.
- `WorldCharacterNativeFieldTests` (4 cases): a layer-end cut → continue round-trips the local
  character's three fields through `characters/<key>.json`; a stored character without them makes the
  continue summary name the key and the three field names; a malformed sub-message is named by field;
  a character no present peer claims is not described at all.
- `PlayerCharacterAccessTests` (2 cases): the interaction clone carries the fields, and stays an
  independent copy of the mutable item tables.
- `NetPacketTests.CharacterData_NativeFields_RoundTripAndDegradeForAnOldSender`: the wire round trip of
  `ProtoMember(9)`, and an old sender's message decoding to a null sub-message.
- `TraderRecruitPolicyTests.PrepareRevive_CarriesTheNativeCharacterFields`: the revive clone keeps them.
- `CharacterDataFileStore`'s own versioned protobuf wrapper needs no schema bump: the new members are
  optional, so an old file/binary simply carries none — the same degradation the report names.

## Acceptance

| # | Scenario | Expected | State |
|---|---|---|---|
| 1 | Continue a run, then look at the wound window | Same `height/age/id/version` (`UNI-HEALTH v2.0<ver>` text) as before the quit, not `0cm/0y/#0` | machine-verified for the values through the archive; the WINDOW TEXT needs the user's in-game check |
| 2 | Continue a run, then die | The death-stats calorie counter shows the restored value, not 0 | machine-verified for the value; in-game display is the user's check |
| 3 | Continue, then open the pause tooltip / reach last chance | The happiness history is the restored window, not ten zeros | machine-verified for the value; in-game read is the user's check |
| 4 | Old snapshot / failed read (no live camera) | The restore report names the missing field set; nothing silently defaults | machine-verified |
| 5 | A malformed or hostile row (history ≠ 10 values, details ≠ 4, an explicit null list) | Refused by name, live value kept, other fields still applied, no throw | machine-verified |

## Verification limits

The Runtime/format/codec/policy layers are machine-verified, and so is the adapter's `Apply` decision
path (`CharacterNativeFields.Apply` drives the port it is given, and the policy tests pin what it may
write). What is NOT machine-verified and needs a dual-client session:

- The three values are GAME UI surfaces: the wound-window text, the death-stats calorie number and the
  tooltip's happiness read. They reach the live body through `ICharacterNativeSystem.LiveSystem`, whose
  two statics (`PlayerCamera.main`, `WoundView.view`) exist only in a running game — a test host can
  neither materialize them nor construct the game's `Body`.
- The adapter's own four CAPTURE sites therefore have no unit coverage either: the test project
  references the Game Adapter with `ExcludeAssets="compile"`, so those types are not reachable from a
  test body without reflection. The tests assert what the capture must produce (the policy's capture
  verdict and the archive/wire round trips), not that the live game's statics were read.

## Next

- S3.5 (exactly-once plus the documentation/re-anchoring pass) stays in the umbrella ticket.
- The umbrella's scope 6 restore-report history: native world facts' own refusals still reach the log
  only (tracked in `review/save-mid-run-consistent-cut.md` scope 6) — the character half of that report
  is what this stage closed.
