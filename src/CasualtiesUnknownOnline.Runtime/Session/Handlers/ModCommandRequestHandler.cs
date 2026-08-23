using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// Guest → host command request (NetMsg.ModCommandRequest — Phase 4b). The
/// handler is a thin adapter: it routes the frame to the mod domain, which
/// validates the request and executes the command on the host's copy of the
/// mod. Direction is locked one-way in PacketReceiver.
/// </summary>
[PacketHandler(NetMsg.ModCommandRequest, NetMessageDirection.GuestToHost)]
public sealed class ModCommandRequestHandler(ILogger<ModCommandRequestHandler> log) : PacketHandlerBase<ModCommandRequestMsg, IModHandlerContext>
{
	private readonly ILogger<ModCommandRequestHandler> _log = log;

	protected override void Handle(ulong sender, ModCommandRequestMsg msg, IModHandlerContext ctx)
	{
		ctx.Mods.FireModCommandRequestReceived(sender, msg);
		_log.LogInformation("[Mods] {Sender} requests {ModId}/{Name} (request {RequestId}).",
			sender, msg.ModId, msg.Name, msg.RequestId);
	}
}
