namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The wear chain's ONE admission rule, answered by the item's own data through
/// <see cref="IWearSemantics"/>: the game calls this item a wearable (and its wear
/// limb resolves), which is the flag the game's own wear flow dispatches on. The
/// deleted <c>RemoteWearCatalog</c> answered the same question from a 40-row id table
/// transcribed out of <c>Item.SetupItems()</c>, so a wearable the table never carried
/// — including every mod item — could be carried, dropped and saved but not put on
/// another player.
/// <para>
/// The rule declines every family that reaches native <c>Body.UseItem</c> instead,
/// and each of those is refused by name before it runs. A wearable is therefore
/// measured by nothing on the operator's side: it has no dose and no native call of
/// its own to capture, and the one thing a wear gesture must not do is run some
/// OTHER native action first.
/// </para>
/// </summary>
internal static class WearAdmission
{
	/// <summary>True when the game's own item data makes this item a wearable — asked at every routing site, so the host and the operator answer it identically.</summary>
	internal static bool IsWearable(IWearSemantics semantics, string itemId) =>
		semantics.TryGetWearPlacement(itemId, out _, out _);
}
