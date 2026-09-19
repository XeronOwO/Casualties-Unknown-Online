using System;
using System.Collections.Generic;
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

	/// <summary>
	/// Guest only: report this guest's WHOLE unlocked recipe set to the host —
	/// the swallowed-report fallback for <see cref="SendRecipeUnlock"/>. The set
	/// is absolute and monotonic (the blueprint is spent; nothing writes an
	/// unlock back), so the host merges the difference and relays exactly the
	/// indices it did not already hold. Sent by the shared fallback cadence while
	/// this side still has an unlock the host's set does not carry; a set that
	/// cannot be read is not sent, and an empty one asks for no write.
	/// </summary>
	void SendRecipeUnlockSetToHost();

	/// <summary>
	/// Host only: send this host's authoritative unlocked recipe set to one
	/// member. It rides the world-entry group and the 60 s in-session repair, so
	/// a member that joined after an unlock — or whose entry send was swallowed —
	/// converges without a reconnect.
	/// </summary>
	void SendRecipeUnlockSnapshot(ulong targetSteamId);

	/// <summary>The fallback's clock: re-report this side's unlocked set when the shared pending window elapses (driven by WorldReportFallbackPump).</summary>
	void PumpRecipeUnlockFallback(long nowMs);

	// ===== Receive side (packet handlers surface the wire here) =====

	/// <summary>A craft report arrived: the host classifies per entry against its tables, applies, stamps the relay routing and relays (source excluded); a guest applies the relay positionally (scene removals + stamped corrections + product fact-table updates).</summary>
	void FireCraftReportReceived(ulong sender, CraftReportMsg msg);

	/// <summary>A recipe-unlock report arrived: every side raises the apply event (the adapter sets the static INT); the host additionally relays (source excluded).</summary>
	void FireRecipeUnlockReceived(ulong sender, int recipeIndex);

	/// <summary>
	/// An ABSOLUTE unlock set arrived. Host: merge the sender's set — every index
	/// this host does not already hold goes through the ordinary unlock path
	/// (apply + relay, source excluded), which keeps the host the authority over
	/// the set and keeps one apply path. Guest: apply the host's set silently
	/// (the receiver performed no unlock, and one alert per recipe would spam a
	/// late joiner) and read it as the answer that ends this side's re-report.
	/// </summary>
	void FireRecipeUnlockSnapshotReceived(ulong sender, IReadOnlyList<int> recipeIndexes);

	// ===== Application event (the adapter applies these) =====

	/// <summary>A recipe was unlocked (every side — the local side's own use and the relayed reports alike) — the adapter sets Recipes.recipes[idx].INT = 0.</summary>
	event Action<int>? RecipeUnlockReceived;

	/// <summary>An absolute unlock set arrived (guest side only): the adapter writes INT = 0 for every index WITHOUT the per-recipe alert — a backfill is not a learn the receiver performed.</summary>
	event Action<IReadOnlyList<int>>? RecipeUnlockSetReceived;
}
