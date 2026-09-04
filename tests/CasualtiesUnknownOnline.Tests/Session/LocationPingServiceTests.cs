using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The local location-ping domain: one marker per player, middle-click
/// double-window upgrade/reset, expiry pruning, session-end clearing, and
/// echo/invalid-kind rejection. The wire star relay is covered by
/// <see cref="LocationPingSyncTests"/>.
/// </summary>
public class LocationPingServiceTests
{
	private const ulong HostId = 1001;
	private const ulong GuestId = 2001;
	private const ulong LobbyId = 9001;

	private static (TestNode Host, TestNode Guest) CreateInWorldPair()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		guest.Session.ReportSceneState(SceneStateType.InWorld, "test");
		host.Session.ReportSceneState(SceneStateType.InWorld, "test");
		return (host, guest);
	}

	[Fact]
	public void FirstClick_CreatesCircleAndAddsLocalPing()
	{
		var (_, guest) = CreateInWorldPair();
		var pings = guest.Services.GetRequiredService<ILocationPingControl>();

		Assert.True(pings.TryPlace(1f, 2f));

		var active = Assert.Single(pings.ActivePings);
		Assert.Equal(GuestId, active.SenderSteamId);
		Assert.Equal(LocationPingKind.Circle, active.Kind);
		Assert.Equal(1f, active.X);
		Assert.Equal(2f, active.Y);
	}

	[Fact]
	public void SecondClickWithinWindow_UpgradesCircleToExclamationAndRetargets()
	{
		var (_, guest) = CreateInWorldPair();
		var pings = guest.Services.GetRequiredService<ILocationPingControl>();
		Assert.True(pings.TryPlace(1f, 2f));
		guest.Clock.Advance(200);

		Assert.True(pings.TryPlace(3f, 4f));

		var active = Assert.Single(pings.ActivePings);
		Assert.Equal(LocationPingKind.Exclamation, active.Kind);
		Assert.Equal(3f, active.X);
		Assert.Equal(4f, active.Y);
	}

	[Fact]
	public void SecondClickAfterWindow_StartsANewCircle()
	{
		var (_, guest) = CreateInWorldPair();
		var pings = guest.Services.GetRequiredService<ILocationPingControl>();
		Assert.True(pings.TryPlace(1f, 2f));
		guest.Clock.Advance(500);

		Assert.True(pings.TryPlace(5f, 6f));

		var active = Assert.Single(pings.ActivePings);
		Assert.Equal(LocationPingKind.Circle, active.Kind);
		Assert.Equal(5f, active.X);
		Assert.Equal(6f, active.Y);
	}

	[Fact]
	public void TryPlace_WithoutLocalInWorld_ReturnsFalse()
	{
		var (_, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var pings = guest.Services.GetRequiredService<ILocationPingControl>();

		Assert.False(pings.TryPlace(1f, 2f));
		Assert.Empty(pings.ActivePings);
	}

	[Fact]
	public void Prune_RemovesExpiredPings()
	{
		var (_, guest) = CreateInWorldPair();
		var pings = guest.Services.GetRequiredService<ILocationPingControl>();
		Assert.True(pings.TryPlace(1f, 2f));
		guest.Clock.Advance(LocationPingService.LifetimeMs + 1);

		pings.Prune();

		Assert.Empty(pings.ActivePings);
	}

	[Fact]
	public void SessionEnd_ClearsActivePings()
	{
		var (_, guest) = CreateInWorldPair();
		var pings = guest.Services.GetRequiredService<ILocationPingControl>();
		Assert.True(pings.TryPlace(1f, 2f));

		guest.Session.EndSession();

		Assert.Empty(pings.ActivePings);
	}

	[Fact]
	public void ReceivedLocalEcho_IsDropped()
	{
		var (_, guest) = CreateInWorldPair();
		var pings = guest.Services.GetRequiredService<ILocationPingControl>();
		var world = guest.Services.GetRequiredService<IWorldControl>();

		world.FireLocationPingReceived(0, new LocationPingMsg
		{
			SenderSteamId = GuestId,
			Position = new NetVector2Msg(1f, 2f),
			Kind = LocationPingKind.Exclamation,
		});

		Assert.Empty(pings.ActivePings);
	}

	[Fact]
	public void ReceivedRemotePing_AddsMarker()
	{
		var (_, guest) = CreateInWorldPair();
		var pings = guest.Services.GetRequiredService<ILocationPingControl>();
		var world = guest.Services.GetRequiredService<IWorldControl>();

		world.FireLocationPingReceived(HostId, new LocationPingMsg
		{
			SenderSteamId = HostId,
			Position = new NetVector2Msg(7f, 8f),
			Kind = LocationPingKind.Circle,
		});

		var active = Assert.Single(pings.ActivePings);
		Assert.Equal(HostId, active.SenderSteamId);
		Assert.Equal(LocationPingKind.Circle, active.Kind);
	}

	[Fact]
	public void ReceivedInvalidKind_IsDropped()
	{
		var (_, guest) = CreateInWorldPair();
		var pings = guest.Services.GetRequiredService<ILocationPingControl>();
		var world = guest.Services.GetRequiredService<IWorldControl>();

		world.FireLocationPingReceived(HostId, new LocationPingMsg
		{
			SenderSteamId = HostId,
			Position = new NetVector2Msg(7f, 8f),
			Kind = (LocationPingKind)99,
		});

		Assert.Empty(pings.ActivePings);
	}

	[Fact]
	public void HostSpoofedSender_IsDropped()
	{
		var (host, _) = CreateInWorldPair();
		var pings = host.Services.GetRequiredService<ILocationPingControl>();
		var world = host.Services.GetRequiredService<IWorldControl>();

		world.FireLocationPingReceived(GuestId, new LocationPingMsg
		{
			SenderSteamId = HostId,
			Position = new NetVector2Msg(7f, 8f),
			Kind = LocationPingKind.Exclamation,
		});

		Assert.Empty(pings.ActivePings);
	}
}
