namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// Automatic world-generation placement style for a custom building entity.
/// The value is plain data in Abstractions; the Game Adapter consumes it during
/// the sealed generation stream.
/// </summary>
public enum ModBuildingGenerationStyle
{
	/// <summary>Disables automatic world generation.</summary>
	None = 0,

	/// <summary>Uses the standard building-entity distribution path (surface raycast).</summary>
	Standard = 1,

	/// <summary>Uses drop-pod-style placement (random high altitude, ground impact orientation).</summary>
	DropPod = 2
}
