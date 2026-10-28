namespace CasualtiesUnknownOnline.Runtime.GameAdapter;

/// <summary>
/// What one creation did: the created item's id (0 for an entity — entities carry no instance id), the item state the object really carries (empty for
/// an entity or a stateless item), and the refusal when nothing was created — the refusal as a KEYED answer (<see cref="GrantRefusal"/>), because the
/// reason is known here and the console that reads it must render it in the reader's language.
/// </summary>
public readonly record struct WorldSummonOutcome(bool Created, ulong InstanceId, string State, GrantRefusal? Refusal);
