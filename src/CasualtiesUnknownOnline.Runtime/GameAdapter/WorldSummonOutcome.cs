namespace CasualtiesUnknownOnline.Runtime.GameAdapter;

/// <summary>
/// What one creation did: the created item's id (0 for an entity — entities carry no instance id), the item state
/// the object really carries (empty for an entity or a stateless item), and the refusal when nothing was created.
/// </summary>
public readonly record struct WorldSummonOutcome(bool Created, ulong InstanceId, string State, string? Refusal);
