using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The host's half of one cross-player TOPICAL application, split out of
/// <see cref="PlayerItemUseService"/> at the architecture line gate so the branch reads
/// as one decision instead of three statements. Two facts decide it: the request must
/// name the limb the WOUND VIEW carried — the application's native call site is
/// <c>PlayerCamera.ApplyWoundItem</c>, which runs the item's own
/// <c>useLimbAction(this.selectedLimb, item)</c>, so the limb IS that gesture
/// (decision 246) — and the dose the operator measured must exist on the item it
/// offers.
/// <para>
/// It decides and plans only. The drain is applied to the item's own clone here because
/// that clone is what the caller commits; the EFFECT belongs to the affected side,
/// which runs each liquid's own <c>onHealthUse</c> through the native path.
/// </para>
/// </summary>
internal static class TopicalUseBranch
{
	/// <summary>
	/// True with <paramref name="dose"/> set when the request may commit. Every refusal
	/// is logged by name and the caller commits nothing.
	/// </summary>
	internal static bool TryPlan(
		int limbIndex,
		ulong target,
		CharacterItemMsg originalItem,
		float doseMl,
		CharacterItemMsg newItem,
		ILogger log,
		out List<LiquidStackMsg>? dose)
	{
		dose = null;

		if (limbIndex < 0)
		{
			log.LogWarning(
				"[ItemUse] refused: topical {ItemId} (id {InstanceId}) from {Target} named no limb — it is applied through the wound view, which carries the limb the operator picked.",
				originalItem.ItemId, originalItem.InstanceId, target);
			return false;
		}

		// The per-use ml is an ldc.r4 literal inside the item's own delegate closure, so
		// no table can hold it: the operator measured it, and the host only caps it at
		// what the item really carries (LiquidDrainPlan mirrors the native
		// CalculateDrain).
		if (!LiquidDrainPlan.TryCreate(originalItem.Liquids, doseMl, out var plan))
		{
			log.LogWarning(
				"[ItemUse] refused: {ItemId} (id {InstanceId}) carried no dose to apply to {Target}.",
				originalItem.ItemId, originalItem.InstanceId, target);
			return false;
		}

		CarriedItemUseTree.ApplyDrain(newItem, plan);
		dose = plan;
		return true;
	}
}
