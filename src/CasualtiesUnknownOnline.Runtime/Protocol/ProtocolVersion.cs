namespace CasualtiesUnknownOnline.Runtime.Protocol;

public static class ProtocolVersion
{
	/// <summary>Bumped on any breaking wire change.</summary>
	public const int Current = 36; // v36: Chat (NetMsg 109) — a v35 peer has no text-chat relay; the message is dropped by direction/unknown handler rules

}
