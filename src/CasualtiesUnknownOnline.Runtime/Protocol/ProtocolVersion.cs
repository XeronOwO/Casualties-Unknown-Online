namespace CasualtiesUnknownOnline.Runtime.Protocol;

public static class ProtocolVersion
{
	/// <summary>Bumped on any breaking wire change.</summary>
	public const int Current = 21; // v21: BuildingEntityDamagedMsg.PlayHitSound — a v20 peer would replay the entity hitSound for silent cactus self-damage (the handshake refuses silent cross-version degradation by policy)
}
