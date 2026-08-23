using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// The host's geyser liquid types arrived (world entry + the 60 s cycle): the
/// guest writes each onto its local GeyserScript (position-keyed; idempotent —
/// same-value SetValue). The liquid type is a generation-time initial
/// condition (GeyserScript.cs:12 rolls it from the public random stream, so
/// every side's copy may differ — the host's roll is the authority), not an
/// event payload.
/// </summary>
[PacketHandler(NetMsg.GeyserStateSnapshot, NetMessageDirection.HostToGuest)]
public sealed class GeyserStateSnapshotHandler(ILogger<GeyserStateSnapshotHandler> log)
	: PacketHandlerBase<GeyserStateSnapshotMsg>
{
	private readonly ILogger<GeyserStateSnapshotHandler> _log = log;

	protected override void Handle(ulong sender, GeyserStateSnapshotMsg msg, HandlerContext ctx)
	{
		ctx.World.FireGeyserStateReceived(msg.Geysers);
		_log.LogInformation("[GeyserSnapshot] received {Count} liquid types.", msg.Geysers.Count);
	}
}
