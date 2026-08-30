# Trader hostile swing presentation sync — self-check (2026-08-24)

The animation audit listed the `TraderScript.Swing` row open
(`TraderScript.cs:548-559`): the hostile trader's one-shot attackAnimation +
swing sound only ran on the side whose local player was attacked, so other
members never saw the swing. This closes the hostile-trader presentation row
with a dedicated event.

## 1. Mechanism inventory (evidence-first)

| Mechanism | Evidence |
|---|---|
| Native hostile swing | `TraderScript.Swing` instantiates `this.attackAnimation`, orients/scales it toward the local body's direction, anchors it at `torso + torso.up * 0.8`, and plays `BSSwing1` (`TraderScript.cs:548-559`). |
| Local damage | The same native method applies the local limb damage (`TraderScript.cs:566-589`); the acting side's damage is already local-compute and must NOT be re-applied on peers. |
| Position key | The trade domain identifies a trader by its world position (`TradeStateSync.PositionTolerance`); a swing replay uses the same key so every member finds the same-position trader. |
| Star semantics | A guest reports its local swing to the host; the host fires the event (for its own replay) and relays to the other guests (source excluded). A host's own swing is sent to every guest. |
| Prefab availability | `TraderScript.attackAnimation` is a public field on the receiver's trader too; the message carries the Resources name, with the receiver's local field as fallback. |

## 2. Changes

- **Wire** — `TraderSwingMsg` (NetMsg 115, bidirectional): `Position`
  (trader key), `Direction` (normalized world-space attack direction) and
  `Prefab` (Resources name of the attackAnimation). `ProtocolVersion` 46 → 47.
- **Capture** — `TraderPatches.TraderSwingPatch` is a thin Postfix on
  `TraderScript.Swing`; it calls `TraderSwingSync.Report`, which reads the
  local body/torso direction and the trader's `attackAnimation.name` and sends
  via `IWorldControl.SendTraderSwing`.
- **Relay** — `TraderSwingHandler` (bidirectional, `IWorldSessionHandlerContext`)
  fires `TraderSwingReceived` and, on the host, relays to every member except
  the source.
- **Replay** — `TraderSwingReplay.Play` loads the reported prefab (fallback:
  the receiver's own `attackAnimation`), orients/scales it with the reported
  direction, anchors it at the local trader's torso, plays `BSSwing1` and
  destroys the clone after 2 s. The replay runs inside `RemoteApply` so the
  Sound.Play capture patch does not re-report it.
- **No trader-state change** — the event is presentation-only; stock,
  reputation, hostility and local damage continue to use the existing trade
  domain and local-compute paths.

## 3. Verification results

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx` | 1358 passed |
| `TraderSwingPresentationTests` | 2 passed (coordinator + replay surfaces locked) |
| `DirectionTests` | `TraderSwing` classified as bidirectional |
| `tools/check-architecture.ps1` | passed |
| `tools/check-event-replay.ps1` / `tools/check-entity-event-dispatch.ps1` | passed (no entity event kind touched) |
| `dotnet format` | run |
| Deploy | `tools/deploy.ps1` to the real game directory succeeded |
| Manual acceptance | Not required by the developer-cycle rule; L0 + static evidence, no manual acceptance. |

## 4. L0 proof

- `TraderSwingPresentationTests` locks `TraderSwingSync.Report(TraderScript)`
  plus its bind/unbind surface, and `TraderSwingReplay.Play(TraderScript,
  TraderSwingMsg)`.
- `DirectionTests` proves the new message is accepted in both directions by
  the runtime direction registry.
- The full suite includes the existing trade-domain tests, so the new event
  does not regress trader state/stock/recruit behavior.
- The `TraderScript.Swing` patch is part of the adapter's `PatchInventory`;
  the existing patch-contract resolver runs against the game assembly and
  validates the new Postfix target.

## 5. Structure review

- `TraderSwingSync` is a focused coordinator with no cross-call state.
- `TraderSwingReplay` is a one-shot display helper with no state.
- `TradeChannel`/`WorldService` gain only a pass-through message path.
- The new NetMsg is a presentation event, not an entity event; no
  `EntityEventKind` tables are touched.
- The acting side's local damage remains outside this sync surface — no
  duplicated/global damage path was created.

## 6. Plan approval

The user instructed this session to pick a backlog item autonomously and
complete it ("由你来自主挑选并完成"), so this cycle's plan is approved
without a separate interactive approval step.
