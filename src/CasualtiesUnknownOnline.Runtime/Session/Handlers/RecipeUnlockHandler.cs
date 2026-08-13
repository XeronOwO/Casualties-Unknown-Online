using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// A blueprint was used: the recipe at RecipeIndex is unlocked on every side
/// (the static recipe table is per-process — the unlock would otherwise exist
/// only on the user's side). Guest → host report; host → guest broadcast relay
/// (source excluded). Every side raises the apply event (the adapter sets
/// Recipes.recipes[idx].INT = 0).
/// </summary>
[PacketHandler(NetMsg.RecipeUnlock)]
public sealed class RecipeUnlockHandler(ILogger<RecipeUnlockHandler> log) : PacketHandlerBase<RecipeUnlockMsg>
{
	private readonly ILogger<RecipeUnlockHandler> _log = log;

	protected override void Handle(ulong sender, RecipeUnlockMsg msg, HandlerContext ctx)
	{
		ctx.Craft.FireRecipeUnlockReceived(sender, msg.RecipeIndex);
		_log.LogInformation("Recipe {Index} unlocked by {Sender}.", msg.RecipeIndex, sender);
	}
}
