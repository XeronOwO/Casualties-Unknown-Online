# Gun State Sync — persistent GunScript transition reports (no protocol bump)

Owner cycle: backlog item-domain recorded gap "gun firing/racking has no
reports" in `../item-features.md`. Decision: report the persistent
`GunScript` state transitions through the existing item-use fact path so the
host record and peer clones update at the action edge instead of waiting for
the 1 Hz character snapshot.

## 1. Mechanism inventory (evidence-first)

| # | Mechanism | Evidence |
|---|---|---|
| 1 | Gun persistent state | `GunScript.cs:229-324` — `roundInChamber`, `roundsInMag`, `magCapacity`, `hasMag`, `triggerPressed`, `firingPinStruck`, `safe`, `racked`, `lastRacked` are `[JsonProperty]`/`[Saveable]`; `condition` is on the parent `Item`. |
| 2 | Native transitions | `GunScript.cs:185-222` (`Fire`), `:47-50` (`TryRack`), `:53-57` (`ToggleSafety`), `:60-87` (`LoadMag`), `:90-102` (`UnloadMag`), `:105-182` (`Update` — trigger, gas-time auto-unrack, rack/unrack sound + chamber edges). |
| 3 | Component digest already carries the state | `ItemStateCodec.CaptureSaveableComponents` admits `[Saveable]` components and reads public simple fields (`ItemStateCodec.cs:184-230`); the gun state was already on the 1 Hz character snapshot. |
| 4 | Missing freshness layer | The item-feature narrative recorded "gun firing/racking has no reports (a stale chamber record corrects on the next drop)" — the action edge did not produce a dedicated report. |
| 5 | Existing use fact path | `ItemUseSync.OnItemUsed` (`ItemUseSync.cs:26-73`) is the accept-with-correction report: guest sends `ItemUse`, host adopts via `CheckUseEvidence` and broadcasts `ItemCarriedSync`; host's own fact broadcasts directly. |
| 6 | Remote clone guard | `RemoteBodyDriver` marks freeze proxies; `GunFirePatch.cs:26-32` already uses it to suppress clone-side reports. |

## 2. Whole-family audit

| Family member | Change |
|---|---|
| `GunStateSync` (new) | Owns per-instance last-reported persistent snapshot; reports only an actual change through `ItemUseSync.OnItemUsed`. |
| `GunStatePatches` (new) | Thin postfixes on `Update`, `Fire`, `TryRack`, `ToggleSafety`, `LoadMag`, `UnloadMag` → `IPatchBridge.OnGunStateChanged`. |
| `IPatchBridge` / `GameAdapter` | New narrow bridge entry forwarding to `GunStateSync`. |
| `GameAdapter.Construction` | Constructs `GunStateSync` after `ItemUseSync` (one-way dependency). |
| Existing item use / carried sync | Unchanged wire: guest → host `ItemUse`, host → guests `ItemCarriedSync`; no new `NetMsg`, no direction row. |
| Existing 1 Hz character snapshot | Unchanged; remains the fallback. |
| Remote clones | No report (remote guard); they still receive the authoritative carried fact through the existing host broadcast. |

## 3. Self-check table

| Mechanism | Change | Evidence |
|---|---|---|
| Fire/rack/safety/load/unload report | Persistent state changes route through `GunStateSync` → `ItemUseSync.OnItemUsed` | `GunStatePatches.cs`; `GunStateSync.cs` |
| Update-driven auto-rack caught | `GunScript.Update` postfix also calls the report; the sync domain deduplicates | `GunStatePatches.UpdatePatch`; snapshot equality in `GunStateSync` |
| No duplicate per fire | `Fire` and `Update` both call `TryReport`; the last-reported snapshot suppresses the second | `GunStateSync.TryReport` |
| Remote clone never reports | `RemoteBodyDriver` parent guard in `TryReport` | `GunStateSync.cs` |
| No new wire/protocol | Reuses `ItemUse`/`ItemCarriedSync`; `ProtocolVersion` unchanged | `ItemUseSync.cs`, `ItemCarriedSyncService.cs` |
| Fallback intact | 1 Hz character snapshot still carries the full state | `CharacterDataSync`; existing tests |
| Patch contracts | Six new attributed targets auto-covered by `PatchContractTests` | `GunStatePatchTests` + `PatchContractTests` |

## 4. Verification design (development-period, no manual acceptance)

- **L0/simulation**: full `dotnet test` — 1172 passed / 0 failed (was 1170).
- **Contract tests**: `PatchContractTests` resolves every new `[HarmonyPatch]`
  target against the game assembly; `GunStatePatchTests` locks the reflective
  surface set and the `GunStateSync.TryReport` shape.
- **Static evidence**: report path is the existing item-use accept-with-
  correction path; no new message/state table; remote clones guarded.
- **Runtime verification**: real-game-dir deploy (see §6); no manual dual-open
  acceptance (user rule).

## 5. Plan approval

The user instructed this session to pick one backlog item autonomously and
complete it, then write the result back into `../backlog.md`
("由你来自主挑选一个并完成，记得在完成之后回写 backlog"). That instruction is
the plan approval for this cycle; no further interactive approval is required.

## 6. Verification results

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx --no-build` | 1172 passed / 0 failed |
| `dotnet format` (verify) | clean on tracked/untracked source; only the gitignored `obj/.../MyPluginInfo.cs` reports |
| `check-architecture.ps1` / `check-event-replay.ps1` / `check-entity-event-dispatch.ps1` | all passed |
| `tools/deploy.ps1 -GameDir "E:\SteamLibrary\steamapps\common\Casualties Unknown Demo"` | deployed to the real game dir only |
| Protocol | unchanged (no new `NetMsg`, no `ProtocolVersion` bump) |

## 7. Structure review

- `GunStateSync.cs` is a single domain type (~95 lines), owns the last-reported
  state (state belongs to the sync domain, not the patch).
- `GunStatePatches.cs` contains only thin nested patch classes with no
  cross-call fields.
- `GameAdapter.cs` remains under the 600-line gate; no new expression-state
  bool fields were added.
- One top-level type per file; no DI/collapse/factory introduced.
- Dead mechanisms: none. The 1 Hz character snapshot stays as the intentional
  fallback, not a second report path.

## 8. Accepted boundaries

- The report is a freshness layer; a reliable-but-lost `ItemUse` still self-heals
  from the 1 Hz character snapshot.
- Transient per-frame fields (`triggerPressed`, `firingPinStruck`) and the
  mirror field `lastRacked` are deliberately outside the report snapshot.
- No new wire message or protocol bump; older clients are unaffected.
