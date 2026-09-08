using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static CasualtiesUnknownOnline.Tests.Session.PlayerInteractionTestSession;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// Behavior family: native use/combine requests and the double-Tab transfer-to-requester path.
/// Split from PlayerInteractionServiceTests so xUnit v2 (one collection per class,
/// serial inside a class) does not serialize the whole player-interaction surface.
/// </summary>
[Trait("Category", "Integration")]
public class NativeApplyRequestTests
{
	[Fact]
	public void HostOwner_NativeUseRequest_RaisesApplyOnHost()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true, Item(42, "waterbottle")));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));

		RemoteInventoryApplyMsg? apply = null;
		host.Services.GetRequiredService<IPlayerInteractionControl>().RemoteInventoryApplyReceived += m => apply = m;

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendRemoteInventoryOperation(new RemoteInventoryOperationRequestMsg
			{
				Kind = RemoteInventoryOperationKind.Use,
				OwnerSteamId = HostId,
				ItemInstanceId = 42,
			});

		Assert.NotNull(apply);
		Assert.Equal(RemoteInventoryOperationKind.Use, apply!.Kind);
		Assert.Equal(HostId, apply.OwnerSteamId);
		Assert.Equal(42UL, apply.ItemInstanceId);
		Assert.Contains(characters.GetHostCharacterData()!.Items, i => i.InstanceId == 42);
	}

	[Fact]
	public void GuestOwner_NativeUseRequest_HostSendsApplyToGuest()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, Item(42, "waterbottle")));

		host.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendRemoteInventoryOperation(new RemoteInventoryOperationRequestMsg
			{
				Kind = RemoteInventoryOperationKind.Use,
				OwnerSteamId = GuestId,
				ItemInstanceId = 42,
			});

		var frame = received.Single(r => r.Msg == NetMsg.RemoteInventoryApply).Frame;
		var apply = NetPacket.DecodePayload<RemoteInventoryApplyMsg>(frame);
		Assert.Equal(RemoteInventoryOperationKind.Use, apply.Kind);
		Assert.Equal(GuestId, apply.OwnerSteamId);
		Assert.Equal(42UL, apply.ItemInstanceId);
	}

	[Fact]
	public void NativeCombine_MissingSecondItem_IsRefusedWithoutApply()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true, Item(42)));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));

		RemoteInventoryApplyMsg? apply = null;
		host.Services.GetRequiredService<IPlayerInteractionControl>().RemoteInventoryApplyReceived += m => apply = m;

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendRemoteInventoryOperation(new RemoteInventoryOperationRequestMsg
			{
				Kind = RemoteInventoryOperationKind.Combine,
				OwnerSteamId = HostId,
				ItemInstanceId = 42,
				TargetItemInstanceId = 999,
			});

		Assert.Null(apply);
		Assert.DoesNotContain(received, r => r.Msg == NetMsg.RemoteInventoryApply);
	}

	[Fact]
	public void Guest_DoubleTabTransfersConsciousHostItemToSelf()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true, Item(42)));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendRemoteInventoryOperation(new RemoteInventoryOperationRequestMsg
			{
				Kind = RemoteInventoryOperationKind.TransferToRequester,
				OwnerSteamId = HostId,
				ItemInstanceId = 42,
			});

		var transfer = TransferResult(received);
		Assert.Equal(HostId, transfer.FromSteamId);
		Assert.Equal(GuestId, transfer.ToSteamId);
		Assert.Equal(42UL, transfer.Item!.InstanceId);
		Assert.DoesNotContain(characters.GetHostCharacterData()!.Items, i => i.InstanceId == 42);
		Assert.Contains(characters.GetSavedCharacter(GuestId)!.Items, i => i.InstanceId == 42);
	}

	[Fact]
	public void Host_DoubleTabTransfersConsciousGuestItemToSelf()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		var items = host.Services.GetRequiredService<IItemControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true));
		var water = WaterBottle(42);
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, water));
		items.AdoptTransferredItem(GuestId, 42, water);

		host.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendRemoteInventoryOperation(new RemoteInventoryOperationRequestMsg
			{
				Kind = RemoteInventoryOperationKind.TransferToRequester,
				OwnerSteamId = GuestId,
				ItemInstanceId = 42,
			});

		var transfer = TransferResult(received);
		Assert.Equal(GuestId, transfer.FromSteamId);
		Assert.Equal(HostId, transfer.ToSteamId);
		Assert.Equal(42UL, transfer.Item!.InstanceId);
		Assert.DoesNotContain(characters.GetSavedCharacter(GuestId)!.Items, i => i.InstanceId == 42);
		Assert.Contains(characters.GetHostCharacterData()!.Items, i => i.InstanceId == 42);
	}
}
