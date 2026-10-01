using CasualtiesUnknownOnline.Runtime.Networking;
using Steamworks;

namespace CasualtiesUnknownOnline.Tests.Fakes;

/// <summary>
/// The send path's Steam calls under test control: the test decides the verdict
/// of every send and counts both the send calls and the diagnostic queries — the
/// facts the refusal policy's bounds are asserted on. The send-side mirror of
/// <see cref="FakeTransport"/>.
/// </summary>
internal sealed class FakeSteamSendChannel : ISteamSendChannel
{
	internal int SendCalls { get; private set; }

	internal int SessionQueries { get; private set; }

	internal int RelayQueries { get; private set; }

	/// <summary>The verdict every send returns.</summary>
	internal EResult Result { get; set; } = EResult.k_EResultOK;

	internal SteamPeerSessionInfo Session { get; set; } = new(
		ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected,
		default,
		"");

	internal SteamRelayStatus Relay { get; set; } = new(false, "", "", "");

	public EResult Send(ulong steamId, byte[] data, bool reliable)
	{
		SendCalls++;
		return Result;
	}

	public SteamPeerSessionInfo DescribeSession(ulong steamId)
	{
		SessionQueries++;
		return Session;
	}

	public SteamRelayStatus DescribeRelay()
	{
		RelayQueries++;
		return Relay;
	}
}
