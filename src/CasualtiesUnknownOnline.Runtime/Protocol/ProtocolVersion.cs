namespace CasualtiesUnknownOnline.Runtime.Protocol;

public static class ProtocolVersion
{
	/// <summary>Bumped on any breaking wire change.</summary>
	public const int Current = 16; // v16: LimbStateEvent (NetMsg 93) + the EntityStateMsg SwingSeq — a v15 peer would never learn a limb latch until the 1 Hz snapshot (and would not replay rapid mining swings), so mixed-version sessions are refused instead of silently degrading
}
