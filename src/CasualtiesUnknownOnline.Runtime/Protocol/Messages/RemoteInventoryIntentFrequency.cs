namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Trigger frequency of the native intent vocabulary. One kind is produced by a
/// CONTINUOUS native gesture: the while-dragging liquid drain tick calls
/// <c>WaterContainerItem.Drain</c> every frame, so a happy path that logged a line
/// per frame would bury every other line in the log — the logging rule is "choose
/// the level by trigger frequency", and this is where that classification lives so
/// the viewer's dispatcher, the host half and the owner's applier cannot disagree
/// about it. A refusal of a continuous kind keeps its Information/Warning line,
/// and every discrete kind keeps the Information line that makes one gesture
/// traceable end to end. The second axis — which bracket kind can produce a member
/// — lives beside it for the same reason: the window's fail-closed rule has to tell
/// a call that frame cannot make apart from the frame's own captures.
/// </summary>
internal static class RemoteInventoryIntentFrequency
{
	extension(RemoteInventoryIntentKind kind)
	{
		/// <summary>True for the kinds a per-frame native gesture produces — their happy path logs below Information.</summary>
		public bool IsContinuousGesture() => kind == RemoteInventoryIntentKind.Drain;

		/// <summary>
		/// True for the kinds the while-dragging frame produces: the continuous drain
		/// tick, which the native code runs inside <c>HandleWhileDragging</c> and
		/// nowhere else. Every other member comes from a release branch, so this is also
		/// what the release-only filter keeps. It reads the same today as
		/// <see cref="IsContinuousGesture"/> and stays separate on purpose: the two
		/// answer different questions (a per-frame gesture vs a frame-produced kind), and
		/// the drain is both by accident of the vocabulary — if a discrete frame kind
		/// ever arrives, only these two lists disagree. The frame's other mutation, the
		/// <c>favourited</c> store on the hovered item, is not an intent at all: the mark
		/// is the local player's own (user ruling 2026-10-11).
		/// </summary>
		public bool IsWhileDraggingGesture() => kind == RemoteInventoryIntentKind.Drain;
	}
}
