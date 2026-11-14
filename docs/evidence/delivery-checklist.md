# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.
- [ ] Design comprehension — the game's own list draws each recipe through `Recipe.resultSprite`; our custom item has no prefab
- [ ] Native parity — the patch only answers when the game's own load returns null; every game recipe takes the native branch
- [ ] Native parity probe — a live open of the crafting screen must show the panel and one placeholder line
- [ ] Entry mapping — `PlayerCamera.HandleInput` craft key (`PlayerCamera.cs:1022`) → `OpenCraftScreen` (`:192`) → `RefreshRecipeList` (`:266-288`) → the getter
- [ ] Mechanism inventory — one getter feeds every list refresh; the patch is owned by the Crafting capability
- [ ] Whole-family audit — liquids take the native droplet path and are untouched
- [ ] Self-check table — src, sweep count, both made in this change
- [ ] Verification design — adapter build 0 errors, unit 6660/0, gates 834/834, format exit 0
- [ ] Plan approved by the user — the user asked for the fix ("做")
- [ ] Structure review done — the patch is one class, one prefix, one cached placeholder
- [ ] Build + dotnet format + dotnet test normative gates pass

- [ ] Release-cycle deployment/acceptance: this cycle deploys and reads the crafting screen

- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
