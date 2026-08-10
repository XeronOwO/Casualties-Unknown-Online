using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// A world entity was created at runtime (outside generation — the spawn
/// command), star semantics: the host creates its own copy at the same
/// position (which is what makes the position-keyed identity hold for runtime
/// creations too) and relays to the other members (the source excluded — it
/// already created locally). Guest: the host's relay — create the copy.
/// </summary>
[PacketHandler(NetMsg.EntitySpawned)]
public sealed class EntitySpawnedHandler(ILogger<EntitySpawnedHandler> log)
	: PacketHandlerBase<EntitySpawnedMsg>
{
	private readonly ILogger<EntitySpawnedHandler> _log = log;

	protected override void Handle(ulong sender, EntitySpawnedMsg msg, HandlerContext ctx)
	{
		ctx.World.FireEntitySpawnedReceived(sender, msg);
		if (ctx.Session.Role == SessionRole.Host)
		{
			ctx.Session.BroadcastExcept(sender, NetMsg.EntitySpawned, msg);
		}

		_log.LogInformation("[EntitySpawn] {Id} at {Pos} from {Sender}.", msg.Id, msg.Position, sender);
	}
}
