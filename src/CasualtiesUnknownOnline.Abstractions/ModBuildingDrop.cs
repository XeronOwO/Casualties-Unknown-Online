namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The framework's ready-made <see cref="IModBuildingDrop"/>: one authored drop
/// entry for a custom building entity. It is a plain data contract in
/// Abstractions — no game type, no Unity type, no Runtime dependency — which is
/// also why it cannot compute a member. The Game Adapter resolves the item id
/// through the existing custom-item/prefab seam and stores the converted vanilla
/// <c>ItemDrop</c> on the runtime template, so the vanilla building destruction
/// path can spawn authored drops.
/// </summary>
public sealed class ModBuildingDrop : IModBuildingDrop
{
	/// <summary>Item content id or vanilla item id spawned when the building is destroyed.</summary>
	public string ItemId { get; set; } = "";

	/// <summary>Probability that this drop is rolled (0..1).</summary>
	public float Chance { get; set; } = 1f;

	/// <summary>Minimum spawned item condition (0..1).</summary>
	public float MinCondition { get; set; }

	/// <summary>Maximum spawned item condition (0..1).</summary>
	public float MaxCondition { get; set; } = 1f;
}
