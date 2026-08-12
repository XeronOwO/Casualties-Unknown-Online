using System;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Networking;

namespace CasualtiesUnknownOnline.Tests.Fakes;

/// <summary>
/// A peer on the fake network. Registers itself with the bus on construction,
/// routes sends through it and surfaces arrivals via <see cref="MessageReceived"/>
/// — the production-behaviour mirror: reliable frames arrive in order (unless
/// faults inject duplicates), unreliable frames may drop, SendTo reports the
/// link state (false when down), never the delivery outcome.
/// </summary>
internal sealed class FakeTransport : INetworkTransport, ICuoService
{
	private readonly FakeNetwork _network;

	internal FakeTransport(ulong steamId, FakeNetwork network)
	{
		SteamId = steamId;
		_network = network;
		_network.Register(this);
	}

	internal ulong SteamId { get; }

	internal FakeNetwork Network => _network;

	public event Action<ulong, byte[]>? MessageReceived;

	public bool SendTo(ulong steamId, byte[] data, bool reliable) => _network.Route(SteamId, steamId, data, reliable);

	public void Poll()
	{
	}

	internal void Deliver(ulong from, byte[] data) => MessageReceived?.Invoke(from, data);

	void ICuoService.Initialize()
	{
	}

	void ICuoService.Start()
	{
	}

	void ICuoService.Update()
	{
	}

	void ICuoService.Stop()
	{
	}

	void IDisposable.Dispose() => _network.Unregister(SteamId);
}
