namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The framework's ready-made <see cref="IModItemWearable"/>: a plain data object
/// in Abstractions with no game type, no Unity type and no Runtime dependency.
/// Use it when every value is a constant; implement the interface when one is
/// computed.
///
/// <para>
/// It stays constructible with no arguments and every member stays settable on
/// purpose, like the other content data classes: the framework reads the
/// interface, and a future data-driven loader (a JSON content pack) would
/// deserialize into this concrete type. A declaration that leaves
/// <see cref="Limb"/> or <see cref="SlotId"/> empty is read as "not wearable"
/// rather than as a garment the game cannot place.
/// </para>
/// </summary>
public sealed class ModItemWearable : IModItemWearable
{
	/// <inheritdoc />
	public string Limb { get; set; } = "";

	/// <inheritdoc />
	public string SlotId { get; set; } = "";

	/// <inheritdoc />
	public bool CanBeHeld { get; set; }

	/// <inheritdoc />
	public float Armor { get; set; }

	/// <inheritdoc />
	public float Isolation { get; set; }

	/// <inheritdoc />
	public float HitDurabilityLossMultiplier { get; set; }

	/// <inheritdoc />
	public int VisualOffset { get; set; } = 5;
}
