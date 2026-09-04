# Guest background ghost item ground/friction sounds — mechanism inventory and self-check

Owner cycle: backlog "Guest background window plays ghost item friction/ground
sounds" (todo → review). Decision: do not mute audio or freeze the guest while
unfocused. The guest world-item copies are already non-authoritative local
simulations; the fix suppresses their native *impact presentation* (drop/step
sounds, DustMini, plush squeak) everywhere on the guest, so the effect no
longer depends on foreground/background frame cadence. Host/solo keeps the
original native impact behaviour.

## 1. Mechanism inventory (evidence-first)

| # | Mechanism | Evidence |
|---|---|---|
| 1 | Guest world items run local physics | `ItemPositionFollow.cs:15-17` — guest copies simulate locally (dynamic bodies, ground-layer collisions only) and the host's 10 Hz stream soft-corrects/snaps them |
| 2 | Layer isolation leaves ground collisions | `ItemPositionFollow.cs:121-137` — Item (7) collides only with Ground (6); every non-authoritative interaction is isolated away, but ground contact remains |
| 3 | Native item impact presentation | `reversing/Assembly-CSharp/Assembly-CSharp/Item.cs:238-247` — `Item.OnCollisionEnter2D` plays `"drop"` + the block `RandomStepSound` and spawns `DustMini` when `relativeVelocity > 3` |
| 4 | Native plush item impact presentation | `reversing/Assembly-CSharp/Assembly-CSharp/PlushScript.cs:17-23` — `PlushScript.OnCollisionEnter2D` squeaks on any impact above velocity 2 |
| 5 | The source of frame-dependent ghost audio | Guest copies can enter/leave the >3f velocity threshold when background frame/time cadence changes; the sound belongs to a local non-authoritative copy, not to a host-committed event |
| 6 | Existing authority pattern | `CrystalDrippingPatch` / `OilPipePatch` / `WorldGenerationUpdatePatch` already use `IsSessionActive && !IsHostMode` to suppress guest-side non-authoritative world simulation |
| 7 | No wire/protocol change | The fix is adapter-only; no new `NetMsg`, envelope, or `ProtocolVersion` change |

## 2. Whole-family audit (similar functions/modules)

The full collision-callback inventory in `reversing/Assembly-CSharp/Assembly-CSharp`
was searched for item-prefab components that can play presentation on guest
world-item copies:

| Family member | Verdict |
|---|---|
| `Item.OnCollisionEnter2D` | **Fixed** — guest standalone world-item copies skip the native drop/step sound + dust |
| `PlushScript.OnCollisionEnter2D` | **Fixed** — guest plushie copies skip the native collision squeak |
| `GroundGlass`, `DamagingCrate`, `SawbladeScript`, `CactusScript`, `CoilScript`, `CrystalBehaviour`, `GeigeFruitScript`, `HookPoint`, `JumpPadScript`, `MineScript`, `SpiderHandler`, `StalactiteDropper`, `Body`, `Limb`, `BuildingEntity` | Not item-prefab ground-contact presentation; either world entities handled by existing host-authority sync/freeze paths, or body/building paths that are not part of this item audio boundary |
| Remote player render clones | Not affected — `RemoteBodyFactory.cs:60-90` already disables physics/colliders and owner-local auto-event components |

No other standalone item-prefab collision callbacks were found after the
full `OnCollisionEnter2D`/`OnTriggerEnter2D` audit, so the two patches cover
the known family.

## 3. Design

- New pure policy `NonAuthoritativeItemImpactPolicy.ShouldSuppress`:
  `isSessionActive && !isHostMode && isStandaloneWorldItem`.
- New shared guard `NonAuthoritativeItemImpactGuard` folds the bridge state
  and `ItemWorldSync.IsStandaloneWorldItem(item)` into one decision.
- `ItemCollisionEnter2DPatch` Prefix returns false on the guarded condition,
  so the entire native `Item.OnCollisionEnter2D` body (sound + dust) is skipped.
- `PlushScriptCollisionEnter2DPatch` Prefix returns false for the same
  guarded item, so plushies keep their explicit use-action squeak but not
  non-authoritative collision squeaks.
- `GameAdapterBridge.OnNonAuthoritativeItemImpactSuppressed` logs the
  suppressed call at Debug with source/type/instance id, making the boundary
  observable without making collisions noisy.

## 4. Self-check table

| Mechanism | Change | Evidence |
|---|---|---|
| Guest standalone world item suppresses impact presentation | Policy returns true | `NonAuthoritativeItemImpactPolicyTests.ShouldSuppress_OnlyForGuestStandaloneWorldCopies` |
| Host/solo keeps native impact | Policy returns false for host/no session | same test |
| Non-standalone guest item not suppressed | Policy returns false for container/body children | same test |
| `Item.OnCollisionEnter2D` patch wired | Prefix routes through the shared guard | `ItemCollisionEnter2DPatch.cs` |
| `PlushScript.OnCollisionEnter2D` patch wired | Prefix routes through the same guard | `PlushScriptCollisionEnter2DPatch.cs` |
| Suppression is observable | Debug log from `GameAdapterBridge` | `GameAdapterBridge.OnNonAuthoritativeItemImpactSuppressed` |
| No focus gating | Suppression is role/session-driven, not window-focus-driven | code; no `Application.runInBackground` / focus checks added |
| No wire/protocol change | Only adapter + tests + docs | `git diff`; no `NetMsg`/`ProtocolVersion` edits |

## 5. Verification results (development-period)

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| Focused red step | `NonAuthoritativeItemImpactPolicyTests` failed (1/4) with the policy returning false before restore |
| `dotnet test CasualtiesUnknownOnline.slnx` | 2230 passed / 0 failed |
| `dotnet format CasualtiesUnknownOnline.slnx` | clean |
| `check-architecture.ps1` / `check-event-replay.ps1` / `check-entity-event-dispatch.ps1` | all passed |
| Runtime verification | static + L0 policy; no manual dual-client acceptance per development-period rule |

## 6. Structure review

- New top-level types are small and single-responsibility:
  `NonAuthoritativeItemImpactPolicy` (pure), `NonAuthoritativeItemImpactGuard`
  (adapter seam), two thin Harmony prefixes.
- No new expression-state bool in a long-lived service; the guest/session
  state remains owned by `ISessionControl` and is read through the existing
  bridge surface.
- No protocol/save authority change: the host still owns world-item physics,
  and the guest still simulates locally for smoothness; only the local
  copy's impact *presentation* is silenced.
