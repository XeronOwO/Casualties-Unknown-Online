using System;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static CasualtiesUnknownOnline.Tests.Session.PlayerInteractionTestSession;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// Behavior family: pushing another player (force computation, cooldown, refusal rules).
/// Split from PlayerInteractionServiceTests so xUnit v2 (one collection per class,
/// serial inside a class) does not serialize the whole player-interaction surface.
/// </summary>
[Trait("Category", "Integration")]
public class PushTests
{
	[Fact]
	public void Guest_PushesHost_ComputesForceAndBroadcastsResult()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));
		SeedHostEntities(host, GuestId, guestX: 10f);

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendPushRequest(HostId);

		var frame = received.Single(r => r.Msg == NetMsg.PlayerPushResult).Frame;
		var result = NetPacket.DecodePayload<PlayerPushResultMsg>(frame);
		Assert.Equal(GuestId, result.PusherSteamId);
		Assert.Equal(HostId, result.TargetSteamId);
		Assert.True(result.ForceX < 0f);
		Assert.True(Math.Abs(result.ForceY) < 0.001f);
	}

	[Fact]
	public void Host_PushesGuest_SendsResultToGuest()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));
		SeedHostEntities(host, GuestId, guestX: 10f);

		host.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendPushRequest(GuestId);

		var frame = received.Single(r => r.Msg == NetMsg.PlayerPushResult).Frame;
		var result = NetPacket.DecodePayload<PlayerPushResultMsg>(frame);
		Assert.Equal(HostId, result.PusherSteamId);
		Assert.Equal(GuestId, result.TargetSteamId);
		Assert.True(result.ForceX > 0f);
		Assert.True(Math.Abs(result.ForceY) < 0.001f);
	}

	[Fact]
	public void Push_NotStandingPusher_IsRefused()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));
		SeedHostEntities(host, GuestId, guestX: 10f, standing: false);

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendPushRequest(HostId);

		Assert.DoesNotContain(received, r => r.Msg == NetMsg.PlayerPushResult);
	}

	[Fact]
	public void Push_OutOfReach_IsRefused()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));
		SeedHostEntities(host, GuestId, guestX: 20f);

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendPushRequest(HostId);

		Assert.DoesNotContain(received, r => r.Msg == NetMsg.PlayerPushResult);
	}

	[Fact]
	public void Push_ImmediateSecondRequest_IsRefusedByCooldown()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));
		SeedHostEntities(host, GuestId, guestX: 10f);

		var interaction = guest.Services.GetRequiredService<IPlayerInteractionControl>();
		interaction.SendPushRequest(HostId);
		interaction.SendPushRequest(HostId);

		Assert.Equal(1, received.Count(r => r.Msg == NetMsg.PlayerPushResult));
	}

	[Fact]
	public void Push_CarryRelation_IsRefused()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: false));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));
		SeedHostEntities(host, GuestId, guestX: 10f);

		var guestInteraction = guest.Services.GetRequiredService<IPlayerInteractionControl>();
		guestInteraction.SendCarryStartRequest(HostId);
		guestInteraction.SendPushRequest(HostId);

		Assert.DoesNotContain(received, r => r.Msg == NetMsg.PlayerPushResult);
	}
}
