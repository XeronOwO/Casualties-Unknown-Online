using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The mod-authored definition of one liquid-tile / world-liquid definition.
/// It is a plain DTO in Abstractions: no game assembly, no Unity type, no
/// Runtime dependency. The Game Adapter liquid-tile provider reads it,
/// allocates a stable custom world-fluid byte, maps the static fields into
/// the vanilla fluid grid behaviour, and runs local projection (body touch,
/// drink, visual) entirely inside the Game Adapter.
///
/// Behaviour callbacks (CUCoreLib-style OnTouch/OnEnter/OnExit/OnDrinkOverride)
/// are intentionally not part of this DTO: mods cannot pass game delegates
/// through Abstractions, and CUO's local-compute/remote-verify model keeps
/// per-player body effects on the acting client.
/// </summary>
public sealed class ModLiquidTileDefinition : IModContentDefinition
{
	/// <summary>The mod-scoped content id: a canonical lower-case path segment, unique within the registering mod.</summary>
	public string Id { get; set; } = "";

	/// <summary>The content kind this definition registers under - fixed by its type, never chosen by a caller.</summary>
	public string Kind => ModContentKind.LiquidTile;

	/// <summary>The mod-owned content schema version the framework stores verbatim (default 1).</summary>
	public int SchemaVersion { get; set; } = 1;

	/// <summary>Player-facing liquid-tile name. When empty, the LiquidId locale is used.</summary>
	public string DisplayName { get; set; } = "";

	/// <summary>Player-facing liquid-tile description. When empty, the LiquidId locale is used.</summary>
	public string Description { get; set; } = "";

	/// <summary>
	/// Logical liquid content id (vanilla or a registered <see cref="ModLiquidDefinition"/>).
	/// Used for drinking and display resolution.
	/// </summary>
	public string LiquidId { get; set; } = "";

	/// <summary>
	/// Logical liquid id used when the world byte is mapped by container/fill
	/// tools. Defaults to <see cref="LiquidId"/>.
	/// </summary>
	public string FillLiquidId { get; set; } = "";

	/// <summary>Buoyancy applied to a body standing in this liquid.</summary>
	public float Buoyancy { get; set; } = 0.6f;

	/// <summary>Drag applied to a body moving through this liquid.</summary>
	public float Drag { get; set; } = 0.915f;

	/// <summary>Whether the liquid sets the body's in-water flag (native push/slip handling).</summary>
	public bool PushBodies { get; set; } = true;

	/// <summary>Wetness added per second while touching the liquid.</summary>
	public float WetnessPerSecond { get; set; } = 20f;

	/// <summary>Temperature delta per second while touching the liquid.</summary>
	public float TemperaturePerSecond { get; set; }

	/// <summary>Sickness added per second while touching the liquid.</summary>
	public float SicknessPerSecond { get; set; }

	/// <summary>Dirtiness added per second while touching the liquid.</summary>
	public float DirtynessPerSecond { get; set; }

	/// <summary>Limb disinfection time given per second while touching the liquid.</summary>
	public float DisinfectPerSecond { get; set; }

	/// <summary>Slip time added per second while touching the liquid (0..1 clamp).</summary>
	public float SlipPerSecond { get; set; }

	/// <summary>Ragdoll-bar drain per second while touching the liquid (0..1 clamp).</summary>
	public float RagdollBarDrainPerSecond { get; set; }

	/// <summary>Visual projection mode. Only tint/base-byte rendering is implemented in the current CUO seam.</summary>
	public ModLiquidTileVisualMode VisualMode { get; set; } = ModLiquidTileVisualMode.ExistingLiquidPlusTint;

	/// <summary>
	/// Vanilla world-fluid byte used as the visual base (1 = water, 2 = algae,
	/// 3 = oil, 4 = sap, 5 = dirty water, 6 = magma). Custom tiles are rendered
	/// with that base particle prefab and their own tint.
	/// </summary>
	public int VisualLiquidByte { get; set; } = 1;

	/// <summary>Liquid-tile tint red component (0..1).</summary>
	public float TintR { get; set; } = 1f;

	/// <summary>Liquid-tile tint green component (0..1).</summary>
	public float TintG { get; set; } = 1f;

	/// <summary>Liquid-tile tint blue component (0..1).</summary>
	public float TintB { get; set; } = 1f;

	/// <summary>Liquid-tile tint alpha component (0..1).</summary>
	public float TintA { get; set; } = 1f;

	/// <summary>
	/// Optional resource path to a custom liquid visual asset. This is the
	/// stable future seam for mod-local asset injection; it is not interpreted
	/// by the current CUO core (the provider logs and falls back to tint).
	/// </summary>
	public string VisualAssetPath { get; set; } = "";

	/// <summary>
	/// Copper-relative world-generation multiplier. Zero disables automatic
	/// spawning.
	/// </summary>
	public float SpawnAmount { get; set; }

	/// <summary>
	/// Bitmask of allowed world layers for automatic spawning. -1 means every
	/// layer; 0 disables automatic spawning. Layer N is bit N-1 (N starts at 1).
	/// </summary>
	public int SpawnLayers { get; set; } = AllSpawnLayers;

	/// <summary>Maximum number of cells one flood-fill seed may fill during world generation.</summary>
	public int MaxFloodFill { get; set; } = 128;

	/// <summary>Whether drinking a custom liquid cell consumes it.</summary>
	public bool ConsumeOnDrink { get; set; } = true;

	/// <summary>Whether filling a container from the custom liquid cell consumes it.</summary>
	public bool ConsumeOnFill { get; set; } = true;

	/// <summary>Extensible mod-owned metadata for future binders/features.</summary>
	public Dictionary<string, string> CustomData
	{
		get;
		set => field = value ?? [];
	} = [];

	/// <summary>Bitmask that allows spawning on every world layer.</summary>
	public const int AllSpawnLayers = -1;

	/// <summary>Build a layer bitmask from one-based layer numbers.</summary>
	public static int LayersToMask(params int[] layerNumbers)
	{
		if (layerNumbers is null)
		{
			return 0;
		}

		var mask = 0;
		foreach (var layer in layerNumbers)
		{
			if (layer > 0 && layer <= 31)
			{
				mask |= 1 << (layer - 1);
			}
		}

		return mask;
	}

	/// <summary>Build an all-layers bitmask excluding one-based layer numbers.</summary>
	public static int AllLayersExcept(params int[] excludedLayerNumbers)
	{
		if (excludedLayerNumbers is null || excludedLayerNumbers.Length == 0)
		{
			return AllSpawnLayers;
		}

		var mask = AllSpawnLayers;
		foreach (var layer in excludedLayerNumbers)
		{
			if (layer > 0 && layer <= 31)
			{
				mask &= ~(1 << (layer - 1));
			}
		}

		return mask;
	}

	/// <summary>
	/// Whether this liquid tile is permitted to spawn automatically on a
	/// zero-based biome depth. Depth 0 is layer 1.
	/// </summary>
	public bool CanSpawnInLayer(int biomeDepth)
	{
		if (SpawnLayers == 0 || biomeDepth < 0)
		{
			return false;
		}

		if (SpawnLayers == AllSpawnLayers)
		{
			return true;
		}

		var layerNumber = biomeDepth + 1;
		return layerNumber > 0 && layerNumber <= 31 && (SpawnLayers & (1 << (layerNumber - 1))) != 0;
	}

}
