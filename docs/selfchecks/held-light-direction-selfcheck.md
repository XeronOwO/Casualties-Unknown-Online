# Held-Light Direction Self-Check (#119)

Remote render clones point their hand-held flashlight / emergencylight /
rangefinder at the local machine's mouse instead of at the peer's reported aim.
This cycle fixes #119 with a Harmony postfix; no protocol or world-state change.

## 1. Mechanism inventory

| # | Mechanism | Evidence |
|---|---|---|
| 1 | `CustomItemBehaviour.Update` points hand-slot flashlight at the local mouse: `Vector2.SignedAngle(Vector2.right, (Camera.main.ScreenToWorldPoint(Input.mousePosition) - transform.position).normalized) - 90` | CustomItemBehaviour.cs:520-528 |
| 2 | Same local-mouse aim for emergencylight (`- 90`) and rangefinder (no offset) | CustomItemBehaviour.cs:433-442, 506-514 |
| 3 | Only the two hand slots are aimed (`inventorySlot == body.slots[0] || body.slots[1]`) | CustomItemBehaviour.cs:434, 508, 522 |
| 4 | Clone bodies carry `RemoteBodyDriver` on the Body GameObject and skip simulation; `SessionStatePump` writes the peer's 20 Hz `LookPos` into `Body.targetLookPos` | RemoteBodyFactory.cs, BodyPatches.cs, SessionStatePump.cs:63-67 |
| 5 | Clone inventory renders are real `CustomItemBehaviour` components, so the game's orientation code runs on them | CloneInventoryRenderer.cs:134-155 |
| 6 | `RemoteBodyDriver.LastStateMs` stays 0 until the first entity snapshot is applied — the readiness gate before overriding an aim that has not arrived yet | SessionStatePump.cs:49-57 |

Whole-family audit: the only `Camera.main.ScreenToWorldPoint(Input.mousePosition)`
call sites in the game's item behaviour are the three above; no other held item
reads the local mouse for orientation (the whole decompiled source was searched).
`IKHandle` clones already disable their local-mouse aim lines in
`RemoteBodyFactory`.

## 2. Design

- New pure angle helper `HeldItemDirection` (scalar `Atan2` equivalent of the
  game's `Vector2.SignedAngle` formula) so the angle rule is testable without
  constructing Unity types in the reflection-only test host.
- `HeldItemDirectionPatch.Postfix` runs after `CustomItemBehaviour.Update`:
  - keeps every other per-item Update side effect untouched;
  - returns for non-directional items, non-hand slots, local bodies, and
    clones that have not received their first snapshot;
  - overwrites only the final `eulerAngles.z` for flashlight /
    emergencylight / rangefinder on a `RemoteBodyDriver` clone, using the
    body's synced `targetLookPos`.

No protocol bump: `LookPos` already rides the 20 Hz entity state stream.

## 3. Self-check table

| Mechanism | Change | Evidence |
|---|---|---|
| flashlight aim | postfix re-aims to `body.targetLookPos`, same `-90` offset | HeldItemDirectionPatch.cs; CustomItemBehaviour.cs:526-527 |
| emergencylight aim | postfix re-aims, same `-90` offset | HeldItemDirectionPatch.cs; CustomItemBehaviour.cs:439-440 |
| rangefinder aim | postfix re-aims, same `0` offset | HeldItemDirectionPatch.cs; CustomItemBehaviour.cs:512-513 |
| non-hand slots | unchanged (hand-slot gate mirrors the original) | HeldItemDirectionPatch.cs:47-50; CustomItemBehaviour.cs:434, 508, 522 |
| local body | unchanged (RemoteBodyDriver gate) | HeldItemDirectionPatch.cs:52-55; RemoteBodyFactory.cs |
| first-snapshot window | unchanged original local-mouse frame until the peer's aim exists (`LastStateMs != 0`) | HeldItemDirectionPatch.cs:52-55; SessionStatePump.cs:49-57 |
| angle math | pure helper tested right/up/offset/zero-aim | HeldItemDirectionPatchTests.cs |
| patch surface guard | `CustomItemBehaviour.Update` contract is part of `PatchInventory`; a deletion fails the new patch-set test | HeldItemDirectionPatchTests.cs; PatchContractTests.cs |

## 4. Verification design

- L0 simulation: six new tests exercise the angle helper and assert the patch
  shape and its presence in `PatchInventory`; the existing contract tests keep
  verifying the target against the copied game assembly.
- Static evidence: decompiled call-site inventory above (whole-family search —
  the three local-mouse item call sites are the complete set).
- Runtime evidence: development-period rule — L0 simulation + static evidence,
  no manual acceptance (user 2026-08-16 mandate).
