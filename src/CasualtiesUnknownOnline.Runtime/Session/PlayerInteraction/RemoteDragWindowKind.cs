namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// Which native invocation a drag bracket spans. The two kinds carry different
/// calls and different reporting rules: a release bracket can be an unclassified
/// gesture when it produced nothing, while a while-dragging frame cannot — most
/// frames of a drag carry no tick at all, and reporting each of them as an unknown
/// gesture would write sixty lines a second over a gesture that is working.
/// </summary>
internal enum RemoteDragWindowKind
{
	/// <summary>One <c>PlayerCamera.HandleReleaseDragging</c> invocation: the discrete intents.</summary>
	Release,

	/// <summary>One <c>PlayerCamera.HandleWhileDragging</c> frame: the continuous actions.</summary>
	WhileDragging,
}
