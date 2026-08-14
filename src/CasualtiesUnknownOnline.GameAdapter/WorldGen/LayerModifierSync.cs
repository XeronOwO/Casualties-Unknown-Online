using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.World;
using HarmonyLib;
using Microsoft.Extensions.Logging;
using Random = UnityEngine.Random;

namespace CasualtiesUnknownOnline.GameAdapter.WorldGen;

/// <summary>
/// The layer modifier's application (guest side). On a generation the guest
/// replays the decision locally (LayerModifierApplyPatch — same draws from the
/// same segment start, so the roll and the stream position match the host's)
/// and this class defers the modifier's Initialize until the generation
/// finished (mid-generation Initialize conflicts with the terrain writes —
/// Flooded would place liquids into a world the generator is still writing);
/// outside a generation (no local replay: solo→lobby conversion, mid-session
/// join) the snapshot-carried index + random state are applied instead
/// (WorldItemsSnapshotMsg on generation, ItemSnapshotMsg periodically). The
/// stream is rewound to the decision's post-draw state before Initialize, so
/// the world effects (Flooded's liquid fills, Infested/Ionized's entity
/// distributions) consume the SAME random sequence the host's did and land in
/// identical positions on every side. The snapshot index is also checked
/// against the local replay — a disagreement means the deterministic roll
/// failed somewhere (different baseline) and the host's snapshot wins.
/// </summary>
internal sealed class LayerModifierSync(ItemService items, ILogger<LayerModifierSync> log)
{
	private readonly ItemService _items = items;
	private readonly ILogger<LayerModifierSync> _log = log;

	/// <summary>The modifier currently applied on this side (-1 = none).</summary>
	private int _applied = -1;

	/// <summary>A snapshot that arrived while the world was still generating —
	/// deferred until the pump sees the generation finished, same pattern as
	/// GeneratedItemApplication.</summary>
	private int _pendingIndex = -1;
	private byte[]? _pendingState;

	/// <summary>The guest's local replay for the current layer (see
	/// LayerModifierApplyPatch): the banner already carries the modifier name
	/// (the prefix write happened before it was built), so no banner resend is
	/// needed; Initialize is deferred to the pump. Kept until the next layer's
	/// generation starts — the snapshot's index is checked against it.
	/// _localIndex = -1 means the roll drew no modifier (the stream is aligned
	/// either way).</summary>
	private bool _localDecided;
	private int _localIndex = -1;
	private byte[]? _localEntryState;
	private byte[]? _localState;

	private bool _lastGenerating;

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

	/// <summary>The guest's local replay of the decision (index + stream state
	/// at the decision entry and after its draws) — the banner is already
	/// filled; Initialize runs once the generation finished.</summary>
	internal void OnLocalDecision(int index, byte[]? entryState, byte[]? afterState)
	{
		_localDecided = true;
		_localIndex = index;
		_localEntryState = entryState;
		_localState = afterState;
		_log.LogInformation("[LayerMod] guest local decision index={Index}.", index);
		Update();
	}

	/// <summary>Pump: apply a decision/snapshot that arrived during generation;
	/// reset per-layer state when a new generation starts.</summary>
	internal void Update()
	{
		var generating = HarmonyTraverse.IsGenerating();
		if (generating && !_lastGenerating)
		{
			// A new generation started — every layer rolls its own modifier
			// (the game resets them at layer start), so the per-layer state
			// resets too. The reset also makes a same-index roll on the new
			// layer apply (the layer's own roll, not an idempotent repeat).
			_applied = -1;
			_localDecided = false;
			_localIndex = -1;
			_localEntryState = null;
			_localState = null;
			_pendingIndex = -1;
			_pendingState = null;
		}
		_lastGenerating = generating;

		if (generating)
		{
			return;
		}

		var next = LayerModifierDecide.NextApply(_localDecided, _localIndex, _applied, _pendingIndex);
		if (next is not { } choice)
		{
			return;
		}

		if (choice.UseLocal)
		{
			RunInitialize(_localIndex, _localState, resendBanner: false);
			return;
		}

		var index = _pendingIndex;
		var state = _pendingState;
		_pendingIndex = -1;
		_pendingState = null;
		ApplySnapshot(index, state);
	}

	private void OnWorldItemsSnapshot(IReadOnlyList<ItemSnapshotEntryMsg> items, int layerModifierIndex, byte[]? layerModifierRandomState)
	{
		if (layerModifierIndex <= 0 || layerModifierIndex > LayerModifier.availableModifiers.Length)
		{
			return; // 0 = none; the wire encoding is modifierIndex + 1 (protobuf-net omits 0-valued ints — Foggy's raw index is 0)
		}

		ApplyIndex(layerModifierIndex - 1, layerModifierRandomState);
	}

	private void OnItemSnapshot(IReadOnlyList<WorldItem> items, int layerModifierIndex, byte[]? layerModifierRandomState)
	{
		if (layerModifierIndex <= 0 || layerModifierIndex > LayerModifier.availableModifiers.Length)
		{
			return;
		}

		ApplyIndex(layerModifierIndex - 1, layerModifierRandomState);
	}

	private void ApplyIndex(int index, byte[]? randomState)
	{
		var decision = LayerModifierDecide.OnSnapshot(
			_localDecided, _localIndex, _localEntryState, index, randomState, _applied, HarmonyTraverse.IsGenerating());

		if (decision.IndexDisagrees)
		{
			_log.LogWarning("[LayerMod] snapshot index {Snapshot} disagrees with the local replay {Local} — applying the host's (authoritative).", index, _localIndex);
		}

		if (decision.BaselineDiverged)
		{
			// The snapshot carries the host's decision-entry state (the rewound
			// segment start) — it must be bit-identical to the guest's local
			// entry state (both are the fingerprint-identical segment start).
			// A mismatch means the segment baselines diverged before the
			// decision: the local replay drew from the wrong position and the
			// world effects will diverge silently.
			_log.LogWarning("[LayerMod] baseline divergence — local segment start {Local} vs host's {Host} (world effects may diverge).",
				BitConverter.ToString(_localEntryState).Replace("-", ""), BitConverter.ToString(randomState).Replace("-", ""));
		}

		switch (decision.Next)
		{
			case LayerModifierDecision.Action.Apply:
				ApplySnapshot(index, randomState);
				break;
			case LayerModifierDecision.Action.Pending:
				_pendingIndex = index; // applied by the pump once generation ends
				_pendingState = randomState;
				break;
		}

		// Drop = idempotent — the snapshot of the layer's own roll (already
		// applied via the local replay) or a periodic repeat.
	}

	/// <summary>Snapshot path (no local replay — world entry outside a
	/// generation): restore the host's decision-entry state, replay its draws,
	/// then Initialize.</summary>
	private void ApplySnapshot(int index, byte[]? randomState)
	{
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
				// host's did. The stream is NOT restored after: it continues
				// from the host's replay point (aligned).
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

		RunInitialize(index, restoreTo: null, resendBanner: true);
	}

	private void RunInitialize(int index, byte[]? restoreTo, bool resendBanner)
	{
		var world = WorldGeneration.world;
		if (world == null) // Unity object — ==
		{
			return;
		}

		if (restoreTo is not null)
		{
			try
			{
				// The local replay already drew (and the world's frame-level
				// draws since then leaked into the public stream) — Initialize
				// must consume from the host's position: the replay point.
				Random.state = RandomStateSerializer.Deserialize(restoreTo);
			}
			catch (Exception ex)
			{
				_log.LogWarning(ex, "[LayerMod] local-decision state restore failed — initializing without it (world effects may diverge).");
			}
		}

		var modifier = LayerModifier.availableModifiers[index];
		modifier.Initialize(world);
		modifier.active = true;
		AccessTools.Field(typeof(WorldGeneration), "layerPrefix")?.SetValue(world, Locale.GetOther("layermodifier" + index));
		AccessTools.Field(typeof(WorldGeneration), "layerDescription")?.SetValue(world, Locale.GetOther("layermodifier" + index + "dsc"));
		_applied = index;
		_log.LogInformation("[LayerMod] applied host modifier {Index}.", index);

		// The entry banner was built at generation finish reading layerPrefix
		// (WorldGeneration.cs:3648). A local replay filled it before the build —
		// nothing to resend. Without one (world entry outside a generation) the
		// banner lacked the modifier name; re-show it with it (the game's own
		// build, WorldGeneration.cs:3640-3665).
		if (resendBanner && (world.loadingObject == null || !world.loadingObject.activeSelf)) // Unity object — ==; hidden = banner already shown
		{
			var prefix = AccessTools.Field(typeof(WorldGeneration), "layerPrefix")?.GetValue(world) as string;
			var description = AccessTools.Field(typeof(WorldGeneration), "layerDescription")?.GetValue(world) as string;
			var text = Locale.GetOther("layer") + " " + (world.biomeDepth + 1) + "\n<color=\"orange\">" + prefix + "</color> " + world.biomeTitles[world.biomeDepth];
			PlayerCamera.main.DoAlert(text, true);
			if (!string.IsNullOrEmpty(description))
			{
				PlayerCamera.main.StartCoroutine(PlayerCamera.main.DoAlertDelayed("<color=\"orange\">" + description + "</color>", false, 6f));
			}
		}
	}
}
