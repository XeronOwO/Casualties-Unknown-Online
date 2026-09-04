using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Protocol.Wire;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.EntitySync;

/// <summary>
/// The player high-frequency stream exchange: host broadcast, guest report,
/// and both receive directions ride <see cref="StateStreamEnvelope"/> over
/// <see cref="NetMsg.KernelEnvelope"/>. Kept separate from
/// <see cref="EntitySyncService"/> so the entity-table owner stays under the
/// architecture line gate; the exchange only reads the narrow
/// <see cref="IEntitySyncControl"/> surface and the protocol control.
/// </summary>
public sealed class PlayerStreamExchange(
	ISessionControl session,
	IEntitySyncControl entities,
	IKernelProtocolControl kernelProtocol,
	ILogger log)
{
	/// <summary>Guest side: last applied host stream seq (the unreliable-stream gate).</summary>
	public uint LastStateSeq { get; set; }

	private uint _nextStateSeq;

	private uint _nextReportSeq;

	public void ResetSession()
	{
		LastStateSeq = 0;
		_nextStateSeq = 0;
		_nextReportSeq = 0;
	}

	public void BroadcastPlayerState()
	{
		var synced = entities.Members.ToList();
		if (synced.Count == 0)
		{
			return;
		}

		// Send one stream per recipient. A player already knows its own local
		// state, so echoing that entry back wastes bytes on every 20 Hz frame;
		// each guest still receives the host and every other member's state.
		foreach (var target in synced)
		{
			var stream = new WireStateStream
			{
				Seq = ++_nextStateSeq,
				PlayerStates = BuildPlayerStreamList(synced, target.SteamId),
			};
			kernelProtocol.BroadcastStateStreamTo(
				[target.SteamId],
				stream,
				WirePayloadType.PlayerStateStream,
				reliable: false);
		}
	}

	public void SendPlayerStateReport()
	{
		if (session.HostSteamId == 0)
		{
			return;
		}

		kernelProtocol.SendStateStreamTo(session.HostSteamId,
			new WireStateStream
			{
				Seq = ++_nextReportSeq,
				PlayerStates = [entities.LocalPlayer.ToWirePlayerStreamState()],
			},
			WirePayloadType.PlayerStateStream,
			reliable: false);
	}

	public void OnEntityStateStreamReceived(ulong sender, WirePayloadType payloadType, WireStateStream stream)
	{
		if (payloadType != WirePayloadType.PlayerStateStream || stream.PlayerStates.Count == 0)
		{
			return;
		}

		if (session.Role == SessionRole.Guest)
		{
			if (stream.Seq <= LastStateSeq)
			{
				return;
			}

			LastStateSeq = stream.Seq;
			foreach (var state in stream.PlayerStates)
			{
				var id = PlayerStreamWireMapper.ToNetworkEntityId(state.EntityId);
				var target = id == entities.LocalPlayer.EntityId ? entities.LocalPlayer
					: entities.Members.FirstOrDefault(m => m.Entity.EntityId == id)?.Entity;
				if (target is null)
				{
					log.LogWarning("Dropping player stream {Id} from {Sender}: no member with that entity id.",
						id, sender);
					continue;
				}

				entities.ApplyPlayerState(state, target);
			}

			entities.FireStateReceived(entities.LocalPlayer);
			return;
		}

		if (session.Role == SessionRole.Host)
		{
			foreach (var state in stream.PlayerStates)
			{
				var id = PlayerStreamWireMapper.ToNetworkEntityId(state.EntityId);
				var member = entities.Members.FirstOrDefault(m => m.Entity.EntityId == id);
				if (member is null)
				{
					log.LogWarning("Dropping player report {Id} from {Sender}: no synced member owns that entity id.",
						id, sender);
					continue;
				}

				// Each member has its own report sequence space.
				if (stream.Seq <= member.LastReportSeq)
				{
					continue;
				}

				member.LastReportSeq = stream.Seq;
				entities.ApplyPlayerState(state, member.Entity);
				entities.FireStateReceived(member.Entity);
			}
		}
	}

	private List<WirePlayerStreamState> BuildPlayerStreamList(
		List<EntitySyncService.SyncedEntity> synced,
		ulong excludeSteamId = 0)
	{
		var list = new List<WirePlayerStreamState>(synced.Count + 1);
		if (entities.LocalPlayer.SteamId != excludeSteamId)
		{
			list.Add(entities.LocalPlayer.ToWirePlayerStreamState());
		}

		foreach (var member in synced)
		{
			if (member.SteamId != excludeSteamId)
			{
				list.Add(member.Entity.ToWirePlayerStreamState());
			}
		}

		return list;
	}
}
