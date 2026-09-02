using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The versioned, mod-authored data contract for one liquid-tile / world-liquid
/// definition. It is a plain DTO in Abstractions: no game assembly, no Unity
/// type, no Runtime dependency. The Game Adapter liquid-tile provider decodes
/// it, allocates a stable custom world-fluid byte, maps the static fields into
/// the vanilla fluid grid behaviour, and runs local projection (body touch,
/// drink, visual) entirely inside the Game Adapter.
///
/// Behaviour callbacks (CUCoreLib-style OnTouch/OnEnter/OnExit/OnDrinkOverride)
/// are intentionally not part of this DTO: mods cannot pass game delegates
/// through Abstractions, and CUO's local-compute/remote-verify model keeps
/// per-player body effects on the acting client.
/// </summary>
[DataContract]
public sealed class ModLiquidTileDefinition
{
	/// <summary>Player-facing liquid-tile name. When empty, the LiquidId locale is used.</summary>
	[DataMember(Order = 1)]
	public string DisplayName { get; set; } = "";

	/// <summary>Player-facing liquid-tile description. When empty, the LiquidId locale is used.</summary>
	[DataMember(Order = 2)]
	public string Description { get; set; } = "";

	/// <summary>
	/// Logical liquid content id (vanilla or a registered <see cref="ModLiquidDefinition"/>).
	/// Used for drinking and display resolution.
	/// </summary>
	[DataMember(Order = 3)]
	public string LiquidId { get; set; } = "";

	/// <summary>
	/// Logical liquid id used when the world byte is mapped by container/fill
	/// tools. Defaults to <see cref="LiquidId"/>.
	/// </summary>
	[DataMember(Order = 4)]
	public string FillLiquidId { get; set; } = "";

	/// <summary>Buoyancy applied to a body standing in this liquid.</summary>
	[DataMember(Order = 5)]
	public float Buoyancy { get; set; } = 0.6f;

	/// <summary>Drag applied to a body moving through this liquid.</summary>
	[DataMember(Order = 6)]
	public float Drag { get; set; } = 0.915f;

	/// <summary>Whether the liquid sets the body's in-water flag (native push/slip handling).</summary>
	[DataMember(Order = 7)]
	public bool PushBodies { get; set; } = true;

	/// <summary>Wetness added per second while touching the liquid.</summary>
	[DataMember(Order = 8)]
	public float WetnessPerSecond { get; set; } = 20f;

	/// <summary>Temperature delta per second while touching the liquid.</summary>
	[DataMember(Order = 9)]
	public float TemperaturePerSecond { get; set; }

	/// <summary>Sickness added per second while touching the liquid.</summary>
	[DataMember(Order = 10)]
	public float SicknessPerSecond { get; set; }

	/// <summary>Dirtiness added per second while touching the liquid.</summary>
	[DataMember(Order = 11)]
	public float DirtynessPerSecond { get; set; }

	/// <summary>Limb disinfection time given per second while touching the liquid.</summary>
	[DataMember(Order = 12)]
	public float DisinfectPerSecond { get; set; }

	/// <summary>Slip time added per second while touching the liquid (0..1 clamp).</summary>
	[DataMember(Order = 13)]
	public float SlipPerSecond { get; set; }

	/// <summary>Ragdoll-bar drain per second while touching the liquid (0..1 clamp).</summary>
	[DataMember(Order = 14)]
	public float RagdollBarDrainPerSecond { get; set; }

	/// <summary>Visual projection mode. Only tint/base-byte rendering is implemented in the current CUO seam.</summary>
	[DataMember(Order = 15)]
	public ModLiquidTileVisualMode VisualMode { get; set; } = ModLiquidTileVisualMode.ExistingLiquidPlusTint;

	/// <summary>
	/// Vanilla world-fluid byte used as the visual base (1 = water, 2 = algae,
	/// 3 = oil, 4 = sap, 5 = dirty water, 6 = magma). Custom tiles are rendered
	/// with that base particle prefab and their own tint.
	/// </summary>
	[DataMember(Order = 16)]
	public int VisualLiquidByte { get; set; } = 1;

	/// <summary>Liquid-tile tint red component (0..1).</summary>
	[DataMember(Order = 17)]
	public float TintR { get; set; } = 1f;

	/// <summary>Liquid-tile tint green component (0..1).</summary>
	[DataMember(Order = 18)]
	public float TintG { get; set; } = 1f;

	/// <summary>Liquid-tile tint blue component (0..1).</summary>
	[DataMember(Order = 19)]
	public float TintB { get; set; } = 1f;

	/// <summary>Liquid-tile tint alpha component (0..1).</summary>
	[DataMember(Order = 20)]
	public float TintA { get; set; } = 1f;

	/// <summary>
	/// Optional resource path to a custom liquid visual asset. This is the
	/// stable future seam for mod-local asset injection; it is not interpreted
	/// by the current CUO core (the provider logs and falls back to tint).
	/// </summary>
	[DataMember(Order = 21)]
	public string VisualAssetPath { get; set; } = "";

	/// <summary>
	/// Copper-relative world-generation multiplier. Zero disables automatic
	/// spawning.
	/// </summary>
	[DataMember(Order = 22)]
	public float SpawnAmount { get; set; }

	/// <summary>
	/// Bitmask of allowed world layers for automatic spawning. -1 means every
	/// layer; 0 disables automatic spawning. Layer N is bit N-1 (N starts at 1).
	/// </summary>
	[DataMember(Order = 23)]
	public int SpawnLayers { get; set; } = AllSpawnLayers;

	/// <summary>Maximum number of cells one flood-fill seed may fill during world generation.</summary>
	[DataMember(Order = 24)]
	public int MaxFloodFill { get; set; } = 128;

	/// <summary>Whether drinking a custom liquid cell consumes it.</summary>
	[DataMember(Order = 25)]
	public bool ConsumeOnDrink { get; set; } = true;

	/// <summary>Whether filling a container from the custom liquid cell consumes it.</summary>
	[DataMember(Order = 26)]
	public bool ConsumeOnFill { get; set; } = true;

	/// <summary>Extensible mod-owned metadata for future binders/features.</summary>
	[DataMember(Order = 27)]
	public Dictionary<string, string> CustomData { get; set; } = [];

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

	/// <summary>Serialize this definition into the opaque payload format.</summary>
	public byte[] ToPayload()
	{
		using var stream = new MemoryStream();
		var serializer = new DataContractSerializer(typeof(ModLiquidTileDefinition));
		serializer.WriteObject(stream, this);
		return stream.ToArray();
	}

	/// <summary>Deserialize a liquid-tile definition payload. Returns null when the payload is not valid.</summary>
	public static ModLiquidTileDefinition? FromPayload(byte[] payload)
	{
		if (payload is null)
		{
			return null;
		}

		try
		{
			using var stream = new MemoryStream(payload);
			var serializer = new DataContractSerializer(typeof(ModLiquidTileDefinition));
			return serializer.ReadObject(stream) as ModLiquidTileDefinition;
		}
		catch (Exception)
		{
			return null;
		}
	}
}
