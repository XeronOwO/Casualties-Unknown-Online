using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// Host → member: the current building-entity health records (world entry /
/// the 60 s resend). The receiver applies each entry to its own
/// deterministically-generated copy — the same semantic as the live
/// BuildingEntityDamaged relay, but for entities damaged or destroyed before
/// the member joined.
/// </summary>
[PacketHandler(NetMsg.BuildingEntityHealthSnapshot, NetMessageDirection.HostToGuest)]
public sealed class BuildingEntityHealthSnapshotHandler(ILogger<BuildingEntityHealthSnapshotHandler> log) : PacketHandlerBase<BuildingEntityHealthSnapshotMsg>
{
	private readonly ILogger<BuildingEntityHealthSnapshotHandler> _log = log;

	protected override void Handle(ulong sender, BuildingEntityHealthSnapshotMsg msg, HandlerContext ctx)
	{
		_log.LogInformation("Building-entity health snapshot received ({Count} entities).", msg.Entries.Count);
		ctx.World.FireBuildingEntityHealthSnapshotReceived(msg.Entries);
	}
}
