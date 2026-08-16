using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// A world entity was created at runtime (outside generation — the spawn
/// command): the handler only surfaces the message. The adapter's
/// EntitySpawnSync creates the host copy, may enrich the relay (keypad code)
/// and is the single broadcast owner — a handler-level broadcast here would
/// send a second, un-enriched copy.
/// </summary>
[PacketHandler(NetMsg.EntitySpawned)]
public sealed class EntitySpawnedHandler(ILogger<EntitySpawnedHandler> log)
	: PacketHandlerBase<EntitySpawnedMsg>
{
	private readonly ILogger<EntitySpawnedHandler> _log = log;

	protected override void Handle(ulong sender, EntitySpawnedMsg msg, HandlerContext ctx)
	{
		ctx.World.FireEntitySpawnedReceived(sender, msg);

		_log.LogInformation("[EntitySpawn] {Id} at {Pos} from {Sender}.", msg.Id, msg.Position, sender);
	}
}
