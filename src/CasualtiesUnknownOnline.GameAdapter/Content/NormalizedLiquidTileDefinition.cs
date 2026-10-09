using System.Collections.Generic;
using CasualtiesUnknownOnline.Abstractions;

namespace CasualtiesUnknownOnline.GameAdapter.Content;

/// <summary>
/// The view of a mod's liquid-tile declaration that the fluid grid is actually
/// built from: the declaration itself plus the defaults CUO applies when the
/// author left a member out (a blank liquid id falls back to the content id, a
/// non-positive flood-fill budget becomes one, a visual base outside the
/// vanilla range becomes water).
///
/// It exists because the defaults must not be WRITTEN BACK into the mod's
/// object: a declaration is a contract the framework reads, and an
/// implementation may compute its members, so "the framework stored your
/// declaration" and "the framework edited your declaration" cannot both be
/// true. Wrapping keeps the rule in one place and leaves the author's object
/// untouched, while every reader keeps asking the declaration it already asks.
///
/// Every member the grid does not normalize is delegated unchanged, and the
/// delegated set is complete by construction: the interface makes the compiler
/// name any member this view forgets.
/// </summary>
internal sealed class NormalizedLiquidTileDefinition(
	IModLiquidTileDefinition declaration,
	string liquidId,
	string fillLiquidId,
	int maxFloodFill,
	int visualLiquidByte,
	bool consumeOnDrink) : IModLiquidTileDefinition
{
	/// <inheritdoc />
	public string Id => declaration.Id;

	/// <inheritdoc />
	public string Kind => declaration.Kind;

	/// <inheritdoc />
	public int SchemaVersion => declaration.SchemaVersion;

	/// <inheritdoc />
	public string DisplayName => declaration.DisplayName;

	/// <inheritdoc />
	public string Description => declaration.Description;

	/// <inheritdoc />
	public string LiquidId => liquidId;

	/// <inheritdoc />
	public string FillLiquidId => fillLiquidId;

	/// <inheritdoc />
	public float Buoyancy => declaration.Buoyancy;

	/// <inheritdoc />
	public float Drag => declaration.Drag;

	/// <inheritdoc />
	public bool PushBodies => declaration.PushBodies;

	/// <inheritdoc />
	public float WetnessPerSecond => declaration.WetnessPerSecond;

	/// <inheritdoc />
	public float TemperaturePerSecond => declaration.TemperaturePerSecond;

	/// <inheritdoc />
	public float SicknessPerSecond => declaration.SicknessPerSecond;

	/// <inheritdoc />
	public float DirtynessPerSecond => declaration.DirtynessPerSecond;

	/// <inheritdoc />
	public float DisinfectPerSecond => declaration.DisinfectPerSecond;

	/// <inheritdoc />
	public float SlipPerSecond => declaration.SlipPerSecond;

	/// <inheritdoc />
	public float RagdollBarDrainPerSecond => declaration.RagdollBarDrainPerSecond;

	/// <inheritdoc />
	public ModLiquidTileVisualMode VisualMode => declaration.VisualMode;

	/// <inheritdoc />
	public int VisualLiquidByte => visualLiquidByte;

	/// <inheritdoc />
	public float TintR => declaration.TintR;

	/// <inheritdoc />
	public float TintG => declaration.TintG;

	/// <inheritdoc />
	public float TintB => declaration.TintB;

	/// <inheritdoc />
	public float TintA => declaration.TintA;

	/// <inheritdoc />
	public string VisualAssetPath => declaration.VisualAssetPath;

	/// <inheritdoc />
	public float SpawnAmount => declaration.SpawnAmount;

	/// <inheritdoc />
	public int SpawnLayers => declaration.SpawnLayers;

	/// <inheritdoc />
	public int MaxFloodFill => maxFloodFill;

	/// <inheritdoc />
	public bool ConsumeOnDrink => consumeOnDrink;

	/// <inheritdoc />
	public bool ConsumeOnFill => declaration.ConsumeOnFill;

	/// <inheritdoc />
	public Dictionary<string, string> CustomData => declaration.CustomData;
}
