using System;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The read-only direction probe shared by the direction-contract test classes
/// (one instance per class, via xUnit <c>IClassFixture</c>). It builds one real
/// handshaken host+guest pair through the production composition and exposes
/// ONLY the two pure queries the contract needs — <see cref="HostAccepts"/> and
/// <see cref="GuestAccepts"/>, which read the static
/// <see cref="NetMessageRegistry"/> plus the session role and mutate nothing.
/// The nodes are private: a test cannot reach the live session, so this is a
/// stateless query facade, not a shared mutable fixture. A test that needs to
/// mutate a session must build its own nodes.
/// </summary>
public sealed class DirectionProbe : IDisposable
{
	private const ulong HostId = 1001;
	private const ulong GuestId = 2001;
	private const ulong LobbyId = 9001;

	private readonly TestNode _host;
	private readonly TestNode _guest;
	private readonly PacketReceiver _hostReceiver;
	private readonly PacketReceiver _guestReceiver;

	public DirectionProbe()
	{
		(_host, _guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		_hostReceiver = _host.Services.GetRequiredService<PacketReceiver>();
		_guestReceiver = _guest.Services.GetRequiredService<PacketReceiver>();
	}

	/// <summary>True when a message arriving at the host role is accepted by the registry.</summary>
	public bool HostAccepts(NetMsg msg) => _hostReceiver.IsValidDirection(msg);

	/// <summary>True when a message arriving at the guest role is accepted by the registry.</summary>
	public bool GuestAccepts(NetMsg msg) => _guestReceiver.IsValidDirection(msg);

	public void Dispose()
	{
		_host.Dispose();
		_guest.Dispose();
	}
}
