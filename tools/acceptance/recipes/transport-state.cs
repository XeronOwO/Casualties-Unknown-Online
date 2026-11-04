// recipe: transport-state
// args: peer=s
// serves: every session row whose failure is "the clients are up and the world never receives the member"
// returns: ok, peer, relay, relayAnyRelay, relayConfig, relayDebug, session, endReason, endDebug, ping, error, detail
//
// What the Steam P2P layer says about ONE peer RIGHT NOW: the two queries production's own failure path prints
// (SteamSendChannel.DescribeSession / DescribeRelay), called here so a batch can name a transport that accepts its sends and
// delivers nothing. `peer` is a decimal SteamId. `session` is the ISteamNetworkingMessages connection state itself (the call
// answers the state, not a boolean); `None` while both sides keep sending is the "queued, never delivered" shape, and a relay
// that is not `Current` is Steam's own reason for it. ONE LINE: the evaluator compiles a submission as one top-level expression
// and a multi-line body is refused as `(1,1): InteractiveHost` (measured 2026-10-27, and its return type cost one more bisect
// 2026-11-04 — the call answers ESteamNetworkingConnectionState, so a probe must compare it, never read it as a flag).
((System.Func<string>)(() => { try { var peer = System.UInt64.Parse({{s:peer}}, System.Globalization.CultureInfo.InvariantCulture); var relay = new Steamworks.SteamRelayNetworkStatus_t(); var availability = Steamworks.SteamNetworkingUtils.GetRelayNetworkStatus(out relay); var identity = new Steamworks.SteamNetworkingIdentity(); identity.SetSteamID64(peer); Steamworks.SteamNetConnectionInfo_t info; Steamworks.SteamNetConnectionRealTimeStatus_t realtime; var state = Steamworks.SteamNetworkingMessages.GetSessionConnectionInfo(ref identity, out info, out realtime); var endDebug = info.m_szEndDebug ?? ""; var relayDebug = relay.m_debugMsg ?? ""; var inv = System.Globalization.CultureInfo.InvariantCulture; return "{\"ok\":true,\"peer\":\"" + peer.ToString(inv) + "\",\"relay\":\"" + availability + "\",\"relayAnyRelay\":\"" + relay.m_eAvailAnyRelay + "\",\"relayConfig\":\"" + relay.m_eAvailNetworkConfig + "\",\"relayDebug\":\"" + relayDebug.Replace("\\", "/").Replace("\"", "'") + "\",\"session\":\"" + state + "\",\"endReason\":" + info.m_eEndReason.ToString(inv) + ",\"endDebug\":\"" + endDebug.Replace("\\", "/").Replace("\"", "'") + "\",\"ping\":" + realtime.m_nPing.ToString(inv) + "}"; } catch (System.Exception e) { return "{\"ok\":false,\"error\":\"" + e.GetType().Name + "\",\"detail\":\"" + e.Message.Replace("\\", "/").Replace("\"", "'") + "\"}"; } }))()
