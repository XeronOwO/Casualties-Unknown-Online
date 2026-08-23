namespace CasualtiesUnknownOnline.Runtime.Protocol;

public static class ProtocolVersion
{
	/// <summary>Bumped on any breaking wire change.</summary>
	public const int Current = 33; // v33: RadiationLineState (NetMsg 106) carries the host-authoritative radiation-line active/timeGone world state; v32 peers would run an independent local line and diverge the radiation boundary

}
