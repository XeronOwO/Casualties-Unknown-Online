namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The native release outcomes that are deliberately nothing, named so the
/// unclassified-gesture signal stays meaningful: a release that produced no
/// intent is only an unknown gesture when it is none of these.
/// </summary>
internal enum RemoteDragNoOp
{
	/// <summary>Not a classified no-op — a release that produced no intent is an unclassified native gesture.</summary>
	None = 0,

	/// <summary>R1: the proxy was released back onto its own inventory button (consumed, no mutation).</summary>
	ReturnedToOwnSlot = 1,

	/// <summary>R7: a wearable that cannot be held — the native branch answers with its own alert and consumes the release.</summary>
	WearableCannotBeHeld = 2,

	/// <summary>R14 and the container-window open of the design's §2.3: local UI on the viewer, never an intent.</summary>
	LocalUiOnly = 3,
}
