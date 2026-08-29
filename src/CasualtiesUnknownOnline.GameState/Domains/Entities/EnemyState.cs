namespace CasualtiesUnknownOnline.GameState.Domains.Entities;

/// <summary>
/// Authoritative enemy/entity kernel fact: identity, prefab, health, and
/// runtime-spawn marker. High-frequency position/velocity remains a stream.
/// </summary>
public sealed record EnemyState(
	EntityId EntityId,
	string PrefabId,
	float Health,
	bool RuntimeSpawned,
	bool Stunned);
