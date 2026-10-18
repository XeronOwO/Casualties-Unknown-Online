using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Protocol.Wire;
using CasualtiesUnknownOnline.Runtime.Protocol;
using Microsoft.Extensions.Logging;
using CasualtiesUnknownOnline.Application.Kernel;

namespace CasualtiesUnknownOnline.Runtime.Session.EntitySync;

/// <summary>
/// The player high-frequency stream exchange: host broadcast, guest report,
/// and both receive directions ride <see cref="StateStreamEnvelope"/> over
/// <see cref="NetMsg.KernelEnvelope"/>. Kept separate from
/// <see cref="EntitySyncService"/> so the entity-table owner stays under the
/// architecture line gate; the exchange only reads the narrow
/// <see cref="IEntitySyncControl"/> surface and the protocol control.
///
/// <para>
/// The stream is member-authored, so both receive directions are bounded by the
/// TRANSPORT SENDER: the host accepts a member's row only from that member (the
/// payload's entity id is a claim), and a guest accepts player state only from the
/// host and drops a row naming its own entity (the host excludes each recipient's
/// own row). Both checks run before the buffer write and, on the host, before the
/// member's report-sequence gate — the entity buffer is the host vitals model's
/// input source.
/// </para>
/// </summary>
public sealed class PlayerStreamExchange(
	ISessionControl session,
	IEntitySyncControl entities,
	IKernelProtocolControl kernelProtocol,
	ILogger log) : ISessionReset
{
	/// <summary>Guest side: last applied host stream seq (the unreliable-stream gate).</summary>
	public uint LastStateSeq { get; set; }

	private uint _nextStateSeq;

	private uint _nextReportSeq;

	public void ResetSessionState()
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
			// The host is the only player-state broadcaster a guest accepts. Without this the
			// transport sender would be ignored and a third peer could write another player's
			// buffer — and, naming this client's own entity id, poison the seq gate below or be
			// re-reported to the host as this client's own state. The sender is a fact the receiver
			// holds (decision 265's data anchor), so this is a bound, not a behaviour rule.
			if (sender != session.HostSteamId)
			{
				log.LogWarning("Dropping player stream from {Sender}: only the host broadcasts player state to a guest.",
					sender);
				return;
			}

			if (stream.Seq <= LastStateSeq)
			{
				return;
			}

			LastStateSeq = stream.Seq;
			foreach (var state in stream.PlayerStates)
			{
				var id = PlayerStreamWireMapper.ToNetworkEntityId(state.EntityId);
				if (id == entities.LocalPlayer.EntityId)
				{
					// The host excludes each recipient's own row from its broadcast, so a row naming
					// this client's own entity is misrouted or forged: applying it would overwrite the
					// local simulation and then be relayed back as this client's own report.
					log.LogWarning("Dropping player stream row naming this client's own entity {Id} from {Sender}: the host never echoes a member's own state.",
						id, sender);
					continue;
				}

				var target = entities.Members.FirstOrDefault(m => m.Entity.EntityId == id)?.Entity;
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

				// A member's state is that member's own report: the payload's entity id is a claim,
				// the transport sender is the fact. Checked BEFORE the seq gate, so a forged row can
				// neither advance the owner's report sequence nor steer the owner's entity buffer —
				// which is the host vitals model's input source.
				if (member.SteamId != sender)
				{
					log.LogWarning("Dropping player report for {Member} from {Sender}: a member's state must be reported by that member.",
						member.SteamId, sender);
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
