namespace CasualtiesUnknownOnline.Runtime.Protocol;

public static class ProtocolVersion
{
	/// <summary>Bumped on any breaking wire change.</summary>
	public const int Current = 3; // v3: HandshakeMsg.Mods — the mod-list consistency check (Phase 4 Mod API). Field-level wire-compatible (protobuf skips unknown fields) but BEHAVIORALLY breaking: the host now rejects inconsistent mod lists, so mixed-version sessions are refused by the version gate instead of silently skipping the check
}
