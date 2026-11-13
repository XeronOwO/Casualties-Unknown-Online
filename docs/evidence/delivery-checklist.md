# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.
- [ ] Design comprehension — the live reading that failed was the identify step, not the wire: the Body component sits on a CHILD of the character object (`RemoteBodyFactory`'s own comment: the proxy patches query `GetComponent<RemoteBodyDriver>()` on "the Body (which is a CHILD of the root clone)"), so `body.gameObject.name` never held the character's name
- [ ] Native parity — the table is now read from the character OBJECT's own name (`body.transform.root.name`), which is the instantiated prefab the game spawns, with Unity's `(Clone)` suffix ignored; the spawn table itself was re-read from `PlayerCamera.SpawnCharacter` (0 Experiment, 1 Milky, else Dune)
- [ ] Native parity probe — the probe is the decompiled `SpawnCharacter`: `if (chr == 0) Experiment; if (chr != 1) Dune; return Milky;` — the same three paths `CharacterIndex.PrefabPath` names
- [ ] Entry mapping — the capture reads `body.transform.root.name` (owner) and the factory compares `local.transform.root.name` (viewer); both are the instantiated character objects, never the child Body
- [ ] Mechanism inventory — the clone source is now named by the RENDERER (`RemotePlayerRenderer.cs` after the clone is registered), because `RemoteBodyFactory.CreateRemoteBody` is covered by `LogVolumeGateTests` (a repeatable diagnostic inside it must ask a window first); the factory returns the source through `out string source` instead
- [ ] Whole-family audit — a wrong character still has to be visible: the source string names the loaded prefab OR the fallback ("the viewer's own body (the member's character prefab did not load)"), and the renderer prints it per member
- [ ] Self-check table — the suffix case is pinned (`IndexOfName("Milky (Clone)")` = 1) and the earlier four facts still pass; build 0 errors, gates 834/0
- [ ] Verification design — the live row reruns the same setup: host character forced to Dune (`lastchar_h2474283127` = 2 before launch), a fresh run, and each client's log must name the OTHER player's character (`Remote body for <id> built from Special/Characters/<name>`)
- [ ] Plan approved by the user — the user ordered the work and the queue continues on the same item
- [ ] Structure review done — no new type: the fix stays inside `CharacterIndex` (one name-reading function) and the factory's existing out-parameter pattern
- [ ] Build + dotnet format + dotnet test normative gates pass
- [ ] Release-cycle deployment/acceptance: the live two-character reading is rerun after this commit

- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
