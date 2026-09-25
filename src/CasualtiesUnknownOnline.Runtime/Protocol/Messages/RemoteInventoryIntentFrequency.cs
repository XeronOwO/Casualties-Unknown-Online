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
/// traceable end to end.
/// </summary>
internal static class RemoteInventoryIntentFrequency
{
	extension(RemoteInventoryIntentKind kind)
	{
		/// <summary>True for the kinds a per-frame native gesture produces — their happy path logs below Information.</summary>
		public bool IsContinuousGesture() => kind == RemoteInventoryIntentKind.Drain;
	}
}
