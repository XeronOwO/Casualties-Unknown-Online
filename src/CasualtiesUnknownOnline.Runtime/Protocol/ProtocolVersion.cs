namespace CasualtiesUnknownOnline.Runtime.Protocol;

public static class ProtocolVersion
{
	/// <summary>Bumped on any breaking wire change.</summary>
	public const int Current = 20; // v20: ItemContainerContent (NetMsg 95) — a v19 peer would silently miss nested container-content events (fact-table freshness until the 1 Hz snapshot, but the handshake refuses silent cross-version degradation by policy)
}
