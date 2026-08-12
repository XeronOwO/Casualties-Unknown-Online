using System;

namespace CasualtiesUnknownOnline.Runtime.Networking;

/// <summary>
/// The transport contract (architecture.md: "abstract as INetworkTransport so
/// it can be swapped for local loopback, LAN UDP, Steam P2P, virtual test
/// network"). One send primitive with the reliable flag, one receive event,
/// one poll. The test suite's FakeTransport implements this surface; the
/// tests defined the contract first (their requirements drove its shape).
/// </summary>
public interface INetworkTransport
{
	/// <summary>Raised when a frame from <c>ulong</c> (peer SteamId) arrives.</summary>
	event Action<ulong, byte[]>? MessageReceived;

	bool SendTo(ulong steamId, byte[] data, bool reliable);

	/// <summary>Drains incoming messages (no-op on the fake — delivery is synchronous).</summary>
	void Poll();
}
