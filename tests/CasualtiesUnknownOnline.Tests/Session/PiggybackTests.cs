using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static CasualtiesUnknownOnline.Tests.Session.PlayerInteractionTestSession;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// Behavior family: piggyback / riding invitations and the carried player's release request.
/// Split from PlayerInteractionServiceTests so xUnit v2 (one collection per class,
/// serial inside a class) does not serialize the whole player-interaction surface.
/// </summary>
public class PiggybackTests
{
	[Fact]
	public void Guest_PiggybacksConsciousHost_RecordsKernelCarryAndUpdatesMirrors()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendPiggybackRequest(HostId);

		var state = Assert.Single(CarryStates(received));
		Assert.Equal(HostId, state.CarrierSteamId);
		Assert.Equal(GuestId, state.CarriedSteamId);

		var interaction = host.Services.GetRequiredService<IPlayerInteractionControl>();
		Assert.True(interaction.TryGetCarried(HostId, out var carried));
		Assert.Equal(GuestId, carried);
		Assert.True(interaction.TryGetCarrier(GuestId, out var carrier));
		Assert.Equal(HostId, carrier);
	}

	[Fact]
	public void Host_PiggybacksConsciousGuest_RecordsKernelCarryAndUpdatesGuestMirror()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));

		host.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendPiggybackRequest(GuestId);

		var state = Assert.Single(CarryStates(received));
		Assert.Equal(GuestId, state.CarrierSteamId);
		Assert.Equal(HostId, state.CarriedSteamId);

		var guestInteraction = guest.Services.GetRequiredService<IPlayerInteractionControl>();
		Assert.True(guestInteraction.TryGetCarried(GuestId, out var guestCarried));
		Assert.Equal(HostId, guestCarried);
		Assert.True(guestInteraction.TryGetCarrier(HostId, out var guestCarrier));
		Assert.Equal(GuestId, guestCarrier);
	}

	[Fact]
	public void Guest_InvitesHostToRideOnGuestBack_RecordsKernelCarryAndUpdatesMirrors()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendCarryOnBackRequest(HostId);

		var state = Assert.Single(CarryStates(received));
		Assert.Equal(GuestId, state.CarrierSteamId);
		Assert.Equal(HostId, state.CarriedSteamId);

		var interaction = host.Services.GetRequiredService<IPlayerInteractionControl>();
		Assert.True(interaction.TryGetCarried(GuestId, out var carried));
		Assert.Equal(HostId, carried);
		Assert.True(interaction.TryGetCarrier(HostId, out var carrier));
		Assert.Equal(GuestId, carrier);
	}

	[Fact]
	public void Host_InvitesGuestToRideOnHostBack_RecordsKernelCarryAndUpdatesGuestMirror()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));

		host.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendCarryOnBackRequest(GuestId);

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
	public void Piggyback_DeadTarget_IsRefused()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(SnapshotWithLimbs(HostId, conscious: false, alive: false));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendPiggybackRequest(HostId);

		Assert.Empty(CarryStates(received));
		Assert.False(host.Services.GetRequiredService<IPlayerInteractionControl>().TryGetCarried(GuestId, out _));
	}

	[Fact]
	public void CarriedPlayer_CanRequestRelease()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));

		var guestInteraction = guest.Services.GetRequiredService<IPlayerInteractionControl>();
		guestInteraction.SendPiggybackRequest(HostId);

		// The carried player (guest) is allowed to end the ride even though it is
		// not the carrier.
		guestInteraction.SendCarryStopRequest(GuestId);

		var states = CarryStates(received);
		Assert.Equal(2, states.Count);
		Assert.Equal(HostId, states[0].CarrierSteamId);
		Assert.Equal(GuestId, states[0].CarriedSteamId);
		Assert.Equal(HostId, states[1].CarrierSteamId);
		Assert.Equal(0UL, states[1].CarriedSteamId);

		var hostInteraction = host.Services.GetRequiredService<IPlayerInteractionControl>();
		Assert.False(hostInteraction.TryGetCarried(HostId, out _));
		Assert.False(hostInteraction.TryGetCarrier(GuestId, out _));
	}

	[Fact]
	public void HostRider_CanRequestReleaseFromGuestCarrier()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));

		// Guest is the carrier and invites the host to ride on the guest's back.
		var guestInteraction = guest.Services.GetRequiredService<IPlayerInteractionControl>();
		guestInteraction.SendCarryOnBackRequest(HostId);

		// The host (rider) requests release through its own local row.
		var hostInteraction = host.Services.GetRequiredService<IPlayerInteractionControl>();
		Assert.True(hostInteraction.TryGetCarrier(HostId, out var carrier));
		Assert.Equal(GuestId, carrier);
		hostInteraction.SendCarryStopRequest(HostId);

		var states = CarryStates(received);
		Assert.Equal(2, states.Count);
		Assert.Equal(GuestId, states[0].CarrierSteamId);
		Assert.Equal(HostId, states[0].CarriedSteamId);
		Assert.Equal(GuestId, states[1].CarrierSteamId);
		Assert.Equal(0UL, states[1].CarriedSteamId);

		Assert.False(hostInteraction.TryGetCarrier(HostId, out _));
		Assert.False(guestInteraction.TryGetCarried(GuestId, out _));
	}
}
