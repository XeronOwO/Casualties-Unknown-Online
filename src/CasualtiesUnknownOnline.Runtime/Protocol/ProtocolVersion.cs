namespace CasualtiesUnknownOnline.Runtime.Protocol;

public static class ProtocolVersion
{
	/// <summary>Wire compatibility version for the handshake. No released
	/// compatibility surface exists yet, so each behavioral wire extension bumps
	/// this and mixed-version sessions are rejected by the handshake.</summary>
	public const int Current = 7;

}
