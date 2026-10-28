using UnityTime = UnityEngine.Time;

namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// The one object a cross-player eat's delegate CREATED to replace the item it was handed — the peel's opened fruit
/// (<c>Item.cs:2619</c>: <c>body.PickUpItem(Instantiate(Resources.Load("stonefruitopen"), …))</c>).
///
/// <para>
/// The object is the OWNER's row becoming another definition in place: it must get no instance id, no spawn and no pickup
/// report (its definition travels back in the eat's outcome instead), and the eat destroys the object once it has read it.
/// Two hooks see it — the instantiation (<c>ItemWorldSync.OnItemInstantiated</c>) and the delegate's own
/// <c>Body.PickUpItem</c> (<c>PickupSync.OnPickedUp</c>) — so the observation is published on the scope rather than read
/// back from a slot: the replacement's slot is wherever the game's own pickup put it, which is not necessarily the slot the
/// eat arranged, and reading a slot missed it (measured 2026-10-26: the eat reported no replacement and the owner's row was
/// destroyed instead of becoming the opened fruit).
/// </para>
///
/// <para>
/// Both hooks ask <see cref="CallContext.Origin.ItemReplacementCreation"/>, so a creation outside the eat's window can never
/// reach this holder; it is cleared when the eat opens its window and consumed when it reads the result.
/// </para>
/// </summary>
internal static class ItemReplacementObservation
{
	private static Item? _created;
	private static Item? _eaten;
	private static int _armedUntilFrame = -1;

	/// <summary>
	/// Opens a fresh window for ONE eat, naming the item that eat is about to use. The name matters: the window is open while the delegate drops the item it was
	/// handed, so the first object a hook meets inside it is the EATEN item, not the replacement — and recording that one made the eat report "replaced the item
	/// with stonefruitclosed (condition 0.00)" while the opened fruit it really created got a fresh id and became a world item at the origin (measured
	/// 2026-10-29, both clients' logs).
	/// </summary>
	internal static void Arm(Item eaten)
	{
		_eaten = eaten;
		_created = null;
		_armedUntilFrame = UnityTime.frameCount + 1;
	}

	/// <summary>
	/// Closes the window once the eat has read the result, so a later creation can never be swallowed by it. The OBJECT stays known: the report that leaked
	/// (measured 2026-10-29: <c>[ItemSpawned] local stonefruitopen … reported at (0.0,0.0), fresh False</c>, a fresh id and a world spawn at the origin)
	/// arrived on a frame AFTER the eat returned, outside any window — identity is what makes it a replacement, not the frame it was created on.
	/// </summary>
	internal static void Disarm() => _armedUntilFrame = -1;

	/// <summary>True while this frame or the previous one ran an eat's delegate — what every creation and pickup hook asks first.</summary>
	internal static bool IsArmed => UnityTime.frameCount <= _armedUntilFrame;

	/// <summary>
	/// True for the exact object this window's delegate created, on whichever frame a hook meets it. The frame window alone missed a report that ran after the
	/// eat returned, so the hooks ask this as well: the replacement is never a world item, no matter when it is first seen.
	/// </summary>
	internal static bool IsTheCreatedReplacement(Item item) => _created != null && item == _created; // Unity object — ==

	/// <summary>The object the delegate created, when this client saw one — otherwise <c>null</c>.</summary>
	internal static Item? Created => _created;

	/// <summary>Records the object a hook saw created or picked up inside the eat's window (first one wins), never the eaten item itself.</summary>
	internal static void Record(Item item)
	{
		if (item == _eaten) // Unity object — ==: the delegate drops the item it was handed inside this same window
		{
			return;
		}

		if (_created == null) // Unity object — ==
		{
			_created = item;
		}
	}
}
