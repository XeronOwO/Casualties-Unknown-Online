using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>Host → guest: world-start parameters (RNG state + world-defining fields) — store for the adapter.</summary>
[PacketHandler(NetMsg.WorldStartParams)]
public sealed class WorldParamsHandler(SessionService session, ILogger<WorldParamsHandler> log)
	: PacketHandlerBase<WorldStartParamsMsg>(session)
{
	private readonly ILogger<WorldParamsHandler> _log = log;

	protected override void Handle(ulong sender, WorldStartParamsMsg msg)
	{
		Session.WorldParams = msg.ToWorldStartParams();
		_log.LogInformation("Received world params ({StateBytes} bytes, loaded run: {LoadedRun}).",
			Session.WorldParams.RandomState.Length, Session.WorldParams.LoadedRun);
	}
}
