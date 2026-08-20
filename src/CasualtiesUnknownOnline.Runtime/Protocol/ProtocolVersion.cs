namespace CasualtiesUnknownOnline.Runtime.Protocol;

public static class ProtocolVersion
{
	/// <summary>Bumped on any breaking wire change.</summary>
	public const int Current = 25; // v25: FluidPresentationMsg (NetMsg 96) — a v24 peer never receives the host's transient water-push/waterflow events and keeps the guest-side fluid sound/push/slip gap; v24 was crystalenemy presentation tint
}
