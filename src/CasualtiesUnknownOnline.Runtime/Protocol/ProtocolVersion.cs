namespace CasualtiesUnknownOnline.Runtime.Protocol;

public static class ProtocolVersion
{
	/// <summary>Bumped on any breaking wire change.</summary>
	public const int Current = 40; // v40: Banned tells a guest it was permanently rejected by the host — a v39 peer only knows Kicked

}
