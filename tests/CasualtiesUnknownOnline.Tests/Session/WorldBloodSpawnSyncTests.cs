using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The world-blood decal event (WorldBloodSpawnMsg): a player's local
/// BleedParticle leaves a visible blood decal in the world; the owner's client
/// reports the one-shot, the host replays it on its own world and relays to the
/// other guests, and directly broadcast host-owned spawns to every guest.
/// One decal = one message; the transient visual has no snapshot fallback.
/// </summary>
public class WorldBloodSpawnSyncTests
{
	private const ulong HostId = 1001;
	private const ulong GuestId = 2001;

	private static WorldBloodSpawnMsg Decal(bool ground = true, float x = 11f, float y = -4f) => new()
	{
		Position = new NetVector2Msg { X = x, Y = y },
		Ground = ground,
	};

	[Fact]
	public void WorldBloodSpawn_RoundTripsEveryField()
	{
		var source = Decal(ground: false, x: 3.5f, y: 7.25f);

		var decoded = NetPacket.DecodePayload<WorldBloodSpawnMsg>(NetPacket.Encode(NetMsg.WorldBloodSpawn, source));

		Assert.Equal(source.Position.X, decoded.Position.X);
		Assert.Equal(source.Position.Y, decoded.Position.Y);
		Assert.Equal(source.Ground, decoded.Ground);
	}

	[Fact]
	public void GuestReport_HostFiresTheEvent_AndRelaysToTheOtherGuest()
	{
		using var w = ItemSimWorld.Create();
		var hostWorld = w.Host.Services.GetRequiredService<IWorldControl>();
		var applied = 0;
		hostWorld.WorldBloodSpawnReceived += (_, _) => applied++;

		w.G1.Services.GetRequiredService<IWorldControl>().SendWorldBloodSpawn(Decal());
		w.Driver.Tick(33);

		Assert.True(applied == 1, "the host must fire the received event (the adapter replays the decal)");
		Assert.True(w.ReceivedCount(w.G2, NetMsg.WorldBloodSpawn) == 1, "the host must relay the decal to the other guest");
		Assert.True(w.ReceivedCount(w.G1, NetMsg.WorldBloodSpawn) == 0, "the source guest must not receive its own decal back");
	}

	[Fact]
	public void HostOwnSpawn_BroadcastsToBothGuests()
	{
		using var w = ItemSimWorld.Create();

		w.Host.Services.GetRequiredService<IWorldControl>().SendWorldBloodSpawn(Decal(ground: false));
		w.Driver.Tick(33);

		Assert.True(w.ReceivedCount(w.G1, NetMsg.WorldBloodSpawn) == 1, "the host's own decal must reach G1");
		Assert.True(w.ReceivedCount(w.G2, NetMsg.WorldBloodSpawn) == 1, "the host's own decal must reach G2");
	}

	[Fact]
	public void GuestRelay_FiresTheEventOnTheOtherGuest()
	{
		using var w = ItemSimWorld.Create();
		var g2World = w.G2.Services.GetRequiredService<IWorldControl>();
		var applied = 0;
		g2World.WorldBloodSpawnReceived += (_, _) => applied++;

		w.G1.Services.GetRequiredService<IWorldControl>().SendWorldBloodSpawn(Decal());
		w.Driver.Tick(33);

		Assert.True(applied == 1, "the relayed decal must fire the received event on the other guest");
	}

	[Fact]
	public void UnknownSender_IsNotEchoedToSource()
	{
		using var w = ItemSimWorld.Create();

		w.G1.Services.GetRequiredService<IWorldControl>().SendWorldBloodSpawn(Decal(x: -2f, y: 9f));
		w.Driver.Tick(33);

		Assert.True(w.ReceivedCount(w.G1, NetMsg.WorldBloodSpawn) == 0, "the reporting guest must not receive its own decal back");
		Assert.True(w.ReceivedCount(w.G2, NetMsg.WorldBloodSpawn) == 1, "the other guest still receives the relayed decal");
	}
}
