using System;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.GameAdapter.WorldGen;
using Microsoft.Extensions.Logging;
using Random = UnityEngine.Random;

namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// The world-start parameters domain: capture the generation baseline (RNG
/// state + run settings + world-defining fields) at the run-start entry or the
/// GenerateWorld boundary, publish it for the guests, restore it on the guest
/// (so both sides generate the identical world) and force the generation
/// stream back to it right before the coroutine starts. Split out of
/// RunCoordinator (gate-driven — it was 598 lines, 2 from the 600-line gate);
/// the run-lifecycle phase machine stays in RunCoordinator, which calls the
/// two boundary hooks (OnGenerateBoundary host branch / EnsureGuestApplied
/// guest branch).
/// </summary>
internal sealed class WorldParamsService(
	IWorldControl world,
	ILogger<WorldParamsService> log)
{
	private readonly IWorldControl _world = world;
	private readonly ILogger<WorldParamsService> _log = log;

	/// <summary>Host: params captured at the run-start entry — the first GenerateWorld must not re-capture.</summary>
	private bool _entryParamsCaptured;

	/// <summary>Guest: the params instance whose Random.state is currently restored (a new instance = a new world/layer = re-apply).</summary>
	private WorldStartParams? _appliedWorldParams;

	/// <summary>Guest: the "generation holding for params" log fired for this wait.</summary>
	private bool _guestParamsWaitLogged;

	/// <summary>
	/// Host: capture + publish the world params at the run-start entry (the
	/// click moment), BEFORE any run randomness is consumed — the generation
	/// stream is force-reset to this baseline at the GenerateWorld boundary, so
	/// capturing now is equivalent to capturing there, and the guests get the
	/// params with zero waiting. Run settings come from the menu (PreRunScript —
	/// WorldGeneration.runSettings is only assigned inside StartRun); the
	/// tutorial nulls them itself (PreRunScript.cs:312). The world-defining
	/// fields are all defaults at the entry: biomeOverride follows the entry
	/// kind (tutorial or not — its other source is the WorldGeneration.Awake
	/// tutorial flag, identical on both sides), depth and traveled start at 0
	/// (debugStartDepth is a debug-console value).
	/// </summary>
	internal void CaptureAtEntry(bool isTutorial)
	{
		_world.ResetDamagedBlocks(); // the new run's damage table starts empty again

		var randomState = RandomStateSerializer.Serialize(Random.state);
		var runSettings = isTutorial ? null : HarmonyTraverse.ReadPreRunRunSettings();
		_world.PublishWorldParams(new WorldStartParams
		{
			RandomState = randomState,
			RunSettings = runSettings,
			BiomeOverride = isTutorial ? (byte)WorldGeneration.OverrideSceneType.Tutorial : (byte)WorldGeneration.OverrideSceneType.None,
			BiomeDepth = 0,
			TotalTraveled = 0,
		});
		_entryParamsCaptured = true;
		_log.LogInformation("Captured world params at run-start entry ({StateBytes} bytes, {SettingCount} settings, tutorial: {Tutorial}).",
			randomState.Length, runSettings?.Count ?? 0, isTutorial);
	}

	/// <summary>
	/// Host, at the GenerateWorld boundary. First generation of a run that
	/// captured its params at the click moment: the entry capture is consumed
	/// (re-capturing here would move the baseline and re-send, racing the
	/// guests' already-started runs). Otherwise (layer switch, solo, load-run):
	/// snapshot what defines a run before generation consumes the RNG — the
	/// world-defining fields (biome override/depth, total traveled) were dead
	/// on the wire until this step, now captured with the RNG state.
	/// </summary>
	internal void OnGenerateBoundary()
	{
		if (_entryParamsCaptured)
		{
			_entryParamsCaptured = false;
			return;
		}

		CaptureAtBoundary();
	}

	/// <summary>Host side: capture + publish the world params at the GenerateWorld boundary (layer switches, solo, load-run — the entry capture does not apply).</summary>
	internal void CaptureAtBoundary()
	{
		// Host side: a new world (or layer) is generating — the damage table
		// starts empty again; mutations during generation are the baseline.
		_world.ResetDamagedBlocks();

		var randomState = RandomStateSerializer.Serialize(Random.state);
		var runSettings = HarmonyTraverse.ReadRunSettings();
		var biomeOverride = (byte)HarmonyTraverse.ReadBiomeOverride();
		var biomeDepth = (byte)HarmonyTraverse.ReadBiomeDepth();
		var totalTraveled = HarmonyTraverse.ReadTotalTraveled();
		_world.PublishWorldParams(new WorldStartParams
		{
			RandomState = randomState,
			RunSettings = runSettings,
			BiomeOverride = biomeOverride,
			BiomeDepth = biomeDepth,
			TotalTraveled = totalTraveled,
			// LoadedRun: no backing game field (PreRunScript.LoadRun is the
			// save-load flow — Phase 3 saves scope) — stays false on the wire.
		});
		_log.LogInformation("Captured world params ({StateBytes} bytes, {SettingCount} settings, "
			+ "biome {Biome}/{Depth}, traveled {Traveled}).",
			randomState.Length, runSettings?.Count ?? 0, biomeOverride, biomeDepth, totalTraveled);
	}

	/// <summary>
	/// Guest side, called before the generation coroutine may consume any
	/// Random: false while the host's world params have not arrived (the
	/// wrapper holds the coroutine — nothing random consumed yet); on arrival
	/// restores them and returns true. Idempotent per params instance — a layer
	/// switch delivers a new instance and re-applies. Host/solo: nothing to
	/// wait for.
	/// </summary>
	internal bool EnsureGuestApplied()
	{
		var parameters = _world.WorldParams;
		if (parameters is null)
		{
			// The wrapper polls every frame — log the hold once per wait, so a
			// held generation is observable without spamming the log.
			if (!_guestParamsWaitLogged)
			{
				_guestParamsWaitLogged = true;
				_log.LogInformation("World generation holding — host world params not arrived yet (fast guest transition).");
			}

			return false;
		}

		_guestParamsWaitLogged = false; // re-arm for the next world/layer
		if (!ReferenceEquals(_appliedWorldParams, parameters))
		{
			Apply(parameters);
			_appliedWorldParams = parameters;
		}

		return true;
	}

	/// <summary>
	/// Session ended: every capture/apply marker is session-scoped. The next
	/// host run captures new params; the next guest follow applies the new
	/// host's params (reference identity distinguishes them, but a dead
	/// marker must never consume the next run's capture).
	/// </summary>
	internal void ResetForSessionEnd()
	{
		_entryParamsCaptured = false;
		_appliedWorldParams = null;
		_guestParamsWaitLogged = false;
	}

	/// <summary>Guest side: restore the host's RNG state + run settings + world-defining fields so local world generation produces the same world.</summary>
	internal void Apply(WorldStartParams parameters)
	{
		Random.state = RandomStateSerializer.Deserialize(parameters.RandomState);
		if (parameters.RunSettings is not null)
		{
			HarmonyTraverse.WriteRunSettings(parameters.RunSettings);
		}

		HarmonyTraverse.WriteBiomeOverride(parameters.BiomeOverride);
		HarmonyTraverse.WriteBiomeDepth(parameters.BiomeDepth);
		HarmonyTraverse.WriteTotalTraveled(parameters.TotalTraveled);

		_log.LogInformation("Applied host world params ({StateBytes} bytes).", parameters.RandomState.Length);
	}

	/// <summary>
	/// Both sides: force Random.state back to the captured baseline right before
	/// the generation coroutine starts. The host captured it at its run-start
	/// entry — everything consumed between that moment and here (transition,
	/// scene loading, WorldGeneration.Start) is overwritten, keeping the two
	/// generation streams identical. Guest: the params were just applied by
	/// <see cref="EnsureGuestApplied"/> — same value, idempotent.
	/// </summary>
	internal void ResetGenStreamToBaseline()
	{
		var parameters = _world.WorldParams;
		if (parameters is null)
		{
			return;
		}

		Random.state = RandomStateSerializer.Deserialize(parameters.RandomState);
		_log.LogInformation("Generation stream reset to captured baseline ({StateBytes} bytes: {StateHex}).",
			parameters.RandomState.Length, BitConverter.ToString(parameters.RandomState).Replace("-", ""));
	}
}
