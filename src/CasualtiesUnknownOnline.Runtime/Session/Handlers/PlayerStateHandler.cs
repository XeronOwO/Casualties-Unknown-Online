using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>Host → guest: the authoritative entity batch (unreliable stream, seq gate).</summary>
[PacketHandler(NetMsg.PlayerState)]
public sealed class PlayerStateHandler(SessionService session, EntitySyncService entities, ILogger<PlayerStateHandler> log)
	: PacketHandlerBase<PlayerStateMsg>(session)
{
	private readonly EntitySyncService _entities = entities;
	private readonly ILogger<PlayerStateHandler> _log = log;

	protected override void Handle(ulong sender, PlayerStateMsg msg)
	{
		if (Session.Role != SessionRole.Guest)
		{
			return;
		}

		// Unreliable stream: drop stale snapshots (reordered or duplicate).
		// The broadcast stream has a single source (the host).
		if (msg.Seq <= _entities.LastStateSeq)
		{
			return;
		}

		_entities.LastStateSeq = msg.Seq;

		foreach (var entity in msg.Entities)
		{
			var id = entity.Id.ToNetworkEntityId();
			var target = id == _entities.LocalPlayer.EntityId ? _entities.LocalPlayer
				: _entities.Members.FirstOrDefault(m => m.Entity.EntityId == id)?.Entity;
			if (target is null)
			{
				_log.LogWarning("Dropping entity state {Id} from {Sender}: no member with that entity id.",
					id, sender);
				continue;
			}

			EntitySyncService.ApplyEntityState(entity, target);
		}

		_entities.FireStateReceived(_entities.LocalPlayer);
	}
}
