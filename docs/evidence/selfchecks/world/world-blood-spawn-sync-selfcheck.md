# World blood spawn sync self-check

Owner cycle: backlog "World bleeding effects sync". Decision: add a dedicated
reliable `WorldBloodSpawn` message (NetMsg 121, ProtocolVersion 51) reported
from the owning player's local `BleedParticle` decal branch. The decal is
transient (120 s lifetime, no periodic snapshot), so a lost message is
presentation-only degradation.

## 1. Mechanism inventory

| # | Mechanism | Evidence |
|---|-----------|----------|
| 1 | Native bleed particle decal spawn | `BleedParticle.cs:18-55` — when a dying particle reaches end-of-life, every 1st/3rd death instantiates `Special/blockblood` (ground) or `wallblood` and destroys after 120 s; it also plays a `dripN` sound. |
| 2 | Existing player fur-blood presentation | `CloneLimbRenderer.cs:123-130` + `LimbPresentation.cs:41-42` — remote clones already receive fur-blood and set the particle emission; without this event they also created unsynchronized local decals. |
| 3 | Remote clone suppression | `RemoteBodyDriver` marks every render/simulated remote player clone; `BleedParticleWorldBloodPatch` skips the native Update on those clones so the owner's report is the only decal source. |
| 4 | One-shot presentation event pattern | `TraderSwingMsg` / `CharacterRagdollMsg` — star semantics: guest → host report, host fires + relays, guest replays inside `RemoteApply`. |

## 2. Design

- `BleedParticleWorldBloodPatch` Prefix observes the native private dying-particle
  loop and simulates the same modulo counter (`spawned` / `every`) to determine
  which particle caused a decal spawn; it only runs for the local player's own
  Body (no `RemoteBodyDriver`).
- For a remote clone the Prefix returns false, suppressing the native local
  decal/drip-sound creation (the owner's report replaces it).
- `WorldBloodSync.Report` sends one reliable `WorldBloodSpawnMsg` (position +
  ground/wall kind) through the existing world/entity channel.
- `WorldBloodSync.OnReceived` calls `WorldBloodReplay.Play`, which instantiates
  the same prefab at the reported position, adds `GroundBlood` for ground
  decals, applies receiver-side random scale/flip/alpha/rotation, replays a
  `dripN` sound, and destroys after 120 s. Receiver replay runs inside
  `CallContext.RemoteApply`.
- Vomit variants of `BleedParticle` (`vomit=true`, every=1) are intentionally
  not reported; this cycle is scoped to blood decals.
- No body/world state is touched; no snapshot/fallback is added.

## 3. Self-check table (mechanism × change × evidence)

| Mechanism | Change | Evidence |
|-----------|--------|----------|
| Wire | new `WorldBloodSpawn` bidirectional message roundtrips | `WorldBloodSpawnSyncTests.WorldBloodSpawn_RoundTripsEveryField` |
| Star relay | guest report reaches host and relays to the other guest | `WorldBloodSpawnSyncTests.GuestReport_HostFiresTheEvent_AndRelaysToTheOtherGuest` |
| Star relay | host's own decal broadcasts to both guests | `WorldBloodSpawnSyncTests.HostOwnSpawn_BroadcastsToBothGuests` |
| Star relay | relayed event fires on the other guest | `WorldBloodSpawnSyncTests.GuestRelay_FiresTheEventOnTheOtherGuest` |
| Source exclusion | reporting guest does not receive its own decal back | `WorldBloodSpawnSyncTests.UnknownSender_IsNotEchoedToSource` |
| Direction registry | new message classified as bidirectional | `DirectionTests` |
| Adapter surface | `WorldBloodSync.Report` + `WorldBloodReplay.Play` keep their contracts | `WorldBloodPresentationTests` |
| Remote clone suppression | remote clones skip native decal creation | `BleedParticleWorldBloodPatch.IsRemoteClone` (static evidence) + `BleedParticleWorldBloodPatch_HasPrefixAndPostfix` |

## 4. Verification

- **L0 unit**: `WorldBloodSpawnSyncTests` +5, `WorldBloodPresentationTests` +3,
  `DirectionTests` updated; targeted 116 passed, full suite 1529 green.
- **Code gates**: `dotnet build` 0 warnings/0 errors; `dotnet format`;
  check-architecture / check-event-replay / check-entity-event-dispatch pass.
- **Deployment**: `tools/deploy.ps1` to the real game directory; development-period
  rule L0 + static evidence, `no manual acceptance`.
