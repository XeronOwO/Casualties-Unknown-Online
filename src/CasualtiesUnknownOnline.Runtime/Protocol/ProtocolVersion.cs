namespace CasualtiesUnknownOnline.Runtime.Protocol;

public static class ProtocolVersion
{
	/// <summary>Bumped on any breaking wire change.</summary>
	public const int Current = 17; // v17: CharacterSound (NetMsg 94) — a v16 peer would silently miss every remote action sound (presentation degradation, but the handshake refuses silent cross-version degradation by policy)
}
