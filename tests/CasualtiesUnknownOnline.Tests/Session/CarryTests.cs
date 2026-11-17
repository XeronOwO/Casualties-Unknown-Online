using System.Linq;
using CasualtiesUnknownOnline.Runtime.Configuration;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Xunit;
using static CasualtiesUnknownOnline.Tests.Session.PlayerInteractionTestSession;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// Behavior family: carrying an unconscious player (kernel carry relation, mirror updates, refusal rules).
/// Split from PlayerInteractionServiceTests so xUnit v2 (one collection per class,
/// serial inside a class) does not serialize the whole player-interaction surface.
/// </summary>
[Trait("Category", "Integration")]
public class CarryTests
{
	[Fact]
	public void Guest_StartsCarryingUnconsciousHost_RecordsKernelCarryAndUpdatesMirrors()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: false));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendCarryStartRequest(HostId);

		var state = Assert.Single(CarryStates(received));
		Assert.Equal(GuestId, state.CarrierSteamId);
		Assert.Equal(HostId, state.CarriedSteamId);

		var interaction = host.Services.GetRequiredService<IPlayerInteractionControl>();
		Assert.True(interaction.TryGetCarried(GuestId, out var carried));
		Assert.Equal(HostId, carried);
		Assert.True(interaction.TryGetCarrier(HostId, out var carrier));
		Assert.Equal(GuestId, carrier);

		var guestInteraction = guest.Services.GetRequiredService<IPlayerInteractionControl>();
		Assert.True(guestInteraction.TryGetCarried(GuestId, out var guestCarried));
		Assert.Equal(HostId, guestCarried);
		Assert.True(guestInteraction.TryGetCarrier(HostId, out var guestCarrier));
		Assert.Equal(GuestId, guestCarrier);
	}

	[Fact]
	public void Guest_StartsCarryingHost_CommitsToKernelAndClearsOnStop()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: false));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));
		var playerKernel = host.Services.GetRequiredService<PlayerKernelStatusProjection>();
		playerKernel.Sync(HostId, alive: true, conscious: true);
		playerKernel.Sync(GuestId, alive: true, conscious: true);

		var interaction = guest.Services.GetRequiredService<IPlayerInteractionControl>();
		interaction.SendCarryStartRequest(HostId);

		var authority = host.Services.GetRequiredService<ItemKernelAuthority>();
		var carrier = authority.QueryPlayers()!.Players.Single(p => p.SteamId == GuestId);
		Assert.Equal(HostId, carrier.CarrierOfSteamId);
		var carried = authority.QueryPlayers()!.Players.Single(p => p.SteamId == HostId);
		Assert.Equal(GuestId, carried.CarriedBySteamId);

		interaction.SendCarryStopRequest(HostId);

		carrier = authority.QueryPlayers()!.Players.Single(p => p.SteamId == GuestId);
		Assert.Null(carrier.CarrierOfSteamId);
		carried = authority.QueryPlayers()!.Players.Single(p => p.SteamId == HostId);
		Assert.Null(carried.CarriedBySteamId);
	}

	[Fact]
	public void Host_StartsCarryingUnconsciousGuest_RecordsKernelCarryAndUpdatesGuestMirror()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: false));

		host.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendCarryStartRequest(GuestId);

		var state = Assert.Single(CarryStates(received));
		Assert.Equal(HostId, state.CarrierSteamId);
		Assert.Equal(GuestId, state.CarriedSteamId);

		var guestInteraction = guest.Services.GetRequiredService<IPlayerInteractionControl>();
		Assert.True(guestInteraction.TryGetCarried(HostId, out var guestCarried));
		Assert.Equal(GuestId, guestCarried);
		Assert.True(guestInteraction.TryGetCarrier(GuestId, out var guestCarrier));
		Assert.Equal(HostId, guestCarrier);
	}

	[Fact]
	public void Carry_ConsciousTarget_IsRefused()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true));

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendCarryStartRequest(HostId);

		Assert.Empty(CarryStates(received));
		Assert.False(host.Services.GetRequiredService<IPlayerInteractionControl>().TryGetCarried(GuestId, out _));
	}

	[Fact]
	public void Carry_UnableCarrier_IsRefused()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: false));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: false));

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendCarryStartRequest(HostId);

		Assert.Empty(CarryStates(received));
		Assert.False(host.Services.GetRequiredService<IPlayerInteractionControl>().TryGetCarried(GuestId, out _));
	}

	[Fact]
	public void Carry_ConsciousTarget_IsAllowedByTheHostRule()
	{
		// The user's 2026-11-14 rule family, second switch: with allow_always_carry on, the game's own
		// "only unconscious/dead bodies are carried" rule is lifted, and the relation is announced exactly
		// as the refused one would have been.
		var (host, guest, received) = CreateSession(s => s.Replace(
			ServiceDescriptor.Singleton<IOptionsMonitor<HostRulesOptions>>(
				new MutableOptionsMonitor<HostRulesOptions>(new HostRulesOptions { AllowAlwaysCarry = true }))));
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendCarryStartRequest(HostId);

		var state = Assert.Single(CarryStates(received));
		Assert.Equal(GuestId, state.CarrierSteamId);
		Assert.Equal(HostId, state.CarriedSteamId);
	}

	[Fact]
	public void Carry_UnableCarrier_IsStillRefused_WithTheRuleOn()
	{
		// The switch lifts the game's CARRIED-side rule (a conscious body may be carried). It does not lift
		// the carrier's own state: the kernel's invariant refuses a relation whose carrier is not
		// alive/conscious (PlayerDomainModule.AssertCarryFields), so the interaction layer cannot grant it.
		var (host, guest, received) = CreateSession(s => s.Replace(
			ServiceDescriptor.Singleton<IOptionsMonitor<HostRulesOptions>>(
				new MutableOptionsMonitor<HostRulesOptions>(new HostRulesOptions { AllowAlwaysCarry = true }))));
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: false));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: false));

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendCarryStartRequest(HostId);

		Assert.Empty(CarryStates(received));
	}

	[Fact]
	public void Carry_Stop_ClearsRelationAndBroadcastsKernelClear()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: false));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));

		var interaction = guest.Services.GetRequiredService<IPlayerInteractionControl>();
		interaction.SendCarryStartRequest(HostId);
		interaction.SendCarryStopRequest(HostId);

		var states = CarryStates(received);
		Assert.Equal(2, states.Count);
		Assert.Equal(GuestId, states[0].CarrierSteamId);
		Assert.Equal(HostId, states[0].CarriedSteamId);
		Assert.Equal(GuestId, states[1].CarrierSteamId);
		Assert.Equal(0UL, states[1].CarriedSteamId);

		var hostInteraction = host.Services.GetRequiredService<IPlayerInteractionControl>();
		Assert.False(hostInteraction.TryGetCarried(GuestId, out _));
		Assert.False(hostInteraction.TryGetCarrier(HostId, out _));
	}

	[Fact]
	public void Carry_AlreadyParticipatingInRelation_IsRefused()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: false));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));

		var guestInteraction = guest.Services.GetRequiredService<IPlayerInteractionControl>();
		guestInteraction.SendCarryStartRequest(HostId);

		// The host is already the carried half; the reverse request must be
		// refused (no symmetric/mutual carry in this MVP relation model).
		var hostInteraction = host.Services.GetRequiredService<IPlayerInteractionControl>();
		hostInteraction.SendCarryStartRequest(GuestId);

		Assert.Single(CarryStates(received));
		Assert.True(hostInteraction.TryGetCarried(GuestId, out var carried));
		Assert.Equal(HostId, carried);
		Assert.True(hostInteraction.TryGetCarrier(HostId, out var carrier));
		Assert.Equal(GuestId, carrier);
		Assert.False(hostInteraction.TryGetCarried(HostId, out _));
		Assert.False(hostInteraction.TryGetCarrier(GuestId, out _));
	}
}
