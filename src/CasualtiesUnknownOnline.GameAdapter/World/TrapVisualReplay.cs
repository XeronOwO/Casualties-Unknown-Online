using System;
using CasualtiesUnknownOnline.Runtime.Protocol;
using HarmonyLib;
using Microsoft.Extensions.Logging;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// Guest-side replay of a trap event (the host's relay): the pure-visual
/// explosion five-piece (sound/particle/blastmark/shake — WorldGeneration.cs:
/// 3965-3970, the no-side-effect part), the real-body effect segment
/// (ExplosionBodyEffect — standing near a replayed blast hurts), and the
/// entity consumption (exploded = true + health = 0 + RemoteEntityDeath — the
/// destroy happens through the existing remote-death path: no drop roll, no
/// local crater). Never calls CreateExplosion itself — the crater rides the
/// SetBlock relay and a local call would double-blast the local world.
/// </summary>
internal sealed class TrapVisualReplay(ILogger<TrapVisualReplay> log)
{
	private readonly ILogger<TrapVisualReplay> _log = log;

	internal void Replay(EntityEventKind kind, Vector2 position, byte extra)
	{
		switch (kind)
		{
			case EntityEventKind.MineExploded:
				ReplayMineExplosion(position);
				break;
			case EntityEventKind.ShuttleDoorOpened:
				ReplayState<ShuttleStartOpen>(position, kind, TrapStateActions.ApplyShuttleDoor);
				break;
			case EntityEventKind.LifepodHeatChanged:
				ReplayState<LifepodController>(position, kind, c => TrapStateActions.ApplyHeat(c, extra));
				break;
			case EntityEventKind.LifepodShowerActivated:
				ReplayState<LifepodController>(position, kind, TrapStateActions.ApplyShower);
				break;
			case EntityEventKind.BioTerminalUnlocked:
				ReplayState<BioTerminalScript>(position, kind, TrapStateActions.ApplyBioTerminal);
				break;
			case EntityEventKind.ScrapEaterProgress:
				ReplayState<ScrapEaterScript>(position, kind, e => TrapStateActions.ApplyScrapEater(e, extra));
				break;
			case EntityEventKind.MedStationHealed:
				ReplayState<MedStationScript>(position, kind, TrapStateActions.ApplyMedStation);
				break;
			case EntityEventKind.BatteryInserted:
				ReplayState<BatteryRecharger>(position, kind, TrapStateActions.ApplyBattery);
				break;
			case EntityEventKind.SpikeStabbed:
				ReplayState<SpikeStabberScript>(position, kind, TrapStateActions.ApplySpike);
				break;
			case EntityEventKind.BearTrapClamped:
				ReplayState<BearTrap>(position, kind, TrapStateActions.ApplyBearTrapClamped);
				break;
			case EntityEventKind.BearTrapReleased:
				ReplayState<BearTrap>(position, kind, TrapStateActions.ApplyBearTrapReleased);
				break;
			case EntityEventKind.StalactiteDropped:
				ReplayState<StalactiteDropper>(position, kind, TrapStateActions.ApplyStalactite);
				break;
			case EntityEventKind.GeyserActivated:
				ReplayState<GeyserScript>(position, kind, g => TrapStateActions.ApplyGeyser(g, extra));
				break;
			case EntityEventKind.SoundCannonFired:
				ReplayState<SoundCannon>(position, kind, TrapStateActions.ApplySoundCannon);
				break;
			case EntityEventKind.CaveTicksSpawned:
				ReplayState<CaveTickSpawner>(position, kind, TrapStateActions.ApplyCaveTicks);
				break;
			case EntityEventKind.CrystalFragileBroken:
				ReplayState<CrystalBehaviour>(position, kind, TrapStateActions.ApplyCrystalFragile);
				break;
			case EntityEventKind.TurretSelfDestructed:
				ReplayTurretSelfDestructed(position);
				break;
			case EntityEventKind.BarbedFenceHit:
				ReplayState<BarbedFence>(position, kind, TrapStateActions.ApplyBarbedFence);
				break;
			case EntityEventKind.CoilShocked:
				ReplayState<CoilScript>(position, kind, TrapStateActions.ApplyCoil);
				break;
			case EntityEventKind.CactusHit:
				ReplayState<CactusScript>(position, kind, TrapStateActions.ApplyCactus);
				break;
			case EntityEventKind.JumpPadLaunched:
				ReplayState<JumpPadScript>(position, kind, TrapStateActions.ApplyJumpPad);
				break;
			case EntityEventKind.BananaPlantSlip:
				ReplayState<BananaPlantSlip>(position, kind, TrapStateActions.ApplyBananaSlip);
				break;
			case EntityEventKind.CrystalElectricShocked:
				ReplayState<CrystalBehaviour>(position, kind, TrapStateActions.ApplyCrystalElectric);
				break;
			case EntityEventKind.TurretFired:
				ReplayState<TurretScript>(position, kind, TrapStateActions.ApplyTurretFired);
				break;
			case EntityEventKind.GrabberGrabbed:
				// The grab's visuals are the player-side ragdoll/scream (each
				// side's own body); the tendril animation is Update-driven
				// everywhere — nothing to replay, the trace line is the record.
				break;
			default:
				_log.LogWarning("[TrapEvent] no replay action for {Kind}.", kind);
				break;
		}
	}

	/// <summary>A state-family replay: run the shared action on the local entity
	/// at the position (the transition itself; the entity animates). The action
	/// reports whether it APPLIED — a false (the local copy already consumed the
	/// one-shot: the two-trigger race) is DROPPED with a trace.</summary>
	private void ReplayState<T>(Vector2 position, EntityEventKind kind, Func<T, bool> action) where T : Component
	{
		var entity = TrapEffectApplier.FindTrap<T>(position);
		if (entity == null) // Unity object — ==
		{
			_log.LogInformation("[TrapEvent] {Kind} at {Pos} already gone — visual only.", kind, position);
			return;
		}

		if (action(entity))
		{
			_log.LogInformation("[TrapEvent] replayed {Kind} at {Pos}.", kind, position);
		}
		else
		{
			_log.LogWarning("[TrapEvent] {Kind} at {Pos} already consumed locally — duplicate dropped.", kind, position);
		}
	}

	private void ReplayMineExplosion(Vector2 position)
	{
		var pos = position + Vector2.up; // the mine's explosion point (MineScript.cs:35-38)
		var param = new ExplosionParams { position = pos };

		// The consumption check comes FIRST: the local copy may have already
		// exploded (the two-trigger race — both guests tripped the same mine;
		// the local explosion already played AND already hurt the local body;
		// this duplicate relay must be dropped, never replayed).
		var mine = TrapEffectApplier.FindTrap<MineScript>(position);
		if (mine != null && Traverse.Create(mine).Field("exploded").GetValue<bool>()) // Unity object — ==
		{
			_log.LogWarning("[TrapEvent] mine at {Pos} already exploded locally — duplicate dropped.", position);
			return;
		}

		// The pure-visual five-piece + the real-body segment: the replaying
		// player near the blast is hurt exactly like the game would hurt them.
		ReplayExplosionVisual(param);
		ExplosionBodyEffect.ApplyToLocalBodies(param);

		// Consume the entity: exploded = true FIRST (the game's OnDestroy then
		// skips its chain explosion), killed as a REMOTE death (no drop roll —
		// the trigger side rolled and reported them).
		if (mine != null)
		{
			Traverse.Create(mine).Field("exploded").SetValue(true);
			mine.build.health = 0f;
			mine.gameObject.AddComponent<RemoteEntityDeath>();
		}
		else
		{
			_log.LogInformation("[TrapEvent] mine at {Pos} already gone — visual only.", position);
		}

		_log.LogInformation("[TrapEvent] replayed mine explosion at {Pos}.", position);
	}

	/// <summary>The turret self-destructed (on the host or another guest): replay
	/// the explosion with the turret's own parameters — pure-visual five-piece +
	/// real-body effect + remote-death consumption. The health &lt; 0.5 check is
	/// the consumption mark (already dead = a duplicate, dropped).</summary>
	private void ReplayTurretSelfDestructed(Vector2 position)
	{
		var turret = TrapEffectApplier.FindTrap<TurretScript>(position);
		var build = turret != null ? Traverse.Create(turret).Field("build").GetValue<BuildingEntity>() : null; // Unity object — ==
		if (build != null && build.health < 0.5f) // Unity object — ==
		{
			_log.LogWarning("[TrapEvent] turret at {Pos} already dead — duplicate dropped.", position);
			return;
		}

		var param = TrapEffectApplier.TurretExplosionParams(position);
		ReplayExplosionVisual(param);
		ExplosionBodyEffect.ApplyToLocalBodies(param);

		if (turret != null) // Unity object — ==
		{
			build!.health = 0f;
			turret.gameObject.AddComponent<RemoteEntityDeath>();
			var collider = turret.GetComponent<Collider2D>();
			if (collider != null) // Unity object — ==
			{
				collider.enabled = false;
			}
		}

		_log.LogInformation("[TrapEvent] replayed turret self-destruct at {Pos}.", position);
	}

	/// <summary>The pure-visual explosion five-piece (WorldGeneration.cs:3965-3970)
	/// — shared by the mine and turret replays.</summary>
	private void ReplayExplosionVisual(ExplosionParams param)
	{
		Sound.Play(param.sound, Vector2.zero, true, false, null, 1f, 1f, false, false);
		Object.Instantiate(Resources.Load("Special/ExplosionParticle"), param.position, Quaternion.identity);
		// The blastmark without a chunk parent (the game parents it to the
		// closest chunk, WorldGeneration.cs:3967-3969, which pulls the
		// Tilemap/GridLayout modules into the reference graph — a pure visual
		// difference, no state).
		var blastmark = Object.Instantiate(Resources.Load("Special/blastmark"), param.position, Quaternion.identity) as GameObject;
		if (blastmark != null) // Unity object — ==
		{
			blastmark.transform.eulerAngles = new Vector3(0f, 0f, Random.value * 360f);
		}

		PlayerCamera.main.shaker.Shake(param.range * 20f);
	}
}
