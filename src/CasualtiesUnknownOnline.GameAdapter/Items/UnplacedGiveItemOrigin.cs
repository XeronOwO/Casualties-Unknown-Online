using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// Marker on an item object whose destroy is not the item's end: the give's placement found nowhere to put it on
/// this client, so the placement outcome reports "nothing placed" and the host returns the row to the giver. The
/// row keeps its instance id, and the object this client materialized is discarded because the alternative — an
/// object nothing on the body accepts — would be re-reported by the character snapshot as if it were carried.
///
/// <para>
/// Unity's <c>Object.Destroy</c> is deferred to the end of the frame, so <c>Item.OnDestroy</c> fires after the
/// outcome report and would tell the host the item ENDED while the host is returning that same id to the giver
/// (a terminal row and a resurrected copy of one id). <c>ItemPatches.ItemOnDestroyPatch</c> reads this marker and
/// stays silent, exactly as it does for the craft claims and <see cref="LimbAppliedItemOrigin"/>.
/// </para>
/// </summary>
internal sealed class UnplacedGiveItemOrigin : MonoBehaviour
{
}
