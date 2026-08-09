using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using HarmonyLib;
using Microsoft.Extensions.Logging;
using Random = UnityEngine.Random;

namespace CasualtiesUnknownOnline.GameAdapter.WorldGen;

/// <summary>
/// The layer modifier's application (guest side): the host's world rolled its
/// layer modifier at generation finish (ApplyLayerModifiers, WorldGeneration.cs:3729)
/// and ships it with the generation snapshot (WorldItemsSnapshotMsg) and the
/// periodic world-item snapshot (ItemSnapshotMsg — covers world entries outside
/// a generation: solo→lobby conversion, mid-session join). This side never rolls
/// its own: the game's ApplyLayerModifiers is skipped (LayerModifierApplyPatch)
/// because the decision reads the random stream AFTER the darken-wait
/// suspension, which the isolation does not restore (the suspension's
/// real-stream draws leak into it) — a local roll would pick a different
/// modifier than the host. Applying runs the modifier's Initialize (world side
/// effects — Flooded's PlaceLiquids, Cold's temperature offset) exactly like
/// the host's roll did, so the world behaves identically on every side.
/// </summary>
internal sealed class LayerModifierSync(ItemService items, ILogger<LayerModifierSync> log)
{
	private readonly ItemService _items = items;
	private readonly ILogger<LayerModifierSync> _log = log;

	/// <summary>The modifier currently applied on this side (-1 = none). The
	/// generation snapshot applies unconditionally (each layer's roll — the game
	/// reset every modifier at layer start); the periodic snapshot applies only
	/// on change (idempotent — Initialize is NOT idempotent: Flooded places
	/// liquids, a re-run would flood again).</summary>
	private int _applied = -1;

	internal void BindToSession()
	{
		_items.WorldItemsSnapshotReceived += OnWorldItemsSnapshot;
		_items.ItemSnapshotReceived += OnItemSnapshot;
	}

	internal void Unbind()
	{
		_items.WorldItemsSnapshotReceived -= OnWorldItemsSnapshot;
		_items.ItemSnapshotReceived -= OnItemSnapshot;
	}

	private void OnWorldItemsSnapshot(IReadOnlyList<ItemSnapshotEntryMsg> items, int layerModifierIndex, byte[]? layerModifierRandomState) => Apply(layerModifierIndex, layerModifierRandomState, force: true);

	private void OnItemSnapshot(IReadOnlyList<WorldItem> items, int layerModifierIndex, byte[]? layerModifierRandomState) => Apply(layerModifierIndex, layerModifierRandomState, force: false);

	private void Apply(int index, byte[]? randomState, bool force)
	{
		if (index < 0 || index >= LayerModifier.availableModifiers.Length)
		{
			return; // none / out of range — never touch a modifier state we cannot name
		}

		if (!force && index == _applied)
		{
			return; // periodic snapshot: idempotent
		}

		var world = WorldGeneration.world;
		if (world == null) // Unity object — ==
		{
			return;
		}

		if (randomState is not null)
		{
			try
			{
				// Replay the host's decision draws on the host's stream so the
				// modifier's Initialize consumes the SAME random sequence the
				// host's did — the world effects (Flooded's liquid fills,
				// Infested/Ionized's entity distributions) land in identical
				// positions on every side. The stream is NOT restored after:
				// it continues from the host's replay point (aligned).
				Random.state = RandomStateSerializer.Deserialize(randomState);
				if (Random.value < WorldGeneration.GetRunSettingFloat("layermodifierchance") * 0.01f)
				{
					Random.Range(0, LayerModifier.availableModifiers.Length); // the PickRandom draw — the value is the host's call
				}
			}
			catch (Exception ex)
			{
				_log.LogWarning(ex, "[LayerMod] random-state replay failed — initializing without it (world effects may diverge).");
			}
		}

		var modifier = LayerModifier.availableModifiers[index];
		modifier.Initialize(world);
		modifier.active = true;
		AccessTools.Field(typeof(WorldGeneration), "layerPrefix")?.SetValue(world, Locale.GetOther("layermodifier" + index));
		AccessTools.Field(typeof(WorldGeneration), "layerDescription")?.SetValue(world, Locale.GetOther("layermodifier" + index + "dsc"));
		_applied = index;
		_log.LogInformation("[LayerMod] applied host modifier {Index}.", index);
	}
}
