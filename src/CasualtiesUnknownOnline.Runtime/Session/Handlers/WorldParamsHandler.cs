using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>Host → guest: world-start parameters (RNG state + world-defining fields) — store for the adapter.</summary>
[PacketHandler(NetMsg.WorldStartParams, NetMessageDirection.HostToGuest)]
public sealed class WorldParamsHandler(ILogger<WorldParamsHandler> log) : PacketHandlerBase<WorldStartParamsMsg, IWorldHandlerContext>
{
	private readonly ILogger<WorldParamsHandler> _log = log;

	protected override void Handle(ulong sender, WorldStartParamsMsg msg, IWorldHandlerContext ctx)
	{
		var worldParams = msg.ToWorldStartParams();
		ctx.World.WorldParams = worldParams;
		_log.LogInformation("Received world params ({StateBytes} bytes, loaded run: {LoadedRun}).",
			worldParams.RandomState.Length, worldParams.LoadedRun);
	}
}
