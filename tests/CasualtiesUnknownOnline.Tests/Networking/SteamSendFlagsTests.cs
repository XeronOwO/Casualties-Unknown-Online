using CasualtiesUnknownOnline.Runtime.Networking;
using Steamworks;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Networking;

/// <summary>
/// L0 tests for the ISteamNetworkingMessages send-flag fix (#118). The old
/// transport sent with only Reliable/Unreliable, so a broken P2P session
/// (peer close, cert error, rendezvous failure) kept returning
/// <c>k_EResultNoConnection</c> and the next retry had no automatic way to
/// re-establish the session. The helper locks the flag combination for both
/// transport modes.
/// </summary>
public class SteamSendFlagsTests
{
	[Fact]
	public void ReliableSend_IncludesAutoRestartBrokenSession()
	{
		var flags = SteamSendFlags.For(reliable: true);
		Assert.True((flags & Constants.k_nSteamNetworkingSend_Reliable) != 0,
			"reliable sends must keep the reliable flag");
		Assert.True((flags & Constants.k_nSteamNetworkingSend_AutoRestartBrokenSession) != 0,
			"reliable sends must auto-restart a broken Steam P2P session");
	}

	[Fact]
	public void UnreliableSend_IncludesAutoRestartBrokenSession()
	{
		// k_nSteamNetworkingSend_Unreliable is 0 by contract, so the only
		// meaningful bits on an unreliable send are AutoRestartBrokenSession.
		var flags = SteamSendFlags.For(reliable: false);
		Assert.True((flags & Constants.k_nSteamNetworkingSend_AutoRestartBrokenSession) != 0,
			"unreliable sends must auto-restart a broken Steam P2P session");
		Assert.True((flags & Constants.k_nSteamNetworkingSend_Reliable) == 0,
			"an unreliable send must not carry the reliable bit");
	}

	[Fact]
	public void Flags_DoNotLeakTheOppositeReliabilityBit()
	{
		Assert.True((SteamSendFlags.For(reliable: true) & Constants.k_nSteamNetworkingSend_Unreliable) == 0,
			"a reliable send must not also carry the unreliable bit");
		Assert.True((SteamSendFlags.For(reliable: false) & Constants.k_nSteamNetworkingSend_Reliable) == 0,
			"an unreliable send must not also carry the reliable bit");
	}
}
