using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// Marker on the item object the local body's own limb action turned into a limb
/// component. The item's own <c>useLimbAction</c> delegate destroys the object it
/// was handed (<c>Item.cs</c>:405-406 for the tourniquet, :1489/:1515 for the two
/// splints) and adds the component, and Unity's <c>Object.Destroy</c> is deferred to
/// the end of the frame — so <c>Item.OnDestroy</c> fires later, outside the call,
/// for an item that did NOT end: it is installed on that limb and keeps its instance
/// id. The apply seam (<see cref="LimbItemTransform"/>) reports the relocation and
/// attaches this marker; <c>ItemPatches.ItemOnDestroyPatch</c> reads it and stays
/// silent, exactly as it does for the craft and heater-cook claims.
/// </summary>
internal sealed class LimbAppliedItemOrigin : MonoBehaviour
{
}
