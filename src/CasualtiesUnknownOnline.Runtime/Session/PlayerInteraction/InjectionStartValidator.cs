using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The OPERATOR half of an injection start: the item the operator named exists on
/// the host's item authority and is an injectable medicine with a plan. Split out
/// of <see cref="MedicalOperationSessionService"/> so the injection family reads
/// like the shrapnel and Stage 3 families (one pure validator per family) and so
/// the service that holds the session state keeps room under the architecture line
/// gate. The remaining volume/claim checks stay with the service: they read the
/// claimed item's live amounts and the shared claim book, which is session state
/// rather than a pure function of the snapshot.
/// </summary>
internal static class InjectionStartValidator
{
	/// <param name="itemIndex">The index of the validated item inside <paramref name="operatorData"/>, so the caller builds the session from the item this validation accepted rather than searching again.</param>
	internal static bool TryValidateOperator(
		CharacterDataMsg operatorData,
		ulong itemInstanceId,
		out int itemIndex,
		out string reason)
	{
		reason = "";
		itemIndex = PlayerItemIndex.Find(operatorData, itemInstanceId);
		if (itemIndex < 0 || itemIndex >= operatorData.Items.Count)
		{
			itemIndex = -1;
			reason = "Item not found.";
			return false;
		}

		var item = operatorData.Items[itemIndex];
		if (!RemoteMedicineCatalog.IsInjectableItem(item.ItemId)
			|| !RemoteMedicineCatalog.TryCreatePlan(item.Liquids, item.ItemId, out _))
		{
			reason = "Item is not injectable.";
			return false;
		}

		return true;
	}
}
