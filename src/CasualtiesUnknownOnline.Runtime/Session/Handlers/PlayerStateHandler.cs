using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>Host → guest: the authoritative entity batch (unreliable stream, seq gate).</summary>
[PacketHandler(NetMsg.PlayerState)]
public sealed class PlayerStateHandler(SessionService session, ILogger<PlayerStateHandler> log)
	: PacketHandlerBase<PlayerStateMsg>(session)
{
	private readonly ILogger<PlayerStateHandler> _log = log;

	protected override void Handle(ulong sender, PlayerStateMsg msg)
	{
		if (Session.Role != SessionRole.Guest)
		{
			return;
		}

		// Unreliable stream: drop stale snapshots (reordered or duplicate).
		// The broadcast stream has a single source (the host).
		if (msg.Seq <= Session.LastStateSeq)
		{
			return;
		}

		Session.LastStateSeq = msg.Seq;

		foreach (var entity in msg.Entities)
		{
			var id = entity.Id.ToNetworkEntityId();
			var target = id == Session.LocalPlayer.EntityId ? Session.LocalPlayer
				: Session.Members.FirstOrDefault(m => m.Entity.EntityId == id)?.Entity;
			if (target is null)
			{
				_log.LogWarning("Dropping entity state {Id} from {Sender}: no member with that entity id.",
					id, sender);
				continue;
			}

			SessionService.ApplyEntityState(entity, target);
		}

		Session.FireStateReceived(Session.LocalPlayer);
	}
}
