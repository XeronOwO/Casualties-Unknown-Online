using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The framework's ready-made <see cref="IModLiquidTileDefinition"/>: a plain
/// data object in Abstractions with no game assembly, no Unity type and no
/// Runtime dependency, which is also why it cannot compute a member. Use it when
/// every value is a constant; implement the interface when one is computed.
///
/// It stays constructible with no arguments and every member stays settable on
/// purpose: the framework reads the interface, and a future data-driven loader
/// would deserialize into this concrete type.
///
/// The two static mask builders and the layer sentinel stay here as the
/// mod-facing helpers a declaration is authored with; the rule that reads the
/// mask is <see cref="ModLayerSpawnRule"/>.
/// </summary>
public sealed class ModLiquidTileDefinition : IModLiquidTileDefinition
{
	/// <inheritdoc />
	public string Id { get; set; } = "";

	/// <inheritdoc />
	public string Kind => ModContentKind.LiquidTile;

	/// <inheritdoc />
	public int SchemaVersion { get; set; } = 1;

	/// <inheritdoc />
	public string DisplayName { get; set; } = "";

	/// <inheritdoc />
	public string Description { get; set; } = "";

	/// <inheritdoc />
	public string LiquidId { get; set; } = "";

	/// <inheritdoc />
	public string FillLiquidId { get; set; } = "";

	/// <inheritdoc />
	public float Buoyancy { get; set; } = 0.6f;

	/// <inheritdoc />
	public float Drag { get; set; } = 0.915f;

	/// <inheritdoc />
	public bool PushBodies { get; set; } = true;

	/// <inheritdoc />
	public float WetnessPerSecond { get; set; } = 20f;

	/// <inheritdoc />
	public float TemperaturePerSecond { get; set; }

	/// <inheritdoc />
	public float SicknessPerSecond { get; set; }

	/// <inheritdoc />
	public float DirtynessPerSecond { get; set; }

	/// <inheritdoc />
	public float DisinfectPerSecond { get; set; }

	/// <inheritdoc />
	public float SlipPerSecond { get; set; }

	/// <inheritdoc />
	public float RagdollBarDrainPerSecond { get; set; }

	/// <inheritdoc />
	public ModLiquidTileVisualMode VisualMode { get; set; } = ModLiquidTileVisualMode.ExistingLiquidPlusTint;

	/// <inheritdoc />
	public int VisualLiquidByte { get; set; } = 1;

	/// <inheritdoc />
	public float TintR { get; set; } = 1f;

	/// <inheritdoc />
	public float TintG { get; set; } = 1f;

	/// <inheritdoc />
	public float TintB { get; set; } = 1f;

	/// <inheritdoc />
	public float TintA { get; set; } = 1f;

	/// <inheritdoc />
	public string VisualAssetPath { get; set; } = "";

	/// <inheritdoc />
	public float SpawnAmount { get; set; }

	/// <inheritdoc />
	public int SpawnLayers { get; set; } = AllSpawnLayers;

	/// <inheritdoc />
	public int MaxFloodFill { get; set; } = 128;

	/// <inheritdoc />
	public bool ConsumeOnDrink { get; set; } = true;

	/// <inheritdoc />
	public bool ConsumeOnFill { get; set; } = true;

	/// <inheritdoc />
	public Dictionary<string, string> CustomData
	{
		get;
		set => field = value ?? [];
	} = [];

	/// <summary>Bitmask that allows spawning on every world layer; one home, see <see cref="ModLayerSpawnRule.AllSpawnLayers"/>.</summary>
	public const int AllSpawnLayers = ModLayerSpawnRule.AllSpawnLayers;

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
}
