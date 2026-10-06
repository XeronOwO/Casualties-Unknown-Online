using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Session;
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
/// (generation snapshot and periodic snapshot streams). The
/// stream is rewound to the decision's post-draw state before Initialize, so
/// the world effects (Flooded's liquid fills, Infested/Ionized's entity
/// distributions) consume the SAME random sequence the host's did and land in
/// identical positions on every side. The snapshot index is also checked
/// against the local replay — a disagreement means the deterministic roll
/// failed somewhere (different baseline) and the host's snapshot wins.
/// </summary>
internal sealed class LayerModifierSync(IItemControl items, ILogger<LayerModifierSync> log)
{
	private readonly IItemControl _items = items;
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

	/// <summary>
	/// The two divergence diagnostics' repetition windows. Both are emitted per arriving
	/// snapshot, and a standing divergence sends the same values on every one of them: on a
	/// 10 Hz stream that is thousands of identical lines for one unchanged fact (batch
	/// `20261005-b` filled the member's log for minutes this way). The window keeps the
	/// diagnostic — first lines, then one summary when the pair finally changes — and the
	/// detector itself is untouched.
	/// </summary>
	private readonly LogRepetitionGuard _baselineLog = new(suppressAfter: 3);
	private readonly LogRepetitionGuard _indexLog = new(suppressAfter: 3);

	/// <summary>The (local, host) baseline pair the last snapshot reported, as key text — a change flushes the window.</summary>
	private string? _baselineKey;

	private string? _indexKey;

	internal void BindToSession()
	{
		_items.WorldItemsSnapshotReceived += OnWorldItemsSnapshot;
		_items.ItemSnapshotReceived += OnItemSnapshot;
	}

	internal void Unbind()
	{
		_items.WorldItemsSnapshotReceived -= OnWorldItemsSnapshot;
		_items.ItemSnapshotReceived -= OnItemSnapshot;

		// The session is over: say what the standing divergence cost before the windows go, exactly
		// as the item pump's own unbind does — a bound that swallowed silently would hide the very
		// divergence the detector exists to raise.
		FlushRepeat(_baselineLog, _baselineKey, "[LayerMod] baseline divergence — {Count} identical line(s) suppressed when the session ended.");
		FlushRepeat(_indexLog, _indexKey, "[LayerMod] index disagreement — {Count} identical line(s) suppressed when the session ended.");
		_baselineKey = null;
		_indexKey = null;
		_baselineLog.Clear();
		_indexLog.Clear();
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

			// A new layer is a new pair: whatever the windows swallowed belongs to the
			// layer that is being left, and the log says so before the counters go.
			FlushRepeat(_baselineLog, _baselineKey, "[LayerMod] baseline divergence — {Count} identical line(s) suppressed for the layer just left.");
			FlushRepeat(_indexLog, _indexKey, "[LayerMod] index disagreement — {Count} identical line(s) suppressed for the layer just left.");
			_baselineKey = null;
			_indexKey = null;
			_baselineLog.Clear();
			_indexLog.Clear();
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

	private void OnWorldItemsSnapshot(IReadOnlyList<WorldItem> items, int layerModifierIndex, byte[]? layerModifierRandomState)
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
			// Keyed on (snapshot, local): the pair IS the divergence. Repeats inside the
			// window report, a pair that changes reports again, and the flush says how many
			// identical lines the standing disagreement cost.
			var key = "index|" + index + "|" + _localIndex;
			if (key != _indexKey)
			{
				FlushRepeat(_indexLog, _indexKey, "[LayerMod] index disagreement — {Count} identical line(s) suppressed while it stood still.");
				_indexKey = key;
			}

			if (_indexLog.TryLog(key, key, out _))
			{
				_log.LogWarning("[LayerMod] snapshot index {Snapshot} disagrees with the local replay {Local} — applying the host's (authoritative).", index, _localIndex);
			}
		}

		if (decision.BaselineDiverged)
		{
			// The snapshot carries the host's decision-entry state (the rewound
			// segment start) — it must be bit-identical to the guest's local
			// entry state (both are the fingerprint-identical segment start).
			// A mismatch means the segment baselines diverged before the
			// decision: the local replay drew from the wrong position and the
			// world effects will diverge silently. It repeats on every snapshot
			// while the pair stands (see the guard's own note).
			var local = FormatState(_localEntryState);
			var host = FormatState(randomState);
			var key = local + "|" + host;
			if (key != _baselineKey)
			{
				FlushRepeat(_baselineLog, _baselineKey, "[LayerMod] baseline divergence — {Count} identical line(s) suppressed while the pair stood still.");
				_baselineKey = key;
			}

			if (_baselineLog.TryLog(key, key, out _))
			{
				_log.LogWarning(
					"[LayerMod] baseline divergence — local segment start {Local} vs host's {Host} (world effects may diverge).",
					local,
					host);
			}
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

	/// <summary>The state a divergence line prints: hex, or <c>-</c> when the snapshot carried none.</summary>
	private static string FormatState(byte[]? state) =>
		state is null ? "-" : BitConverter.ToString(state).Replace("-", "");

	/// <summary>
	/// The divergence behind <paramref name="key"/> stopped repeating — say how many identical
	/// lines its window swallowed before the log forgets it. Silent when nothing was suppressed.
	/// </summary>
	private void FlushRepeat(LogRepetitionGuard guard, string? key, string message)
	{
		if (key is not null && guard.TryFlush(key, out var suppressed))
		{
			_log.LogWarning(message, suppressed);
		}
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
