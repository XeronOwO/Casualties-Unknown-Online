using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>Guest → host: the guest's locally simulated state (host renders it, no host-side simulation).</summary>
[PacketHandler(NetMsg.PlayerStateReport)]
public sealed class PlayerStateReportHandler(SessionService session, EntitySyncService entities, ILogger<PlayerStateReportHandler> log)
	: PacketHandlerBase<PlayerStateReportMsg>(session)
{
	private readonly EntitySyncService _entities = entities;
	private readonly ILogger<PlayerStateReportHandler> _log = log;

	protected override void Handle(ulong sender, PlayerStateReportMsg msg)
	{
		if (Session.Role != SessionRole.Host || !_entities.TryGetSynced(sender, out var member))
		{
			return;
		}

		// Unreliable stream: drop stale snapshots (reordered or duplicate).
		// Each member has its own sequence space — the counter lives on the member.
		if (msg.Seq <= member.LastReportSeq)
		{
			return;
		}

		member.LastReportSeq = msg.Seq;

		// Ownership check: the report must carry the member's own entity id —
		// an id we allocated to the member (or stale) means a misbehaving peer.
		var reportedId = msg.Entity.Id.ToNetworkEntityId();
		if (reportedId != member.Entity.EntityId)
		{
			_log.LogWarning("Dropping report from {Sender}: entity {Id} is not the member's {Expected}.",
				sender, reportedId, member.Entity.EntityId);
			return;
		}

		EntitySyncService.ApplyEntityState(msg.Entity, member.Entity);
		_entities.FireStateReceived(member.Entity);
	}
}
