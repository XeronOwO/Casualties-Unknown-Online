namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The operand ORDER of the item-interaction replays, named once and covered without a
/// scene. Two native calls take an item pair whose first argument is the receiver, and
/// getting that order wrong is invisible to every scene-bound path — the owner would
/// charge the wrong item and nothing would throw — so the decision lives here instead of
/// inline in the Game Adapter's switch: <c>Body.CombineItems(target, item)</c> takes the
/// HIT item first (<c>PlayerCamera.cs:1602</c>; the condition merge credits <c>it1</c>,
/// <c>Body.cs:1280-1282</c>, and the gun takes the magazine from <c>it2</c>), and
/// <c>BatteryItem.LoadBattery(battery)</c> runs on the RECEIVING item's own component
/// while the battery is its argument (<c>PlayerCamera.cs:1550</c>).
/// </summary>
internal static class RemoteItemInteractionOperands
{
	/// <summary>The (receiver, consumed) pair of <c>Body.CombineItems</c>: the hit item is the receiver, the dragged one is consumed.</summary>
	internal static (ulong Receiver, ulong Consumed) CombineTargets(ulong draggedItemId, ulong targetItemId) =>
		(targetItemId, draggedItemId);

	/// <summary>The (receiving item, battery) pair of <c>BatteryItem.LoadBattery</c>: the hit item receives, the dragged battery is consumed.</summary>
	internal static (ulong Receiver, ulong Battery) BatteryLoadTargets(ulong draggedItemId, ulong targetItemId) =>
		(targetItemId, draggedItemId);
}
