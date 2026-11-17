using System;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Configuration;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
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
	public void Push_HostLocalOutOfReach_IsRefusedOnThePushersOwnClient()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));
		SeedHostEntities(host, GuestId, guestX: 20f);

		// Reach is the pusher's own judgment — with the host as the pusher, the
		// host's own picture of the two bodies decides and the request never leaves.
		host.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendPushRequest(GuestId);

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
	public void Push_RuleOff_RefusesOnThePushersClient_AndNamesTheReason()
	{
		// The user's 2026-11-14 rule family: push is a host switch. The gesture is refused where it was
		// made — a named line on the pusher's own screen — and no request leaves the client.
		var (host, guest, received) = CreateSession(s => s.Replace(
			ServiceDescriptor.Singleton<IOptionsMonitor<HostRulesOptions>>(
				new MutableOptionsMonitor<HostRulesOptions>(new HostRulesOptions { AllowPush = false }))));
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));
		SeedHostEntities(host, GuestId, guestX: 10f);

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendPushRequest(HostId);

		Assert.DoesNotContain(received, r => r.Msg == NetMsg.PlayerPushResult);
		var notices = guest.Services.GetRequiredService<ISessionNotices>();
		Assert.True(notices.TryTake(out var notice), "the refused gesture must read as a line on the pusher's own screen");
		Assert.Contains("Pushing", notice.Text);
	}

	[Fact]
	public void Push_RuleOff_RefusesTheRequestAtTheHostToo()
	{
		// A member's client is never the gate (decision 263's shape): a request formed against a stale
		// announcement is refused again on the host, so the two sides cannot disagree about the rule.
		var (host, guest, received) = CreateSession(s => s.Replace(
			ServiceDescriptor.Singleton<IOptionsMonitor<HostRulesOptions>>(
				new MutableOptionsMonitor<HostRulesOptions>(new HostRulesOptions { AllowPush = false }))));
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));
		SeedHostEntities(host, GuestId, guestX: 10f);

		host.Services.GetRequiredService<IPlayerInteractionControl>()
			.HandlePushRequest(GuestId, new PlayerPushRequestMsg { TargetSteamId = HostId });

		Assert.DoesNotContain(received, r => r.Msg == NetMsg.PlayerPushResult);
	}
}
