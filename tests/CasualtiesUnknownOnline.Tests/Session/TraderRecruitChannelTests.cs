using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The trader-recruit wire channel: the new guest→host request and host→guest
/// result messages travel over the real receive dispatch and direction table
/// (same fake-network stack as every other message test).
/// </summary>
public class TraderRecruitChannelTests
{
	private const ulong HostId = 1001;
	private const ulong GuestId = 2001;
	private const ulong LobbyId = 9001;

	[Fact]
	public void GuestRecruitRequest_ArrivesAtHost()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		ulong receivedSender = 0;
		TraderRecruitRequestMsg? received = null;
		host.Services.GetRequiredService<IWorldControl>().TraderRecruitRequestReceived += (sender, msg) =>
		{
			receivedSender = sender;
			received = msg;
		};

		guest.Services.GetRequiredService<IWorldControl>().SendTraderRecruitRequest(new TraderRecruitRequestMsg
		{
			TargetSteamId = GuestId,
			TraderPosition = new NetVector2Msg(123f, 45f),
		});

		var driver = new SimulationDriver(guest.Clock, guest.Transport.Network, host, guest);
		driver.TickUntil(() => received is not null, maxMs: 1000);

		Assert.Equal(GuestId, receivedSender);
		Assert.NotNull(received);
		Assert.Equal(GuestId, received!.TargetSteamId);
		Assert.Equal(123f, received.TraderPosition.X);
		Assert.Equal(45f, received.TraderPosition.Y);
	}

	[Fact]
	public void HostRecruitResult_ArrivesAtTargetGuest()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		TraderRecruitResultMsg? received = null;
		guest.Services.GetRequiredService<IWorldControl>().TraderRecruitResultReceived += msg => received = msg;

		host.Services.GetRequiredService<IWorldControl>().SendTraderRecruitResult(GuestId, new TraderRecruitResultMsg
		{
			TargetSteamId = GuestId,
			Health = new CharacterHealthMsg { BrainHealth = 75f, Alive = true, Conscious = true },
		});

		var driver = new SimulationDriver(guest.Clock, guest.Transport.Network, host, guest);
		driver.TickUntil(() => received is not null, maxMs: 1000);

		Assert.NotNull(received);
		Assert.Equal(GuestId, received!.TargetSteamId);
		Assert.Equal(75f, received.Health!.BrainHealth);
	}
}
