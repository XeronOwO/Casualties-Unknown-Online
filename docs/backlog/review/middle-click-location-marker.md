# Middle-click location marker (circle → exclamation)

- Status: Review
- Priority: Medium
- Category: Player interaction / UI / co-op coordination
- Source: KrokMP feature comparison — do not blind-copy; use the behaviour as a reference only
- Evidence: `docs/evidence/selfchecks/ui/location-ping-selfcheck.md`

## Goal

Add an in-world co-op location marker controlled by the middle mouse button:

- First middle click in-world places a circle/location marker at the cursor's
  world position.
- Pressing middle click again within a short window upgrades/retargets that
  marker to an exclamation/alert marker.
- The marker is a short-lived, player-visible ping: peers see who marked where,
  and it fades after a few seconds.

## Reference behaviour (KrokMP)

KrokMP implements this as `krokosha_coop_pointfingerat` (KeyCode 325 = middle
mouse), sends a raw `NetDataWriter` packet (`10036`) containing world position +
type, and stores `is_pointingfingerat*` on each `NetPlayer` with hardcoded
`circlething` / `alert2` sprites, a 3-second expiry, and a directional arrow
when the ping is far from the pinger's body.

CUO does **not** copy the custom packet, the NetPlayer singleton fields, or the
hardcoded asset names. It borrows the interaction feel only: first click =
location circle, quick second click = exclamation/alert.

## Implemented CUO shape

1. Input: Plugin captures middle-click in-world (`LocationPingInputHandler`),
   resolves the cursor to world position, and emits a semantic one-shot
   "location ping" fact (source SteamId, world position, marker type).
2. Transportation: the ping travels as one dedicated `LocationPing` event
   (`NetMsg 124`, `LocationPingMsg`) over the existing session star relay.
   No generic JToken/JObject channel or new large snapshot was added.
3. Rendering: all peers render the same ephemeral marker locally through the
   IMGUI `LocationPingOverlay` (on-screen marker or off-screen edge arrow).
   The marker owner is presentation provenance, not simulation authority.
4. Visual seam: CUO-owned IMGUI glyphs (`●` / `!`) and player colors/names —
   no KrokMP private sprites, no new assets.
5. Decisions:
   - **One active ping per player.** A newer placement replaces that player's
     previous ping.
   - **Double-click window:** 400 ms, deterministic via `ITimeSource`; second
     click within the window upgrades a circle to an exclamation and retargets
     to the new cursor position. After the window it starts a fresh circle.
   - **Lifetime:** 5 s with a 1 s fade; expired pings are pruned by the
     Runtime domain.
   - **Dead/spectating:** no ping is placed unless the local player is in an
     active world (`Session.LocalInWorld`); no special dead/spectator path is
     added in this slice.
   - **Mod exposure:** none in this slice. The feature stays in Plugin/Runtime;
     `Abstractions` is not touched.

## Accepted implementation notes

- Both host and guest see the same marker position and type.
- The double-press window is deterministic and frame-rate independent.
- No new generic snapshot/wire protocol; no game/Unity type crosses
  Abstractions.
- Tests cover wire roundtrip, star relay/fan-out, source exclusion, double-click
  upgrade/reset, expiry, session end, local echo drop, invalid-kind drop, and
  direction classification.
