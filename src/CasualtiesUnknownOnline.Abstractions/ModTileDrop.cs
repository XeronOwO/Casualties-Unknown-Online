namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The framework's ready-made <see cref="IModTileDrop"/>: one authored drop entry
/// for a custom tile. It is a plain data contract in Abstractions — no game type,
/// no Unity type, no Runtime dependency — which is also why it cannot compute a
/// member. The Game Adapter resolves the item id through the existing
/// custom-item/prefab seam and spawns the drop with the local break report.
/// </summary>
public sealed class ModTileDrop : IModTileDrop
{
	/// <summary>Item content id or vanilla item id spawned when the tile breaks.</summary>
	public string ItemId { get; set; } = "";

	/// <summary>Probability that this drop is spawned (0..1).</summary>
	public float Chance { get; set; } = 1f;

	/// <summary>Minimum spawned item condition (0..1).</summary>
	public float MinCondition { get; set; }

	/// <summary>Maximum spawned item condition (0..1).</summary>
	public float MaxCondition { get; set; } = 1f;
}
