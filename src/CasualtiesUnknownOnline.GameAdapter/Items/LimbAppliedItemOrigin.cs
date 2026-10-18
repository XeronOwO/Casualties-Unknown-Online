using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// Marker on an item object whose destroy is not the item's end: the item became a limb
/// component and keeps its instance id. Two seams attach it, because the same transform has
/// two sides:
/// <list type="bullet">
/// <item>the local body's own limb action (<c>PlayerCamera.ApplyWoundItem</c>, reported by
/// <see cref="LimbItemTransform"/>) — that item's own <c>useLimbAction</c> delegate destroys the
/// object it was handed (<c>Item.cs</c>:405-406 for the tourniquet, :1489/:1515 for the two
/// splints) and adds the component;</item>
/// <item>the OWNER's client when a cross-player apply ran on somebody else's limb: the owner's
/// carried object goes away and <c>PlayerInteractionApply.OnPlayerItemUseReceived</c> destroys
/// it after the result said the item became the limb component.</item>
/// </list>
/// Unity's <c>Object.Destroy</c> is deferred to the end of the frame — so <c>Item.OnDestroy</c>
/// fires later, outside the call and outside every scope that might have silenced it, for an
/// item that did NOT end. <c>ItemPatches.ItemOnDestroyPatch</c> reads this marker and stays
/// silent, exactly as it does for the craft and heater-cook claims.
/// </summary>
internal sealed class LimbAppliedItemOrigin : MonoBehaviour
{
}
