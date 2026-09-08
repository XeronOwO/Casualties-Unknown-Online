using CasualtiesUnknownOnline.Protocol.Wire;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static CasualtiesUnknownOnline.Tests.Session.PlayerInteractionTestSession;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// Cross-family contract: every direct player interaction is refused when the visibility oracle reports no line of sight.
/// Split from PlayerInteractionServiceTests so xUnit v2 (one collection per class,
/// serial inside a class) does not serialize the whole player-interaction surface.
/// </summary>
public class LineOfSightRefusalTests
{
	[Fact]
	public void Take_BlockedByLineOfSight_IsRefused()
	{
		var (host, guest, received) = CreateBlockedSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: false, Item(42)));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendTakeRequest(HostId, 42);

		Assert.DoesNotContain(KernelEvents(received), e => e.Kind == WireEventKind.PlayerInventoryTransfer);
		Assert.Contains(characters.GetHostCharacterData()!.Items, i => i.InstanceId == 42);
	}

	[Fact]
	public void Carry_BlockedByLineOfSight_IsRefused()
	{
		var (host, guest, received) = CreateBlockedSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: false));

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendCarryStartRequest(HostId);

		Assert.Empty(CarryStates(received));
		Assert.False(host.Services.GetRequiredService<IPlayerInteractionControl>().TryGetCarried(GuestId, out _));
	}

	[Fact]
	public void Heal_BlockedByLineOfSight_IsRefused()
	{
		var (host, guest, received) = CreateBlockedSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(SnapshotWithLimbs(HostId, conscious: false));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, Item(42, "bandage", slot: 0)));

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendHealRequest(HostId);

		Assert.DoesNotContain(KernelEvents(received), e => e.Kind == WireEventKind.PlayerHealResult);
		Assert.Contains(characters.GetSavedCharacter(GuestId)!.Items, i => i.InstanceId == 42);
	}

	[Fact]
	public void Use_BlockedByLineOfSight_IsRefused()
	{
		var (host, guest, received) = CreateBlockedSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(SnapshotWithLimbs(HostId, conscious: true));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, WaterBottle(42)));

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendUseRequest(HostId, 42);

		Assert.DoesNotContain(KernelEvents(received), e => e.Kind == WireEventKind.PlayerItemUseResult);
		Assert.Contains(characters.GetSavedCharacter(GuestId)!.Items, i => i.InstanceId == 42);
	}

	[Fact]
	public void Push_BlockedByLineOfSight_IsRefused()
	{
		var (host, guest, received) = CreateBlockedSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));
		SeedHostEntities(host, GuestId, guestX: 10f);

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendPushRequest(HostId);

		Assert.DoesNotContain(received, r => r.Msg == NetMsg.PlayerPushResult);
	}

	[Fact]
	public void RemoteOperation_BlockedByLineOfSight_IsRefused()
	{
		var (host, guest, received) = CreateBlockedSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true, Item(42)));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendRemoteInventoryOperation(new RemoteInventoryOperationRequestMsg
			{
				Kind = RemoteInventoryOperationKind.Drop,
				OwnerSteamId = HostId,
				ItemInstanceId = 42,
			});

		Assert.DoesNotContain(KernelEvents(received), e => e.Kind is WireEventKind.ItemRelocated or WireEventKind.PlayerInventoryTransfer);
		Assert.Contains(characters.GetHostCharacterData()!.Items, i => i.InstanceId == 42);
	}
}
