using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The co-op location-ping event (LocationPingMsg): a player's middle-click
/// marker reports to the host, the host fires the received event on its own
/// client, and relays it to the other guests. The source already rendered the
/// marker locally, so it is excluded from the relay. One ping = one message;
/// transient UI presentation has no snapshot fallback.
/// </summary>
public class LocationPingSyncTests
{
	private const ulong HostId = 1001;
	private const ulong GuestId = 2001;

	private static LocationPingMsg Ping(LocationPingKind kind = LocationPingKind.Circle, float x = 11f, float y = -4f) => new()
	{
		SenderSteamId = GuestId,
		Position = new NetVector2Msg { X = x, Y = y },
		Kind = kind,
	};

	[Fact]
	public void LocationPing_RoundTripsEveryField()
	{
		var source = Ping(LocationPingKind.Exclamation, x: 3.5f, y: 7.25f);

		var decoded = NetPacket.DecodePayload<LocationPingMsg>(NetPacket.Encode(NetMsg.LocationPing, source));

		Assert.Equal(source.SenderSteamId, decoded.SenderSteamId);
		Assert.Equal(source.Position.X, decoded.Position.X);
		Assert.Equal(source.Position.Y, decoded.Position.Y);
		Assert.Equal(source.Kind, decoded.Kind);
	}

	[Fact]
	public void GuestReport_HostFiresTheEvent_AndRelaysToTheOtherGuest()
	{
		using var w = ItemSimWorld.Create();
		var hostWorld = w.Host.Services.GetRequiredService<IWorldControl>();
		var applied = 0;
		hostWorld.LocationPingReceived += (_, _) => applied++;

		w.G1.Services.GetRequiredService<IWorldControl>().SendLocationPing(Ping());
		w.Driver.Tick(33);

		Assert.True(applied == 1, "the host must fire the received event (the adapter adds the remote marker)");
		Assert.True(w.ReceivedCount(w.G2, NetMsg.LocationPing) == 1, "the host must relay the ping to the other guest");
		Assert.True(w.ReceivedCount(w.G1, NetMsg.LocationPing) == 0, "the source guest must not receive its own ping back");
	}

	[Fact]
	public void HostOwnPing_BroadcastsToBothGuests()
	{
		using var w = ItemSimWorld.Create();

		var msg = Ping(kind: LocationPingKind.Exclamation);
		msg.SenderSteamId = HostId;
		w.Host.Services.GetRequiredService<IWorldControl>().SendLocationPing(msg);
		w.Driver.Tick(33);

		Assert.True(w.ReceivedCount(w.G1, NetMsg.LocationPing) == 1, "the host's own ping must reach G1");
		Assert.True(w.ReceivedCount(w.G2, NetMsg.LocationPing) == 1, "the host's own ping must reach G2");
	}

	[Fact]
	public void GuestRelay_FiresTheEventOnTheOtherGuest()
	{
		using var w = ItemSimWorld.Create();
		var g2World = w.G2.Services.GetRequiredService<IWorldControl>();
		var applied = 0;
		g2World.LocationPingReceived += (_, _) => applied++;

		w.G1.Services.GetRequiredService<IWorldControl>().SendLocationPing(Ping());
		w.Driver.Tick(33);

		Assert.True(applied == 1, "the relayed ping must fire the received event on the other guest");
	}

	[Fact]
	public void UnknownSender_IsNotEchoedToSource()
	{
		using var w = ItemSimWorld.Create();

		w.G1.Services.GetRequiredService<IWorldControl>().SendLocationPing(Ping(x: -2f, y: 9f));
		w.Driver.Tick(33);

		Assert.True(w.ReceivedCount(w.G1, NetMsg.LocationPing) == 0, "the reporting guest must not receive its own ping back");
		Assert.True(w.ReceivedCount(w.G2, NetMsg.LocationPing) == 1, "the other guest still receives the relayed ping");
	}
}
