using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// One liquid-tile / world-liquid declaration. This is the contract a consumer
/// reads: the Game Adapter liquid-tile provider allocates a stable custom
/// world-fluid byte, maps these members into the vanilla fluid grid behaviour,
/// and runs local projection (body touch, drink, visual) entirely inside the
/// Game Adapter.
///
/// Behaviour callbacks (CUCoreLib-style OnTouch/OnEnter/OnExit/OnDrinkOverride)
/// are intentionally not part of this contract: mods cannot pass game delegates
/// through Abstractions, and CUO's local-compute/remote-verify model keeps
/// per-player body effects on the acting client.
///
/// <see cref="ModLiquidTileDefinition"/> is the framework's ready-made
/// implementation: use it when every value is a constant, and implement this
/// interface when one is computed.
/// </summary>
public interface IModLiquidTileDefinition : IModContentDefinition
{
	/// <summary>Player-facing liquid-tile name. When empty, the LiquidId locale is used.</summary>
	string DisplayName { get; }

	/// <summary>Player-facing liquid-tile description. When empty, the LiquidId locale is used.</summary>
	string Description { get; }

	/// <summary>
	/// Logical liquid content id (vanilla or a registered <see cref="ModLiquidDefinition"/>).
	/// Used for drinking and display resolution.
	/// </summary>
	string LiquidId { get; }

	/// <summary>
	/// Logical liquid id used when the world byte is mapped by container/fill
	/// tools. Defaults to <see cref="LiquidId"/>.
	/// </summary>
	string FillLiquidId { get; }

	/// <summary>Buoyancy applied to a body standing in this liquid.</summary>
	float Buoyancy { get; }

	/// <summary>Drag applied to a body moving through this liquid.</summary>
	float Drag { get; }

	/// <summary>Whether the liquid sets the body's in-water flag (native push/slip handling).</summary>
	bool PushBodies { get; }

	/// <summary>Wetness added per second while touching the liquid.</summary>
	float WetnessPerSecond { get; }

	/// <summary>Temperature delta per second while touching the liquid.</summary>
	float TemperaturePerSecond { get; }

	/// <summary>Sickness added per second while touching the liquid.</summary>
	float SicknessPerSecond { get; }

	/// <summary>Dirtiness added per second while touching the liquid.</summary>
	float DirtynessPerSecond { get; }

	/// <summary>Limb disinfection time given per second while touching the liquid.</summary>
	float DisinfectPerSecond { get; }

	/// <summary>Slip time added per second while touching the liquid (0..1 clamp).</summary>
	float SlipPerSecond { get; }

	/// <summary>Ragdoll-bar drain per second while touching the liquid (0..1 clamp).</summary>
	float RagdollBarDrainPerSecond { get; }

	/// <summary>Visual projection mode. Only tint/base-byte rendering is implemented in the current CUO seam.</summary>
	ModLiquidTileVisualMode VisualMode { get; }

	/// <summary>
	/// Vanilla world-fluid byte used as the visual base (1 = water, 2 = algae,
	/// 3 = oil, 4 = sap, 5 = dirty water, 6 = magma). Custom tiles are rendered
	/// with that base particle prefab and their own tint.
	/// </summary>
	int VisualLiquidByte { get; }

	/// <summary>Liquid-tile tint red component (0..1).</summary>
	float TintR { get; }

	/// <summary>Liquid-tile tint green component (0..1).</summary>
	float TintG { get; }

	/// <summary>Liquid-tile tint blue component (0..1).</summary>
	float TintB { get; }

	/// <summary>Liquid-tile tint alpha component (0..1).</summary>
	float TintA { get; }

	/// <summary>
	/// Optional resource path to a custom liquid visual asset. This is the
	/// stable future seam for mod-local asset injection; it is not interpreted
	/// by the current CUO core (the provider logs and falls back to tint).
	/// </summary>
	string VisualAssetPath { get; }

	/// <summary>
	/// Copper-relative world-generation multiplier. Zero disables automatic
	/// spawning.
	/// </summary>
	float SpawnAmount { get; }

	/// <summary>
	/// Bitmask of allowed world layers for automatic spawning. -1 means every
	/// layer; 0 disables automatic spawning. Layer N is bit N-1 (N starts at 1).
	/// </summary>
	int SpawnLayers { get; }

	/// <summary>Maximum number of cells one flood-fill seed may fill during world generation.</summary>
	int MaxFloodFill { get; }

	/// <summary>Whether drinking a custom liquid cell consumes it.</summary>
	bool ConsumeOnDrink { get; }

	/// <summary>Whether filling a container from the custom liquid cell consumes it.</summary>
	bool ConsumeOnFill { get; }

	/// <summary>Extensible mod-owned metadata for future binders/features.</summary>
	Dictionary<string, string> CustomData { get; }
}
