namespace CasualtiesUnknownOnline.Runtime.Protocol;

public static class ProtocolVersion
{
	/// <summary>Bumped on any breaking wire change.</summary>
	public const int Current = 19; // v19: CharacterSound (NetMsg 94) gains Footstep + LandingImpact kinds — a v18 peer would silently miss the step/landing events (presentation degradation, but the handshake refuses silent cross-version degradation by policy)
}
