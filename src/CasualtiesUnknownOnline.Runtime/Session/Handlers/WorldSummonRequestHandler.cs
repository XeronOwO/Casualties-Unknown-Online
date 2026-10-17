using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// Guest → host: a member typed <c>/summon</c>. The host judges its own cheat switch and roster, refuses a request
/// whose layer stamp is stale, creates the object through the game's own call — a loose item entity through the
/// item table's own fact path (the give ladder's drop fact), an entity or an animal through <c>Utils.Create</c> —
/// and answers with one sentence: the answer the member's console prints. The created object itself reports
/// through its family's own path.
/// </summary>
[PacketHandler(NetMsg.WorldSummonRequest, NetMessageDirection.GuestToHost)]
internal sealed class WorldSummonRequestHandler : PacketHandlerBase<WorldSummonRequestMsg, IWorldSummonHandlerContext>
{
	protected override void Handle(ulong sender, WorldSummonRequestMsg msg, IWorldSummonHandlerContext ctx) =>
		ctx.WorldSummon.HandleSummonRequest(sender, msg);
}
