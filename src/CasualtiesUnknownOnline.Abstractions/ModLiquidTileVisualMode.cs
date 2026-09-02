namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// Visual projection mode for a <see cref="ModLiquidTileDefinition"/>. The
/// values mirror the CUCoreLib vocabulary so migrating mods can keep their
/// authored visual intent; the current CUO Game Adapter implements the
/// tint/base-byte path and treats the asset-backed modes as a future
/// mod-local resource seam.
/// </summary>
public enum ModLiquidTileVisualMode
{
	/// <summary>Render with the referenced vanilla liquid prefab and apply the authored tint.</summary>
	ExistingLiquidPlusTint = 0,

	/// <summary>Render with a custom material. Requires a future mod-local resource API.</summary>
	Material = 1,

	/// <summary>Render with a custom sprite particle material. Requires a future mod-local resource API.</summary>
	Sprite = 2,

	/// <summary>Render with a generated high-resolution image material. Requires a future mod-local resource API.</summary>
	HighResImageGenerated = 3
}
