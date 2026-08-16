namespace CasualtiesUnknownOnline.Runtime.Protocol;

public static class ProtocolVersion
{
	/// <summary>Bumped on any breaking wire change.</summary>
	public const int Current = 10; // v10: mod permissions in the handshake + mod host commands (NetMsg 86/87) + semver-validated mod versions — a v9 peer would drop the permission flags and command messages, so mixed-version sessions are refused instead of silently degrading
}
