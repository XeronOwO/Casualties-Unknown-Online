# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.

- [ ] Design comprehension
      and `docs/design/game/README.md`; the change is Runtime-only (`IpDirectTransport`, `CuoNetworkRouter`,
      `SessionComposition`), no adapter class touched.
- [ ] Native parity
      `NativeBindingParityTests` passed in the same run, 834/0.
- [ ] Native parity probe
      because no declared binding moved.
- [ ] Entry mapping
      invalid frame `:360-364`, exception `:375-378`) and `Disconnect()` (`:227-258`) through `DisconnectPeer`
      (`:385-397`) into the `_disconnected` queue that `Poll` drains (`:221-224`); consumer chain
      `CuoNetworkRouter.OnIpPeerDisconnected` → `PeerDisconnectWatchdog.OnPeerDisconnected`.
- [ ] Mechanism inventory
      host silence (`GuestHostSilenceWatchdog`), transport disconnect (`PeerDisconnectWatchdog`, new); the Steam path's
      link loss remains its P2P session failure.
- [ ] Whole-family audit
      gate's waiting set clears through the same `KickMember` drop the stall watchdog uses, so the member table and the
      gate cannot disagree.
- [ ] Self-check table
      `tests/CasualtiesUnknownOnline.Tests/Session/PeerDisconnectWatchdogTests.cs` cover every policy branch (host drops
      a known member; host ignores a stranger; host ignores its own edge; guest ends on the host's edge; guest ignores
      another member; no session ignores every edge); build reports 0 errors.
- [ ] Verification design
      the boxed guest joins, the host's log must carry `Dropping <peer> (<name>) from the session: the transport lost
      this peer's connection`, and the member must enter the world instead of waiting at the loading screen.
- [ ] Plan approved by the user
      the guest behind, and the suite must stay solid; this change is that direction.
- [ ] Structure review done
      its decision is a pure function returning `PeerLossAction`, the edge travels through the router (the only place
      that knows which transport is active), and DI registers it beside its sibling.
- [ ] Build + dotnet format + dotnet test normative gates pass
      reads 834/0 (gates) and 6650/0 (unit suite); `dotnet format` reported no change.

- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
