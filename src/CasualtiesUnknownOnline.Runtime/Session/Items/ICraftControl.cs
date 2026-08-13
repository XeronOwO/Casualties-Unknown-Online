using System;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// The crafting-domain surface packet handlers operate on — implemented by
/// CraftSyncService (the seventh control surface, the IModsControl precedent).
/// Handlers depend on this narrow interface instead of the concrete service,
/// which keeps the constructor graph acyclic (abstract extraction, user rule).
/// The craft apply cannot live in ItemService — that file sits at the
/// 600-line architecture gate — so the domain has its own service composing
/// ItemService's crafting seams.
/// </summary>
public interface ICraftControl
{
	// ===== Report side (the adapter's local compute reports here) =====

	/// <summary>One crafting operation completed locally (Recipe.TryMake / CombineItems / LiquidTransfer.Finish) — the complete terminal state: guest → host report; host → local apply + broadcast relay.</summary>
	void ReportCraft(CraftReportMsg msg);

	/// <summary>A blueprint was used locally — the recipe at RecipeIndex is unlocked (Recipes.recipes[idx].INT = 0): guest → host report; host → local apply + broadcast relay.</summary>
	void SendRecipeUnlock(int recipeIndex);

	// ===== Receive side (packet handlers surface the wire here) =====

	/// <summary>A craft report arrived: the host classifies per entry against its tables, applies, stamps the relay routing and relays (source excluded); a guest applies the relay positionally (scene removals + stamped corrections + product fact-table updates).</summary>
	void FireCraftReportReceived(ulong sender, CraftReportMsg msg);

	/// <summary>A recipe-unlock report arrived: every side raises the apply event (the adapter sets the static INT); the host additionally relays (source excluded).</summary>
	void FireRecipeUnlockReceived(ulong sender, int recipeIndex);

	// ===== Application event (the adapter applies these) =====

	/// <summary>A recipe was unlocked (every side — the local side's own use and the relayed reports alike) — the adapter sets Recipes.recipes[idx].INT = 0.</summary>
	event Action<int>? RecipeUnlockReceived;
}
