using System;
using System.Collections;
using CasualtiesUnknownOnline.GameAdapter.Items;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// Runs the cross-player LIMB TOOL on the AFFECTED side's client — the last chain
/// of <c>mod-cross-player-native-semantics</c>'s Part B. The treated player's own
/// limb is the object the item's own <c>useLimbAction</c> writes and its own local
/// incarnation of the offered item is the object that delegate is handed, so the
/// whole effect is the game's own code on the body it lands on: the limb field
/// writes, the limb component the tool turns into (<c>SplintLimb</c>,
/// <c>TourniquetScript</c>, <c>ChilledLimb</c>), the timed op a delegate starts
/// (<c>medicalsuture</c>'s bleed ramp through <c>CoUtils.DoTimedOp</c>, keyed by the
/// limb's own name), every clip it plays and the item condition it spends.
/// <para>
/// Nothing is measured and nothing is computed anywhere else, which is what the
/// deleted <c>RemoteLimbToolCatalog</c> used to hold: ten ids with transcribed
/// condition costs, body deltas, component fields and a timed ramp, all of them
/// literals inside these delegates.
/// </para>
/// <para>
/// The call runs inside the medical capture scope, exactly as the topical family's
/// patient-side application does, so the clip a delegate plays at the treated limb
/// is classified and relayed instead of staying this client's alone. It runs
/// OUTSIDE the <c>RemoteApply</c> scope for the sibling families' reason: that
/// scope is the "this is a replay of a peer's fact" marker, and this is a real
/// effect on this body.
/// </para>
/// <para>
/// One deliberate deviation from the native dispatch: <c>PlayerCamera.ApplyWoundItem</c>
/// gates the call on the OPERATOR's own <c>body.conscious</c> and its depression
/// cutoff, which are facts about the player who acts — in a single-player game that
/// is also the player who is treated, so neither is a "may this limb be treated"
/// rule here; the treating side's own gesture gates are the operator's, and the
/// treated side is often unconscious by design.
/// </para>
/// </summary>
internal static class NativeLimbToolApply
{
	/// <summary>
	/// Run the item's own limb action on <paramref name="requestedLimbIndex"/> of the
	/// local body and report what the run left of the item; returns whether the
	/// game's own action ran. Every refusal is logged by name, because a silent no-op
	/// after a host-admitted use is the one outcome nobody could diagnose.
	/// </summary>
	internal static bool Apply(Body body, int requestedLimbIndex, ulong itemInstanceId, GameAdapterDomains domains)
	{
		if (itemInstanceId == 0)
		{
			return false;
		}

		if (!domains.StandingMaterializer.TryGetStandingItem(itemInstanceId, out var item))
		{
			domains.Log.LogWarning("[ItemUse] the limb tool on item {ItemId} cannot run: no standing object stands for that id on this client.", itemInstanceId);
			return false;
		}

		// The category, not only the marker: the item's owner may have dropped it while
		// the request was in flight, and treating this body with an item that just left
		// the inventory would spend a resource its owner no longer offers.
		if (!StandingItems.Is(item))
		{
			domains.Log.LogWarning("[ItemUse] the limb tool on item {ItemId} cannot run: its data row is no longer a carried row on this client.", itemInstanceId);
			return false;
		}

		if (!Item.GlobalItems.TryGetValue(item.id, out var info) || info is null || !info.usableOnLimb || info.useLimbAction is null)
		{
			domains.Log.LogWarning("[ItemUse] the limb tool on {Type} (id {ItemId}) cannot run: the game's own data carries no limb action for it.", item.id, itemInstanceId);
			return false;
		}

		// The limb the request NAMED and nothing else: this family's whole meaning is
		// which limb the operator picked (decision 246), so a limb this body can no
		// longer serve — dismembered since the pick, or an index past its layout — is a
		// REFUSAL, never a silent landing on the most injured one. The automatic rule
		// stays with the injection and topical chains, where a -1 limb is a legal
		// auto-select (NativeLimbTarget's class doc).
		var limb = NativeLimbTarget.ResolveNamed(body, requestedLimbIndex);
		if (limb is null)
		{
			domains.Log.LogWarning(
				"[ItemUse] the limb tool on {Type} (id {ItemId}) cannot run: limb {Limb} of the request is not a usable limb of this body (gone, dismembered, or past its layout) — refused rather than applied to another limb.",
				item.id, itemInstanceId, requestedLimbIndex);
			return false;
		}

		var limbIndex = Array.IndexOf(body.limbs, limb);
		var definitionId = item.id;
		using (CallContext.Enter(CallContext.Origin.CharacterMedicalUse))
		{
			info.useLimbAction(limb, item);
		}

		// The condition the run left is read NOW, because the destroy a delegate may have
		// made lands later: Unity's Object.Destroy is deferred to the end of the frame, so
		// the object is still non-null on this line while the field the delegate wrote is
		// already its final value.
		var condition = item.condition;

		// ...and the consumption is decided one frame later, when that destroy HAS
		// landed: the component-bearing tools turn the item into the limb component and
		// destroy the object they were handed (Item.cs:405-406 for the tourniquet,
		// 1489/1515 for the two splints), which is a fact about the object's fate rather
		// than something the item's own data or the field it left could say. Reporting
		// inside this frame would report every one of them as surviving.
		body.StartCoroutine(ReportAfterTheDestroyLands(item, itemInstanceId, definitionId, limbIndex, condition, domains));
		return true;
	}

	/// <summary>
	/// One frame later: the delegate's own destroy has landed, so Unity's overloaded
	/// <c>== null</c> answers whether the run consumed the object. A consumed object
	/// reports condition 0 because nothing readable is left of it — the limb component
	/// that took it carries the value it held instead.
	/// </summary>
	private static IEnumerator ReportAfterTheDestroyLands(
		Item item,
		ulong itemInstanceId,
		string definitionId,
		int limbIndex,
		float condition,
		GameAdapterDomains domains)
	{
		yield return null;

		if (item == null) // Unity object — ==
		{
			domains.PlayerInteraction.SendItemActionOutcome(itemInstanceId, 0f, consumed: true);
			domains.Log.LogInformation(
				"[ItemUse] ran the game's own limb action of {Type} (id {ItemId}) on the local limb {Limb}; the item was consumed into the limb.",
				definitionId, itemInstanceId, limbIndex);
			yield break;
		}

		domains.PlayerInteraction.SendItemActionOutcome(itemInstanceId, condition, consumed: false);
		domains.Log.LogInformation(
			"[ItemUse] ran the game's own limb action of {Type} (id {ItemId}) on the local limb {Limb}; item condition left at {Condition:F2}.",
			definitionId, itemInstanceId, limbIndex, condition);
	}
}
