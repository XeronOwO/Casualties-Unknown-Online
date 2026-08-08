using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>Host → guest: world-start parameters (RNG state + world-defining fields) — store for the adapter.</summary>
[PacketHandler(NetMsg.WorldStartParams)]
public sealed class WorldParamsHandler(ILogger<WorldParamsHandler> log) : PacketHandlerBase<WorldStartParamsMsg>
{
	private readonly ILogger<WorldParamsHandler> _log = log;

	protected override void Handle(ulong sender, WorldStartParamsMsg msg, HandlerContext ctx)
	{
		ctx.Session.WorldParams = msg.ToWorldStartParams();
		_log.LogInformation("Received world params ({StateBytes} bytes, loaded run: {LoadedRun}).",
			ctx.Session.WorldParams.RandomState.Length, ctx.Session.WorldParams.LoadedRun);
	}
}
