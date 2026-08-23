using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>Host → guest: the authoritative entity batch (unreliable stream, seq gate).</summary>
[PacketHandler(NetMsg.PlayerState, NetMessageDirection.HostToGuest)]
public sealed class PlayerStateHandler(ILogger<PlayerStateHandler> log) : PacketHandlerBase<PlayerStateMsg, IEntitySessionHandlerContext>
{
	private readonly ILogger<PlayerStateHandler> _log = log;

	protected override void Handle(ulong sender, PlayerStateMsg msg, IEntitySessionHandlerContext ctx)
	{
		var entities = ctx.Entities;
		if (ctx.Session.Role != SessionRole.Guest)
		{
			return;
		}

		// Unreliable stream: drop stale snapshots (reordered or duplicate).
		// The broadcast stream has a single source (the host).
		if (msg.Seq <= entities.LastStateSeq)
		{
			return;
		}

		entities.LastStateSeq = msg.Seq;

		foreach (var entity in msg.Entities)
		{
			var id = entity.Id.ToNetworkEntityId();
			var target = id == entities.LocalPlayer.EntityId ? entities.LocalPlayer
				: entities.Members.FirstOrDefault(m => m.Entity.EntityId == id)?.Entity;
			if (target is null)
			{
				_log.LogWarning("Dropping entity state {Id} from {Sender}: no member with that entity id.",
					id, sender);
				continue;
			}

			entities.ApplyEntityState(entity, target);
		}

		entities.FireStateReceived(entities.LocalPlayer);
	}
}
