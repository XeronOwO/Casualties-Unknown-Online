using System;
using System.Linq;
using CasualtiesUnknownOnline.Protocol.Wire;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static CasualtiesUnknownOnline.Tests.Session.PlayerInteractionTestSession;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// Behavior family: healing another player (consumed heal item, limb selection, result event).
/// Split from PlayerInteractionServiceTests so xUnit v2 (one collection per class,
/// serial inside a class) does not serialize the whole player-interaction surface.
/// </summary>
[Trait("Category", "Integration")]
public class HealTests
{
	[Fact]
	public void Guest_HealsUnconsciousHost_ConsumesItemAndSendsResult()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		var items = host.Services.GetRequiredService<IItemControl>();
		characters.SaveHostCharacterData(SnapshotWithLimbs(HostId, conscious: false));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, Item(42, "bandage", slot: 0)));

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendHealRequest(HostId);

		var result = HealResult(received);
		Assert.Equal(GuestId, result.HealerSteamId);
		Assert.Equal(HostId, result.TargetSteamId);
		Assert.Equal(42UL, result.ItemInstanceId);
		Assert.True(result.ItemDestroyed);
		Assert.Equal(1, result.HealedLimbIndex);

		// The host's target limb gained the bandage's skin-heal amount.
		var hostData = characters.GetHostCharacterData()!;
		var healedLimb = hostData.Limbs[result.HealedLimbIndex];
		Assert.True(healedLimb.SkinHealAmount > 0f);
		Assert.True(Math.Abs(healedLimb.SkinHealAmount - 30f) < 0.001f);
		Assert.True(Math.Abs(healedLimb.BandageSlowAmount - 45f) < 0.001f);

		// The healer's item was consumed and its transfer-table entry removed.
		Assert.DoesNotContain(characters.GetSavedCharacter(GuestId)!.Items, i => i.InstanceId == 42);
		Assert.DoesNotContain(items.GetTransferredItems(GuestId), w => w.Item.InstanceId == 42);
	}

	[Fact]
	public void Guest_HealsSelectedLimbOnHost_AppliesRequestedLimbNotAutoPick()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(SnapshotWithLimbs(HostId, conscious: false));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, Item(42, "bandage", slot: 0)));

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendHealRequest(HostId, 42, targetLimbIndex: 0);

		var result = HealResult(received);
		Assert.Equal(0, result.HealedLimbIndex);

		var hostData = characters.GetHostCharacterData()!;
		Assert.True(Math.Abs(hostData.Limbs[0].SkinHealAmount - 30f) < 0.001f);
		Assert.True(Math.Abs(hostData.Limbs[0].BandageSlowAmount - 45f) < 0.001f);
		Assert.Equal(0f, hostData.Limbs[1].SkinHealAmount);
		Assert.Equal(0f, hostData.Limbs[1].BandageSlowAmount);
	}

	[Fact]
	public void Guest_HealResult_ProjectsHealEventOnBothParticipants()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(SnapshotWithLimbs(HostId, conscious: false));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, Item(42, "bandage", slot: 0)));

		PlayerHealResultMsg? hostHeal = null;
		PlayerHealResultMsg? guestHeal = null;
		host.Services.GetRequiredService<IPlayerInteractionControl>().HealReceived += m => hostHeal = m;
		guest.Services.GetRequiredService<IPlayerInteractionControl>().HealReceived += m => guestHeal = m;

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendHealRequest(HostId);

		Assert.NotNull(hostHeal);
		Assert.Equal(GuestId, hostHeal!.HealerSteamId);
		Assert.Equal(HostId, hostHeal.TargetSteamId);
		Assert.NotNull(guestHeal);
		Assert.Equal(GuestId, guestHeal!.HealerSteamId);
		Assert.Equal(HostId, guestHeal.TargetSteamId);
	}

	[Fact]
	public void Host_HealsUnconsciousGuest_SendsResultToGuest()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true, Item(77, "bandage", slot: 0)));
		characters.SaveCharacterData(GuestId, SnapshotWithLimbs(GuestId, conscious: false));

		host.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendHealRequest(GuestId, 77);

		var result = HealResult(received);
		Assert.Equal(HostId, result.HealerSteamId);
		Assert.Equal(GuestId, result.TargetSteamId);
		Assert.Equal(77UL, result.ItemInstanceId);
		Assert.Equal(1, result.HealedLimbIndex);

		// The host's own item is consumed; the guest's saved target limb healed.
		Assert.DoesNotContain(characters.GetHostCharacterData()!.Items, i => i.InstanceId == 77);
		Assert.True(characters.GetSavedCharacter(GuestId)!.Limbs[1].SkinHealAmount > 0f);
	}

	[Fact]
	public void Heal_NoHealItem_IsRefused()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(SnapshotWithLimbs(HostId, conscious: false));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, Item(42, "knife", slot: 0)));

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendHealRequest(HostId);

		Assert.DoesNotContain(KernelEvents(received), e => e.Kind == WireEventKind.PlayerHealResult);
	}

	[Fact]
	public void Heal_UnableHealer_IsRefused()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(SnapshotWithLimbs(HostId, conscious: false));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: false, Item(42, "bandage", slot: 0)));

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendHealRequest(HostId);

		Assert.DoesNotContain(KernelEvents(received), e => e.Kind == WireEventKind.PlayerHealResult);
		Assert.Contains(characters.GetSavedCharacter(GuestId)!.Items, i => i.InstanceId == 42);
	}

	[Fact]
	public void Heal_DeadTarget_IsRefused()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(SnapshotWithLimbs(HostId, conscious: false, alive: false));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, Item(42, "bandage", slot: 0)));

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendHealRequest(HostId);

		Assert.DoesNotContain(KernelEvents(received), e => e.Kind == WireEventKind.PlayerHealResult);
	}

	[Fact]
	public void Heal_PartialCondition_PreservesItemAndUpdatesTransferTable()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		var items = host.Services.GetRequiredService<IItemControl>();
		characters.SaveHostCharacterData(SnapshotWithLimbs(HostId, conscious: false));
		var bandage = Item(42, "bandage", slot: 0);
		bandage.Condition = 1.5f;
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, bandage));
		items.AdoptTransferredItem(GuestId, 42, bandage);

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendHealRequest(HostId, 42);

		var result = HealResult(received);
		Assert.False(result.ItemDestroyed);
		Assert.True(Math.Abs(result.ItemConditionAfter - 0.5f) < 0.001f);

		var saved = characters.GetSavedCharacter(GuestId)!.Items.Single(i => i.InstanceId == 42);
		Assert.True(Math.Abs(saved.Condition - 0.5f) < 0.001f);
		var transferred = items.GetTransferredItems(GuestId).Single(w => w.Item.InstanceId == 42);
		Assert.True(Math.Abs(transferred.Item.Condition - 0.5f) < 0.001f);
	}
}
