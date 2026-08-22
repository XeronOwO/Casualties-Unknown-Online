# Carry another player — direct player interaction slice (ProtocolVersion 27)

Owner cycle: backlog "Direct player interaction (view/take items, carry, view
vitals, heal)". Decision for this cycle: close the **carry/release another
player** half by making the host the cross-player authority for the carry
relation and letting the carried player's own client drive its body from the
carrier's reported entity state. The Online UI gets Carry/Drop buttons for
in-world unconscious/dead remote players. Heal remains a separate open item.

Decision summary:

- The host validates the carryable rule (target unconscious or dead; carrier
  conscious and alive), enforces one carrier/one carried with no symmetric or
  mutual carry in this MVP, records the relation and broadcasts a
  `PlayerCarryStateMsg` to every member. One operation = one reliable message.
- Every side keeps a read-only carry mirror for UI and GameAdapter. The
  carried player's own client adds a `CarriedBodyDriver` marker to its local
  body; `BodyPatches` then treats that body like a render proxy (physics and
  per-frame Body/Limb simulation skipped) and `GameAdapter.CarryInteraction`
  moves it to an offset on the carrier's back each frame from the carrier's
  entity buffer.
- Because the carried body reports its own position/velocity through the
  ordinary 20 Hz/1 Hz streams, peers do not need a carry-specific render
  network: they already see the position the carried client reports. The carry
  state is only the authority/UI driver, not a second movement channel.
- Release (Drop) is the inverse: the host clears the relation, broadcasts
  `CarriedSteamId = 0`, and the carried client destroys the `CarriedBodyDriver`
  so its body returns to local simulation.

## 1. Mechanism inventory (evidence-first)

| # | Mechanism | Evidence |
|---|---|---|
| 1 | Existing direct-player interaction domain | `PlayerInteractionService` already owns the host-authoritative cross-player take; carry is the second operation in the same domain (`src/.../PlayerInteraction/PlayerInteractionService.cs`) |
| 2 | KrokMP reference (not copied) | KrokMP has `carry`/`piggyback` as NetBody relations and a keybind menu (`reversing/KrokMP/.../ClientMain.cs`, `NetBody.cs`, `CoopKeybinds.cs`); CUO adopts only the *cooperative rule* and the relation concept, not the byte protocol/physics copy |
| 3 | Host has authoritative character data | `GetHostCharacterData` / `GetSavedCharacter` — the same snapshots used by take v26; host uses them to validate conscious/alive state for both carrier and carried |
| 4 | Carryable state rule | Only `!Conscious || !Alive` targets are carryable (same KrokMP-compatible cooperative default the take slice uses); the host re-checks it, never trusting the UI |
| 5 | Local body discovery | `RunCoordinator.LocalBody` + `PlayerCamera.main.body` pattern; the existing transfer handler already uses the latter |
| 6 | Carrier entity on every side | `EntitySyncService.GetRemotePlayer(steamId)` returns host/guest entity buffers (host: guest reports; guest: host + host-relayed guests) — the carried body driver reads the carrier's position/velocity from there |
| 7 | Render-proxy mechanics | `BodyPatches` already skips Body.FixedUpdate/Update and Limb.Update for `RemoteBodyDriver`; the new `CarriedBodyDriver` reuses the same skip path for a local body while it is carried |
| 8 | Protocol | New `PlayerCarryStartRequestMsg` (99, guest→host), `PlayerCarryStopRequestMsg` (100, guest→host), `PlayerCarryStateMsg` (101, host→all); ProtocolVersion 27 |

## 2. Whole-family audit

| Family member | Change |
|---|---|
| `PlayerInteractionService` | New carry state dictionaries + start/stop handlers, carry-state mirror, lifecycle cleanup (SessionEnded/MemberRemoved/RemoteSceneChanged), publish/broadcast |
| `IPlayerInteractionControl` | New `SendCarryStartRequest` / `SendCarryStopRequest` / `Handle*` / `FireCarryStateReceived` / `CarryStateChanged` / read-only mirror |
| `PacketReceiver` / `DirectionTests` | 99/100 guest→host; 101 host→guest |
| `NetMsg` / `ProtocolVersion` | New IDs + 27 |
| GameAdapter | Subscribes to `CarryStateChanged`, adds/removes `CarriedBodyDriver`, drives carried body per frame |
| BodyPatches | FixedUpdate/Update/LimbUpdate treat a carried local body like a render proxy |
| Online UI / Plugin | Carry and Drop buttons on remote member rows; both forward through the same host-authoritative domain |
| Existing entity/character streams | Unchanged — the carried body's position is reported by the carried client through the ordinary streams, so peers need no carry-specific movement network |

## 3. Self-check table (mechanism × change × evidence)

| Mechanism | Change | Evidence |
|---|---|---|
| Host validates carryable | Refuses conscious/alive target; refuses non-conscious carrier | `HandleCarryStartRequest` + tests `Carry_ConsciousTarget_IsRefused`, `Carry_UnableCarrier_IsRefused` |
| Host validates no mutual/symmetric relation | Refuses when either party already carries/is carried | `Carry_AlreadyParticipatingInRelation_IsRefused` |
| Host records and broadcasts | `PlayerCarryStateMsg` with Carrier/Carried reaches every guest; host local event applies | `Guest_StartsCarryingUnconsciousHost...`, `Host_StartsCarryingUnconsciousGuest...` |
| Guest mirror | A receiving guest updates its carry mirror for UI | `Guest_StartsCarryingUnconsciousHost...` asserts guest mirror |
| Stop clears | `PlayerCarryStopRequest` removes both dictionaries and broadcasts `CarriedSteamId=0` | `Carry_Stop_ClearsRelationAndBroadcastsEmptyState` |
| Direction table | New one-way messages classified | `DirectionTests` |
| Local carried body follows carrier | `GameAdapter.UpdateCarriedBody` writes transform from carrier entity; `CarriedBodyDriver` marker present while carried | Static code + runtime service tests (no manual acceptance) |
| Body simulation paused while carried | BodyPatches skip path checks `CarriedBodyDriver` | Static evidence in `BodyPatches.cs`; no new state bools |
| Online UI | Carry button only on in-world unconscious/dead remote; Drop when local carries that member | Static UI code; host rule re-checked in runtime |
| Session cleanup | Carry relations clear on session end/member remove/world leave | `PlayerInteractionService` event subscriptions + cleanup path |

## 4. Verification design (development-period, no manual acceptance)

- **L0 runtime wire tests** (`PlayerInteractionServiceTests`): guest→host and
  host→guest carry starts, conscious-target refusal, unable-carrier refusal,
  already-participating refusal, stop + empty-state broadcast, guest mirror.
- **Direction tests**: every new NetMsg is explicitly classified g2h/h2g.
- **Full regression**: `dotnet test CasualtiesUnknownOnline.slnx` — all existing
  item/entity/character domains stay untouched.
- **Static evidence**: the carried body follows via the ordinary entity stream,
  so no second movement channel; BodyPatches reuses the proven proxy skip path.
- **Runtime verification box**: L0 simulation + static evidence + real-game-dir
  deploy; **no manual acceptance** (user rule 2026-08-16).

## 5. Plan approval

The user instructed this session to pick one backlog item autonomously and
complete it, then write the result back into `../backlog.md`
("由你来自主挑选一个并完成，记得在完成之后回写 backlog"). That instruction is
the plan approval for this cycle; no further interactive approval is required.

## 6. Verification results (2026-08-22)

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx --no-build` | 1053 passed / 0 failed |
| `dotnet format CasualtiesUnknownOnline.slnx` | clean on source (verify-no-changes flags only the gitignored generated `obj/.../MyPluginInfo.cs`) |
| `check-architecture.ps1` / `check-event-replay.ps1` / `check-entity-event-dispatch.ps1` | all passed |
| `tools/deploy.ps1 -GameDir "E:\SteamLibrary\steamapps\common\Casualties Unknown Demo"` | deployed to the real game dir only |
| Protocol | 27 (new NetMsg 99-101) |

## 7. Structure review

- `PlayerInteractionService` ~487 lines (take + carry, one direct-interaction
  domain), `GameAdapter.CarryInteraction` 88, `CarriedBodyDriver` 26,
  `OnlineUiOverlay` 287 — all under the 600-line gate.
- One top-level type per file; the carry relation state belongs to
  `PlayerInteractionService` (host-owned) with a read-only mirror surface, not
  a shared DI state object.
- No new expression-state bool fields in touched classes; `CarriedBodyDriver`
  is a marker and the BodyPatches additions are guard expressions, not state.
- Dead mechanisms: none. The carried body rides the existing entity/character
  streams; no new parallel movement or render channel was added.

## 8. Accepted boundaries

- One carrier and one carried per relation; no piggyback stack, no mutual carry.
- No distance/line-of-sight validation in this slice (the Online UI is a
  status-panel interaction, matching the take slice's host-snapshot check).
- A late joiner does not need a carry-relation snapshot: the carried body's
  position already travels via the ordinary 20 Hz stream; carry state only
  drives the carried player's own client and the UI.
- Heal remains open (direct body/Inventory interaction).
