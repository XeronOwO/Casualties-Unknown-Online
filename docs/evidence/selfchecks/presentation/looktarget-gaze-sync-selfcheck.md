# LookTarget Gaze/Scare — Player Clone Presentation Self-Check (ProtocolVersion 31)

Delivery-cycle fact sheet for closing the recorded enemy-presentation gap:
a remote player's render clone did not show the owner's `LookTarget`/`CorpseScript`
override gaze or `Body.eyeScareTime`. Verification is L0 / protocol / static,
not manual dual-open acceptance.

## Step 1 — Mechanism inventory

| # | Mechanism | Current behaviour | Evidence |
|---|-----------|-------------------|----------|
| 1 | `LookTarget.OnWillRenderObject` | Sets local `Body.overrideLookTime = 0.5`, `overrideLookPos = transform.position`, and optionally `eyeScareTime = 0.5` when the enemy is near/visible | `LookTarget.cs:10-17` |
| 2 | `CorpseScript` look/scare | Same override/eye-scare transient on harvested corpses | `CorpseScript.cs:87-91` |
| 3 | `Body.overrideLookPos` / `overrideLookTime` | Drives the head angle in `HandleVisuals` (`(overrideLookTime > 0) ? overrideLookPos : targetLookPos`, Body.cs:3178) | `Body.cs:257`, `Body.cs:3178` |
| 4 | `Body.eyeScareTime` | Drives the scared facial expression through `FacialExpression` | `FacialExpression.cs:52`; `Body.cs:4008` |
| 5 | Player state stream | `RunCoordinator.PublishBodyState` already sends position/look/velocity/pose flags at 20 Hz; `SessionStatePump` writes them onto every remote clone | `RunCoordinator.cs:494-511`; `SessionStatePump.cs` |
| 6 | Remote clone Body.Update | Skipped by `BodyPatches`; the proxy only runs `HandleVisuals` and visual-input fields, so gaze/face state must be written explicitly | `BodyPatches.cs` (`BodyUpdatePatch`) |

## Step 2 — Design

- `EntityStateMsg` (already the 20 Hz player-state wire message) gains
  `LookOverridePos` (`NetVector2Msg?`, null = no override), `LookOverrideTime`,
  `EyeScareTime`, `EyePanicTime` and `EyeCloseTime` (ProtoMember 8-12).
- `PlayerEntity` carries those values; `EntitySyncService.PublishLocalState`
  accepts them from `RunCoordinator.PublishBodyState`.
- The adapter capture keeps `targetLookPos` (mouse/weapon aim) and the override
  gaze separate, so a remote clone's gun direction does not change to the
  enemy just because the owner's head is looking at it.
- `SessionStatePump` writes the override target/timer and the face timers onto
  the proxy Body every frame. The proxy's `HandleVisuals` then turns the
  head/eyes toward the override point; `FacialExpression` shows the
  scared/panic/closed-eyes face.
- No new `NetMsg`, no direction-table change. `ProtocolVersion` 30→31 because a
  v30 peer cannot render the new presentation fields.

## Step 2 — Self-check table (mechanism × change × evidence)

| # | Mechanism | Change | Evidence (file:line / test) |
|---|-----------|--------|------------------------------|
| 1 | `LookTarget` gaze | Local body unaffected; owner's override target captured into the entity stream | `RunCoordinator.cs` (`PublishBodyState`); `LookTarget.cs:12-16` |
| 2 | `Body.overrideLookTime/Pos` | Wire + proxy apply | `PlayerEntity.cs`, `EntityStateMsg.cs`, `SessionStatePump.cs` |
| 3 | `Body.eyeScareTime` / `eyePanicTime` / `eyeCloseTime` | Wire + proxy apply | same as above; `FacialExpression.cs:37-52` |
| 4 | Wire round-trip | Null override stays null; non-null target and float timers survive protobuf | `NetPacketTests.EntityState_GazeOverrideAndEyeScare_RoundTrips` |
| 5 | Entity model round-trip | `ToEntityStateMsg`/`ApplyTo` exact | `EntityStateRoundtripTests.GazeOverrideAndEyeScare_Roundtrip`, `GazeOverride_DefaultsToNull_AndTimersToZero` |
| 6 | Protocol/version | New wire fields enforced by v31 gate | `ProtocolVersion.cs`; `HandshakeHandler`/`HandshakeAckHandler` version check |

## Verification design

- **L0 / protocol**: entity-state wire round-trip for the new fields incl. the
  null-inactive case; entity model round-trip for capture/apply.
- **Patch/mechanism**: no new Harmony patch; existing `LookTarget` and
  `CorpseScript` native paths are untouched. The proxy write is display-only.
- **Code gates**: `dotnet build`, `dotnet test`, `dotnet format` +
  check-architecture + check-event-replay + check-entity-event-dispatch.
- **Runtime smoke**: deploy to the real game directory only (`tools/deploy.ps1`);
  start the real game once; `BepInEx/LogOutput.log` shows the full patch set
  installed with no CUO patch error. No manual dual-open acceptance in the
  development cycle.

## Closeout record

- Code gates: `dotnet build` 0 warnings/0 errors; `dotnet test` **1137/1137 green**;
  `dotnet format` clean; check-architecture / check-event-replay /
  check-entity-event-dispatch all passed.
- Structure review: touched classes remain under the 600-line gate
  (`RunCoordinator.cs` stays below); no new state bools, no new top-level type.
- Delivery checklist: not run as a separate final acceptance cycle; the item is
  closed with L0/protocol/static evidence and `no manual acceptance`.
