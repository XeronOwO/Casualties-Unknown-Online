# Tech Decisions Index

Traceability index of original decision numbers. The active normative register is
[`tech-decisions.md`](active.md); historical delivery detail is in
[`tech-decisions-archive.md`](archive.md); Phase A–E detail is in
[`architecture-evolution/phase-decisions.md`](../architecture/phase-decisions.md).
Some early entries share numbers (`15` and `30` appear twice); the file order is
authoritative.

| # | Decision |
|---:|---|
| 1 | Technical stack & toolchain |
| 2 | Wire transport (landed 2026-08-07) |
| 3 | Session layer (landed 2026-08-08) |
| 4 | Item physics (landed 2026-08-09/10, user mandate; terminal form #124) |
| 5 | World entity events (landed 2026-08-10, #123) |
| 6 | Patch contracts + contract tests (landed 2026-08-12, the game-update guard) |
| 7 | Replay archive + regression (landed 2026-08-12) |
| 8 | Entity-event behavior suite (landed 2026-08-12, Phase 5) |
| 9 | Mod API first round (landed 2026-08-13; `docs/api/mod-api.md` is the binding contract) |
| 10 | Crafting domain (landed 2026-08-13) |
| 11 | Cross-domain fix round (2026-08-13 — #191/#192/#194) |
| 12 | Reconnect-restore rounds (2026-08-13, ProtocolVersion 5) |
| 13 | Test-hardening round (2026-08-13, 499 → 545 tests) |
| 14 | Trap layout authority (landed 2026-08-14, ProtocolVersion 6) |
| 15 | Guest-leave must never end the host's session (fixed 2026-08-14) |
| 15 | Multiplayer enemy targeting + host-ordered attacks (ProtocolVersion 7) |
| 16 | Runtime enemy spawn binding (ProtocolVersion 8) |
| 17 | Enemy proximity effects + host-local lunge report (ProtocolVersion 9) |
| 18 | Lobby-domain lifecycle refactor (2026-08-15) |
| 19 | Mod API second round — permissions, host commands, dependencies, SemVer (ProtocolVersion 10) |
| 20 | Damaged building-entity health snapshot (ProtocolVersion 11) |
| 21 | Partial block-damage snapshot + metallic damage multiplier (ProtocolVersion 12) |
| 22 | World-time flow — host-authoritative fast-forward + all-unconscious sleep (ProtocolVersion 13) |
| 23 | CrystalMimic trigger sync — one-shot latch event + EntitySpawned enemies (ProtocolVersion 14) |
| 24 | In-flight pickup queue — bounded hold instead of immediate UnknownItem reject |
| 25 | Config foundation — BepInEx ConfigFile → IOptionsMonitor + logging levels + state-stream cadence |
| 26 | Heater cooker meat→steak conversion — one ItemCook event (ProtocolVersion 15) |
| 27 | Character-data disk persistence (no protocol change) |
| 28 | Tutorial-claw props are per-player until pickup (no protocol change) |
| 29 | Limb/death/bleed/mining presentation sync — LimbStateEvent + SwingSeq (ProtocolVersion 16) |
| 30 | Character action sounds — one CharacterSound event + native block/building sound paths (ProtocolVersion 17) |
| 31 | Weapon-fire direction + recoil — no new message, gunangle kick rides CharacterSound (ProtocolVersion 18) |
| 32 | Periodic keyframe self-heals world-item top-level state (no protocol change) |
| 33 | Cross-player carry/release (ProtocolVersion 27) |
| 34 | Cross-player heal (ProtocolVersion 28) |
| 35 | GameAdapter construction readability split (no protocol change) |
| 36 | Tutorial-claw 20 Hz presentation stream (ProtocolVersion 29) |
| 37 | Mod-state saves (no protocol bump) |
| 30 | Mod UI — local immediate-mode windows (no protocol bump) |
| 38 | Mod content registration (no protocol bump) |
| 39 | Whole-protocol network traffic monitor (no protocol bump) |
| 40 | Dynamite detonation sync — dedicated player-item explosion event (ProtocolVersion 30) |
| 41 | GrapplingHook presentation sync and clone owner-local script isolation (no protocol bump) |
| 42 | Remote player container content view — recursive Online UI projection (no protocol bump) |
| 43 | Heal item selector — explicit Online UI medical item picker (no protocol bump) |
| 44 | LookTarget gaze/scare — remote clone presentation via the player entity stream (ProtocolVersion 31) |
| 45 | Network health metrics — RTT history / jitter / probe loss (no protocol bump) |
| 46 | Mod ReadGameState — read-only player character projection (no protocol bump) |
| 47 | Remote clone FacialExpression disfigurement/eye-loss presentation (ProtocolVersion 32) |
| 48 | Animal death presentation replay on remote kills (no protocol bump) |
| 49 | Mod entity spawn — permission-gated native prefab replication (no protocol bump) |
| 50 | AccessNativeApi — curated read-only native operation registry (no protocol bump) |
| 51 | Gun state reports — persistent GunScript transitions ride the item-use fact path (no protocol bump) |
| 52 | Liquidcentrifuge cooldown — persistent `CustomItemBehaviour.data[0]` state (no protocol bump) |
| 53 | Dynamite lit-fuse presentation — synthetic fuse field rides item state (no protocol bump) |
| 54 | WorldService / ItemService partial split (no protocol bump) |
| 55 | RadiationLine world-state sync (ProtocolVersion 33) |
| 56 | CrystalTeleport sync — repeatable teleport-laugh/flash event (ProtocolVersion 34) |
| 57 | Owner-local body auto-events — clone suppression (no protocol change) |
| 58 | RadiationLine straggler pressure — multiplayer activation rule (no protocol change) |
| 59 | Trader Recruit — host-authoritative co-op revive (ProtocolVersion 35) |
| 60 | Revive / respawn rules — next-level auto-respawn + host rules (no protocol bump) |
| 61 | Text chat — host-relayed co-op chat line (ProtocolVersion 36) |
| 62 | Trader Recruit random trader-stock bonus items (ProtocolVersion 37) |
| 63 | NetMsg direction registry — fail-closed protocol metadata (no protocol bump) |
| 64 | World-entry snapshot completion + fan-out ownership (ProtocolVersion 38) |
| 65 | Partial-aware architecture gate + debt ledger (no protocol change) |
| 66 | PlayerInteractionService flattening — real responsibility split (no protocol change) |
| 67 | ItemApplication cook-replay split — real top-level responsibility (no protocol change) |
| 68 | EnemySyncCoordinator combat-replay split — real top-level responsibility (no protocol change) |
| 69 | WorldService message-flow split — real top-level responsibilities (no protocol change) |
| 70 | ItemService message-flow split — real top-level responsibilities (no protocol change) |
| 71 | ModService split — real top-level responsibilities (no protocol change) |
| 72 | GameAdapter split — real top-level responsibilities (no protocol change) |
| 73 | HandlerContext per-domain narrowing — capability interfaces, no protocol change |
| 74 | Minimal host-rules service + late-join gate + Plugin registrar split (no wire change) |
| 75 | GameAdapter concrete-service dependency narrowing (no wire change) |
| 76 | Host kick — dedicated Kicked message (ProtocolVersion 39) |
| 77 | Host ban — dedicated Banned message + persisted list (ProtocolVersion 40) |
| 78 | Online UI window — full tabbed multiplayer UI (no protocol bump) |
| 79 | I18n framework — en/zh localization for the CUO UI (no protocol bump) |
| 80 | Online window modal input blocker (no protocol bump) |
| 81 | Lobby leave / close + host rules in-game editor (no protocol bump) |
| 82 | IP direct connection — non-Steam TCP transport (no protocol bump) |
| 83 | Character attack-animation sync — dedicated one-shot visual event (ProtocolVersion 41) |
| 84 | In-world right-click player interaction menu (no protocol bump) |
| 85 | Direct placeable-item ArmsSwing sync (no protocol bump) |
| 86 | Online UI polish + idempotent world-time resend (no protocol bump) |
| 87 | Workout/exercise animation sync — player entity stream (ProtocolVersion 42) |
| 88 | Nap variant + dog-shake intensity — player entity stream (ProtocolVersion 43) |
| 89 | Gun muzzle-flash particle replay — existing GunFire event (no protocol bump) |
| 90 | Wall-slide + landing presentation sync (ProtocolVersion 44) |
| 91 | Spider enemy presentation — leg IK targets + bite claw replay (ProtocolVersion 45) |
| 92 | CrystalEnemy wind-up telegraph line sync (ProtocolVersion 47) |
| 93 | Trader hostile swing presentation sync (ProtocolVersion 47) |
| 94 | Online UI player awareness: off-screen distance, per-player colors, overlapping target selection |
| 95 | Co-op custom run-settings range broadening (no protocol change) |
| 96 | Cross-player consumable use (ProtocolVersion 48) |
| 97 | Piggyback (conscious-alive ride) + carried-player release |
| 98 | Cross-player medicine/injectable use (second cross-player item-use slice) |
| 99 | Cross-player push/shove (ProtocolVersion 49) |
| 100 | Cross-player topical use (third cross-player item-use slice) |
| 102 | Cross-player opiate use (fourth cross-player item-use slice) |
| 103 | Cross-player limb-tool use (fifth cross-player item-use slice) |
| 104 | Hot-path latency instrumentation |
| 105 | Cross-player component-bearing limb tools |
| 106 | Cross-player wearable use |
| 107 | Cross-player component medicine (analgesicgauze opiate component) |
| 108 | Cross-player shrapnel and timed tool use |
| 109 | Cross-player timed/random liquid medicine (injectable branches) |
| 110 | Cross-player drinkable medicine |
| 111 | Dedicated standalone player-interaction quick panel |
| 112 | Player-interaction carry/piggyback follow-ups |
| 113 | Piggyback drop cleanup — release must update the driver immediately |
| 114 | Player ragdoll-toggle presentation sync |
| 115 | Player world-blood decal presentation sync |
| 116 | Online UI scoped anti-passthrough + transport-mode exclusivity |
| 117 | Remote inventory UI follow-up — openable containers + host take toggle |
| 118 | Native remote backpack view + shuttle-door trigger sound live replay |
| 119 | Ragdoll one-shot stale-state / clone-creation race fix |
| 120 | Remote container destroy authority — display-proxy destroy containment |
| 121 | Piggyback release facing — shared BodyFacing rule |
| 122 | Remote backpack container take — recursive cross-player take + native drag take |
| 123 | Remote-backpack drag escape — display-proxy release containment |
| 124 | Direct player-interaction line-of-sight / visibility gate (no protocol change) |
| 125 | Retire legacy F7/F8/F9 session hotkeys (no protocol change) |
| 126 | Phase A shadow kernel — typed deterministic GameState beside the item path |
| 127 | Phase B — items become the first authoritative kernel domain |
| 128 | Phase C protocol/save core — completed cutover (2026-08-28) |
| 129 | High-frequency player/enemy streams moved to StateStreamEnvelope (2026-08-29) |
| 130 | Push is transient presentation, not a kernel fact (2026-08-29) |
| 131 | Take/heal/use results ride journal-only kernel events (2026-08-29) |
| 132 | Enemy combat results ride journal-only kernel events (2026-08-29) |
| 133 | Fluid guest kernel read projection (2026-08-29) |
| 134 | Destroyed building entities cannot be revived by health reports (2026-08-29) |
| 135 | Enemy aggregate removal rides kernel batches (2026-08-29) |
| 136 | Enemy removal terminal tombstones in kernel (2026-08-29) |
| 137 | Protocol version baseline before first release (2026-08-29) |
| 138 | Trap state machine kernel shadow (2026-08-29) |
| 139 | Trap state live production reporting (2026-08-29) |
| 140 | Guest checkpoint projection of non-one-shot trap states (2026-08-29) |
| 141 | Atomic composite kernel commands (2026-08-29) |
| 142 | Trap trigger kernel facts ride one atomic composite (2026-08-29) |
| 143 | Building-death drop provenance markers (2026-08-29) |
| 144 | Destructive trap item drops ride one atomic composite (2026-08-29) |
| 145 | Guest fluid kernel read projection reaches the Game Adapter (2026-08-29) |
| 146 | Enemy combat policy extraction (2026-08-29) |
| 147 | Enemy target resolver extraction (2026-08-29) |
| 148 | Enemy combat order policy extraction (2026-08-30) |
| 149 | Spider-bite local-path handoff to the order policy (2026-08-30) |
| 150 | Fluids guest projection and convergence semantics (2026-08-30) |
| 151 | WorldEntities 4.2 checklist closure (2026-08-30) |
| 152 | Player durable skills move into the Players kernel domain (2026-08-30) |
| 153 | Player kernel identity floor on entity-sync start (2026-08-30) |
| 154 | Explicit cross-player interaction authority policies (2026-08-30) |
| 155 | Carry carrier liveness invariant (2026-08-30) |
| 156 | Player/item ownership consistency and death preservation (2026-08-30) |
| 157 | Phase D 4.3 closure: cross-player prediction/rollback boundary (2026-08-30) |
| 158 | Phase E kernel reset centralization and guard suite (2026-08-30) |
| 159 | Cooperative manual world-time acceleration (2026-08-31) |

