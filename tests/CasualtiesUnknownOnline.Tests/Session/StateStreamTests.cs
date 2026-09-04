using System.Linq;
using CasualtiesUnknownOnline.Protocol.Versioning;
using CasualtiesUnknownOnline.Protocol.Wire;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The player state stream's seq gate (now riding <see cref="StateStreamEnvelope"/>
/// over <see cref="NetMsg.KernelEnvelope"/>): the 20 Hz host→guest stream is
/// unreliable — stale snapshots (reordered) and duplicates must be dropped,
/// newer ones pass. Locked through the real handler over the fake network.
/// </summary>
public class StateStreamTests
{
	private const ulong HostId = 1001;
	private const ulong GuestId = 2001;
	private const ulong LobbyId = 9001;

	private static ProtocolFrame PlayerStreamFrame(uint seq, params WirePlayerStreamState[] players) =>
		new()
		{
			Kind = EnvelopeKind.StateStream,
			StateStream = new StateStreamEnvelope
			{
				Header = new EnvelopeHeader
				{
					ProtocolVersion = ProtocolConstants.EnvelopeVersion,
					SenderId = HostId,
					PayloadType = WirePayloadType.PlayerStateStream,
				},
				Stream = new WireStateStream
				{
					Seq = seq,
					PlayerStates = [.. players],
				},
			},
		};

	private static WirePlayerStreamState PlayerState(ulong epoch, uint counter, float x = 0f) =>
		new()
		{
			EntityId = new WireEntityId { Epoch = epoch, Counter = counter, Generation = 0 },
			Position = new WireVector2 { X = x, Y = 0f },
		};

	[Fact]
	public void StaleAndDuplicateSequences_Dropped_NewerPass()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var entities = guest.Services.GetRequiredService<IEntitySyncControl>();
		var sender = host.Services.GetRequiredService<PacketSender>();

		sender.Send(GuestId, NetMsg.KernelEnvelope, PlayerStreamFrame(seq: 1, PlayerState(1, 99)), reliable: false);
		Assert.Equal(1u, entities.LastStateSeq);

		sender.Send(GuestId, NetMsg.KernelEnvelope, PlayerStreamFrame(seq: 1, PlayerState(1, 99)), reliable: false); // duplicate
		sender.Send(GuestId, NetMsg.KernelEnvelope, PlayerStreamFrame(seq: 0, PlayerState(1, 99)), reliable: false); // stale (reordered)
		Assert.Equal(1u, entities.LastStateSeq);

		sender.Send(GuestId, NetMsg.KernelEnvelope, PlayerStreamFrame(seq: 2, PlayerState(1, 99)), reliable: false);
		Assert.Equal(2u, entities.LastStateSeq);
	}

	[Fact]
	public void PlayerStateBatch_MissingPlayer_DoesNotRemoveExistingBuffer()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var entities = guest.Services.GetRequiredService<IEntitySyncControl>();
		var sender = host.Services.GetRequiredService<PacketSender>();
		var hostEntityId = new NetworkEntityIdMsg(1, 1, 0);
		var guestEntityId = new NetworkEntityIdMsg(1, 2, 0);

		entities.ProcessPlayerJoin(new PlayerJoinMsg
		{
			HostSteamId = HostId,
			GuestSteamId = GuestId,
			HostEntityId = hostEntityId,
			GuestEntityId = guestEntityId,
			HostPosition = new NetVector2Msg(0f, 0f),
			GuestPosition = new NetVector2Msg(10f, 0f),
		});
		Assert.NotNull(entities.GetRemotePlayer(HostId));

		// An update-only stream batch that omits the host must not remove the
		// host buffer; lifecycle is owned by PlayerJoin/PlayerLeave.
		sender.Send(GuestId, NetMsg.KernelEnvelope, PlayerStreamFrame(
			seq: 1,
			PlayerState(1, 2, x: 5f)), reliable: false);

		Assert.NotNull(entities.GetRemotePlayer(HostId));
		Assert.Equal(5f, entities.LocalPlayer.Position.X);
	}

	[Fact]
	public void PlayerLeave_RemovesRemoteBuffer()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var entities = guest.Services.GetRequiredService<IEntitySyncControl>();
		var sender = host.Services.GetRequiredService<PacketSender>();
		var hostEntityId = new NetworkEntityIdMsg(1, 1, 0);
		var guestEntityId = new NetworkEntityIdMsg(1, 2, 0);

		entities.ProcessPlayerJoin(new PlayerJoinMsg
		{
			HostSteamId = HostId,
			GuestSteamId = GuestId,
			HostEntityId = hostEntityId,
			GuestEntityId = guestEntityId,
			HostPosition = new NetVector2Msg(0f, 0f),
			GuestPosition = new NetVector2Msg(10f, 0f),
		});
		Assert.NotNull(entities.GetRemotePlayer(HostId));

		sender.Send(GuestId, NetMsg.PlayerLeave, new PlayerLeaveMsg
		{
			SteamId = HostId,
			EntityId = hostEntityId,
		}, reliable: true);

		Assert.Null(entities.GetRemotePlayer(HostId));
	}

	[Fact]
	public void GuestReport_ReachesHostAndUpdatesSyncedMember()
	{
		using var w = ItemSimWorld.Create();
		w.Host.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene", new NetVector2(0f, 0f));
		w.G1.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene", new NetVector2(10f, 0f));
		w.Driver.Tick(33);

		var hostEntities = w.Host.Services.GetRequiredService<IEntitySyncControl>();
		var member = hostEntities.Members.FirstOrDefault(m => m.SteamId == w.G1.SteamId);
		Assert.NotNull(member);

		var guestProtocol = w.G1.Services.GetRequiredService<IKernelProtocolControl>();
		guestProtocol.SendStateStreamTo(w.Host.SteamId,
			new WireStateStream
			{
				Seq = member.LastReportSeq + 1,
				PlayerStates =
				[
					new WirePlayerStreamState
					{
						EntityId = PlayerStreamWireMapper.ToWireEntityId(member!.Entity.EntityId),
						Position = new WireVector2 { X = 7f, Y = 8f },
					},
				],
			},
			WirePayloadType.PlayerStateStream,
			reliable: false);

		Assert.True(member.LastReportSeq >= 1);
		Assert.Equal(7f, member.Entity.Position.X);
		Assert.Equal(8f, member.Entity.Position.Y);
	}
}
