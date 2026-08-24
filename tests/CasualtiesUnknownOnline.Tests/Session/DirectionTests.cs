using System;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The independent direction contract exercised through
/// <see cref="PacketReceiver.IsValidDirection"/> (backed by
/// <see cref="NetMessageRegistry"/>): a one-way message arriving at the wrong
/// role is dropped before any handler runs. These rows lock the classification
/// — a message whose handler attribute carries the wrong direction fails here.
/// </summary>
public class DirectionTests
{
	private const ulong HostId = 1001;
	private const ulong GuestId = 2001;
	private const ulong LobbyId = 9001;

	public static TheoryData<NetMsg> GuestToHostMessages => new()
	{
		NetMsg.Handshake,
		NetMsg.PlayerStateReport,
		NetMsg.HandshakeAckAck,
		NetMsg.TraderAction,
		NetMsg.ItemUse,
		NetMsg.ItemSlot,
		NetMsg.CarriedInventory,
		NetMsg.ItemContainerContent,
		NetMsg.ModCommandRequest,
		NetMsg.WorldTimeRequest,
		NetMsg.PlayerInventoryTakeRequest,
		NetMsg.PlayerCarryStartRequest,
		NetMsg.PlayerCarryStopRequest,
		NetMsg.PlayerHealRequest,
		NetMsg.TraderRecruitRequest,
	};

	public static TheoryData<NetMsg> HostToGuestMessages => new()
	{
		NetMsg.HandshakeAck,
		NetMsg.WorldStartParams,
		NetMsg.WorldJoin,
		NetMsg.WorldReady,
		NetMsg.PlayerJoin,
		NetMsg.PlayerLeave,
		NetMsg.PlayerState,
		NetMsg.WorldBlockState,
		NetMsg.ItemReject,
		NetMsg.ItemSnapshot,
		NetMsg.HostCharacterData,
		NetMsg.EarthquakeStart,
		NetMsg.ItemMove,
		NetMsg.KeypadCode,
		NetMsg.TrapStateSnapshot,
		NetMsg.GeyserStateSnapshot,
		NetMsg.FluidRegion,
		NetMsg.TraderState,
		NetMsg.ItemCorrection,
		NetMsg.WorldItemsSnapshot,
		NetMsg.ItemCarriedSync,
		NetMsg.OpenedEntitiesSnapshot,
		NetMsg.TrapLayoutSnapshot,
		NetMsg.BuildingEntityHealthSnapshot,
		NetMsg.BlockDamageSnapshot,
		NetMsg.EnemyState,
		NetMsg.EnemySnapshot,
		NetMsg.EnemyAttack,
		NetMsg.ModCommandResult,
		NetMsg.WorldTime,
		NetMsg.ItemCook,
		NetMsg.FluidPresentation,
		NetMsg.PlayerInventoryTransfer,
		NetMsg.PlayerCarryState,
		NetMsg.PlayerHealResult,
		NetMsg.TutorialClawState,
		NetMsg.RadiationLineState,
		NetMsg.TraderRecruitResult,
		NetMsg.WorldSnapshotComplete,
		NetMsg.Kicked,
		NetMsg.Banned,
	};

	public static TheoryData<NetMsg> BidirectionalMessages => new()
	{
		NetMsg.Ping,
		NetMsg.Pong,
		NetMsg.SceneState,
		NetMsg.BlockDamaged,
		NetMsg.CharacterData,
		NetMsg.ItemSpawn,
		NetMsg.ItemPickup,
		NetMsg.ItemDrop,
		NetMsg.ItemDestroy,
		NetMsg.BlockPlaced,
		NetMsg.BuildingEntityDamaged,
		NetMsg.BuildingEntityOpened,
		NetMsg.ItemIdWatermark,
		NetMsg.EntityEvent,
		NetMsg.EntitySpawned,
		NetMsg.FluidInteraction,
		NetMsg.ModMessage,
		NetMsg.CraftReport,
		NetMsg.RecipeUnlock,
		NetMsg.SpeechMsg,
		NetMsg.EnemyBite,
		NetMsg.EnemyLunge,
		NetMsg.EnemyEffect,
		NetMsg.LimbStateEvent,
		NetMsg.CharacterSound,
		NetMsg.CharacterAttackAnim,
		NetMsg.CharacterLandingVisual,
		NetMsg.DynamiteExplosion,
		NetMsg.Chat,
		NetMsg.TraderSwing,
	};

	[Theory]
	[MemberData(nameof(GuestToHostMessages))]
	public void GuestToHost_AllowedOnHost_RejectedOnGuest(NetMsg msg)
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var hostReceiver = host.Services.GetRequiredService<PacketReceiver>();
		var guestReceiver = guest.Services.GetRequiredService<PacketReceiver>();

		Assert.True(hostReceiver.IsValidDirection(msg), $"{msg} must be valid at the host");
		Assert.False(guestReceiver.IsValidDirection(msg), $"{msg} must be dropped at the guest");
	}

	[Theory]
	[MemberData(nameof(HostToGuestMessages))]
	public void HostToGuest_AllowedOnGuest_RejectedOnHost(NetMsg msg)
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var hostReceiver = host.Services.GetRequiredService<PacketReceiver>();
		var guestReceiver = guest.Services.GetRequiredService<PacketReceiver>();

		Assert.True(guestReceiver.IsValidDirection(msg), $"{msg} must be valid at the guest");
		Assert.False(hostReceiver.IsValidDirection(msg), $"{msg} must be dropped at the host");
	}

	[Theory]
	[MemberData(nameof(BidirectionalMessages))]
	public void Bidirectional_AllowedOnBothSides(NetMsg msg)
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var hostReceiver = host.Services.GetRequiredService<PacketReceiver>();
		var guestReceiver = guest.Services.GetRequiredService<PacketReceiver>();

		Assert.True(hostReceiver.IsValidDirection(msg), $"{msg} must be valid at the host");
		Assert.True(guestReceiver.IsValidDirection(msg), $"{msg} must be valid at the guest");
	}

	/// <summary>
	/// The classification-completeness guard: every NetMsg value must appear in
	/// exactly one direction list. The receiver is now fail-closed (an
	/// unregistered id is dropped), but this contract still matters: a new
	/// message that is not listed here (or whose handler attribute does not
	/// match this list) never gets its intended direction enforced.
	/// </summary>
	[Fact]
	public void EveryNetMsg_IsExplicitlyClassified()
	{
		var all = Enum.GetValues(typeof(NetMsg)).Cast<NetMsg>().ToHashSet();
		var classified = GuestToHostMessages
			.Concat(HostToGuestMessages)
			.Concat(BidirectionalMessages)
			.Select(row => (NetMsg)row[0])
			.ToHashSet();

		var missing = all.Except(classified).ToList();
		Assert.True(missing.Count == 0,
			$"every NetMsg must be explicitly classified as g2h / h2g / bidirectional; missing: [{string.Join(", ", missing)}]");
	}
}
