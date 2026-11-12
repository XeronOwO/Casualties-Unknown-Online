# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.

- [ ] Design comprehension — the removal path must finish its work after a throw in any one step
- [ ] Native parity — the game creates a clone's parent with the member's world entry and destroys it with the exit
- [ ] Native parity probe — the host's log at a removal answers the pair
- [ ] Entry mapping — `HostKickService.Kick` → `RemoveGuestMember` → `EntitySyncService.OnMemberRemoved` → `RemoteSceneChanged` → `OnRemoteSceneChanged` (`src/CasualtiesUnknownOnline.GameAdapter/Character/RemotePlayerRenderer.cs:395`)
- [ ] Mechanism inventory — both clone-destroy sites in the file carried the unguarded parent (`:405-409`, `:150-153`); both are fixed
- [ ] Whole-family audit — the guard now removes the cache entry first, then destroys, and handles a missing parent
- [ ] Self-check table — build 0 errors, gates 834/0
- [ ] Verification design — a Unity clone cannot be unit-tested; the row is the live log pair at a removal
- [ ] Plan approved by the user — night mode: fix or file every finding with its reading
- [ ] Structure review done — the fix stays inside the renderer; no new type, no interface change
- [ ] Build + dotnet format + dotnet test normative gates pass

- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
