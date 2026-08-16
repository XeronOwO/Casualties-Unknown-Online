using System;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The one-way direction table (PacketReceiver.IsValidDirection): a one-way
/// message arriving at the wrong role is dropped before any handler runs.
/// These rows lock the table — a message added without its direction rule
/// fails here when the direction is wrong.
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
		NetMsg.ModCommandRequest,
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
	/// exactly one direction list. Without this, a new message (or a forgotten
	/// one — observed: SpeechMsg was bidirectional but unlisted, silently
	/// falling into IsValidDirection's default-true) never gets its direction
	/// locked, and a one-way message could regress to bidirectional.
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
