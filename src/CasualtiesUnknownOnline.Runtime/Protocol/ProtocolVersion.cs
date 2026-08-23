namespace CasualtiesUnknownOnline.Runtime.Protocol;

public static class ProtocolVersion
{
	/// <summary>Bumped on any breaking wire change.</summary>
	public const int Current = 39; // v39: Kicked tells a guest it was removed by the host — a v38 peer has no dedicated teardown signal

}
