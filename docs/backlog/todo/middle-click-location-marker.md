# Middle-click location marker (circle → exclamation)

- Status: Todo
- Priority: Medium
- Category: Player interaction / UI / co-op coordination
- Source: KrokMP feature comparison — do not blind-copy; use the behaviour as a reference only

## Goal

Add an in-world co-op location marker controlled by the middle mouse button:

- First middle click in-world places a circle/location marker at the cursor's
  world position.
- Pressing middle click again within a short window (KrokMP uses about 0.2 s)
  upgrades/retargets that marker to an exclamation/alert marker.
- The marker is a short-lived, player-visible ping: peers should see who marked
  where, and it should fade after a few seconds.

## Reference behaviour (KrokMP)

KrokMP implements this as `krokosha_coop_pointfingerat` (KeyCode 325 = middle
mouse), sends a raw `NetDataWriter` packet (`10036`) containing world position +
type, and stores `is_pointingfingerat*` on each `NetPlayer` with hardcoded
`circlething` / `alert2` sprites, a 3-second expiry, and a directional arrow
when the ping is far from the pinger's body.

CUO should **not** copy the custom packet, the NetPlayer singleton fields, or
the hardcoded asset names. It can borrow the interaction feel only: first click
= location circle, quick second click = exclamation/alert.

## Proposed CUO shape (open, needs design approval before code)

1. Input: GameAdapter/plugin captures middle-click in-world, resolves cursor to
   world position, and emits a semantic one-shot "location ping" fact
   (source SteamId, world position, marker type).
2. Transportation: communicate the ping as a dedicated lightweight event over
   the existing session network/state machinery. Do **not** add a generic
   JToken/JObject channel or a new large snapshot.
3. Rendering: all peers render the same ephemeral marker locally. The marker
   owner is just presentation provenance, not simulation authority — no
   host-authoritative world state is required for a transient UI ping.
4. Asset/visual seam: choose a CUO-owned, stable resource path/UI surface rather
   than copying KrokMP's private sprite names.
5. Decide: one active ping per player vs a small transient list; behavior for
   dead/spectating players; whether to expose the ping to mods later.

## Acceptance notes (when implemented)

- Both host and guest see the same marker position and type.
- The double-press window is deterministic and not tied to frame-rate hacks.
- No new generic snapshot/wire protocol; no game/Unity type crosses
  Abstractions if a mod-facing surface is added later.
- Tests cover input edge cases, duplicate pings, expiry, and network fan-out.
