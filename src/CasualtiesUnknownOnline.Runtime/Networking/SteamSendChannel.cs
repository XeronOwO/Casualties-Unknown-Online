using System;
using Steamworks;

namespace CasualtiesUnknownOnline.Runtime.Networking;

/// <summary>
/// Production <see cref="ISteamSendChannel"/> and the only place the send path
/// touches Steamworks: the identity construction, the unsafe fixed buffer and
/// the three calls moved here verbatim from <see cref="SteamTransport"/>. Keeping
/// them in one small adapter is what leaves the transport's refusal decisions
/// free of Steam state (and testable).
/// </summary>
internal sealed class SteamSendChannel : ISteamSendChannel
{
	public EResult Send(ulong steamId, byte[] data, bool reliable)
	{
		var identity = new SteamNetworkingIdentity();
		identity.SetSteamID64(steamId);

		var flags = SteamSendFlags.For(reliable);

		unsafe
		{
			fixed (byte* pData = data)
			{
				return SteamNetworkingMessages.SendMessageToUser(
					ref identity, (IntPtr)pData, (uint)data.Length, flags, 0);
			}
		}
	}

	public SteamPeerSessionInfo DescribeSession(ulong steamId)
	{
		var identity = new SteamNetworkingIdentity();
		identity.SetSteamID64(steamId);

		var state = SteamNetworkingMessages.GetSessionConnectionInfo(ref identity, out var info, out _);
		return new SteamPeerSessionInfo(state, (ESteamNetConnectionEnd)info.m_eEndReason, info.m_szEndDebug);
	}

	public SteamRelayStatus DescribeRelay()
	{
		var availability = SteamNetworkingUtils.GetRelayNetworkStatus(out var relay);
		return new SteamRelayStatus(
			availability == ESteamNetworkingAvailability.k_ESteamNetworkingAvailability_Current,
			relay.m_eAvailAnyRelay.ToString(),
			relay.m_eAvailNetworkConfig.ToString(),
			relay.m_debugMsg);
	}
}
