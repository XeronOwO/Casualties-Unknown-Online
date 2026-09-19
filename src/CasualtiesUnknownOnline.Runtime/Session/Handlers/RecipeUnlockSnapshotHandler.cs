using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// An absolute recipe-unlock SET arrived. Guest → host: this guest's own
/// unlocked set — the fallback for a one-shot report/relay the session
/// swallowed — which the host merges (it relays exactly the indices it had not
/// learned, through the ordinary unlock path). Host → guest: the host's
/// authoritative set (world entry / the 60 s repair), which the guest applies
/// silently and idempotently. Same payload both ways: the unlock fact is the
/// set, and an unlock is monotonic on every side.
/// </summary>
[PacketHandler(NetMsg.RecipeUnlockSnapshot, NetMessageDirection.Bidirectional)]
public sealed class RecipeUnlockSnapshotHandler(ILogger<RecipeUnlockSnapshotHandler> log) : PacketHandlerBase<RecipeUnlockSnapshotMsg, ICraftHandlerContext>
{
	private readonly ILogger<RecipeUnlockSnapshotHandler> _log = log;

	protected override void Handle(ulong sender, RecipeUnlockSnapshotMsg msg, ICraftHandlerContext ctx)
	{
		ctx.Craft.FireRecipeUnlockSnapshotReceived(sender, msg.RecipeIndexes);
		_log.LogInformation("Recipe-unlock set of {Count} recipe(s) arrived from {Sender}.", msg.RecipeIndexes.Count, sender);
	}
}
