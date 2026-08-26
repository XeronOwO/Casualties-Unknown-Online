using System;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session.World;
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

	internal void Replay(EntityEventKind kind, Vector2 position, byte extra, float elapsedSeconds = 0f)
	{
		switch (kind)
		{
			case EntityEventKind.MinePressed:
				ReplayState<MineScript>(position, kind, TrapStateActions.ApplyMinePressed);
				break;
			case EntityEventKind.MineExploded:
				ReplayMineExplosion(position);
				break;
			case EntityEventKind.ShuttleDoorOpened:
				ReplayShuttleDoor(position, elapsedSeconds);
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
				ReplaySpike(position, elapsedSeconds);
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
				ReplayState<GeyserScript>(position, kind, TrapStateActions.ApplyGeyser);
				break;
			case EntityEventKind.SoundCannonFired:
				ReplayState<SoundCannon>(position, kind, TrapStateActions.ApplySoundCannon);
				break;
			case EntityEventKind.CaveTicksSpawned:
				ReplayState<CaveTickSpawner>(position, kind, TrapStateActions.ApplyCaveTicks);
				break;
			case EntityEventKind.CrystalFragileBroken:
				ReplayState<CrystalBehaviour>(position, kind, CrystalStateActions.ApplyCrystalFragile);
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
				ReplayState<CrystalBehaviour>(position, kind, CrystalStateActions.ApplyCrystalElectric);
				break;
			case EntityEventKind.TurretFired:
				ReplayState<TurretScript>(position, kind, TrapStateActions.ApplyTurretFired);
				break;
			case EntityEventKind.CrystalUnstableTicked:
				// The transient ticking start — replay the 5 s pre-explosion
				// visual (sound + glow ramp + jitter) WITHOUT writing the
				// crystal's timerStarted/timer latches (the local copy must not
				// count down and explode naturally — CrystalUnstableExploded
				// owns the consumption). The CrystalTickingReplay component
				// IS the duplicate guard.
				ReplayState<CrystalBehaviour>(position, kind, CrystalStateActions.ApplyCrystalUnstableTicked);
				break;
			case EntityEventKind.CrystalUnstableExploded:
				ReplayCrystalUnstableExplosion(position);
				break;
			case EntityEventKind.CrystalMetamorphicTriggered:
				ReplayState<CrystalBehaviour>(position, kind, CrystalStateActions.ApplyCrystalMetamorphic);
				break;
			case EntityEventKind.CrystalMimicTriggered:
				// Live relay: consume the latch + play the original 2D laugh.
				// Late-joiner snapshot (ElapsedSeconds > 0): latch only — an old
				// laugh must not fire over the joining player.
				ReplayState<CrystalBehaviour>(position, kind, c => CrystalStateActions.ApplyCrystalMimic(c, playSound: elapsedSeconds <= 0f));
				break;
			case EntityEventKind.CrystalShySwapped:
				ReplayState<CrystalBehaviour>(position, kind, CrystalStateActions.ApplyCrystalShy);
				break;
			case EntityEventKind.CrystalEMPActivated:
				ReplayState<CrystalBehaviour>(position, kind, CrystalStateActions.ApplyCrystalEMP);
				break;
			case EntityEventKind.CrystalTeleportTriggered:
				// The teleported body already rides the 20 Hz player stream;
				// the replay is the same 2D observerlaugh + FlashBrief call the
				// trigger side made (CrystalTeleport.cs:27-28).
				ReplayState<CrystalBehaviour>(position, kind, CrystalStateActions.ApplyCrystalTeleport);
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
			LogGoneWithNearest<T>(kind, position);
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

	/// <summary>
	/// The shuttle door's replay — an ANIMATION-DRIVEN one-shot: activated +
	/// progress accumulate 2 s (pre-warning), then the doors lerp up over the
	/// next seconds and the script destroys itself at progress > 10. A late
	/// joiner must land at the CURRENT state: the snapshot's elapsed is the
	/// anchor — progress = elapsed puts the doors exactly where the host's are
	/// (elapsed > 10 → the doors sit at the top and the script destroys itself
	/// on the first Update, exactly like the host's already-gone door).
	/// </summary>
	private void ReplayShuttleDoor(Vector2 position, float elapsedSeconds)
	{
		var door = TrapEffectApplier.FindTrap<ShuttleStartOpen>(position);
		if (door == null) // Unity object — ==
		{
			LogGoneWithNearest<ShuttleStartOpen>(EntityEventKind.ShuttleDoorOpened, position);
			return;
		}

		// Live relay (elapsed == 0): the trigger side just opened the door, so
		// this side must play the same collision-only trigger sound
		// (shuttleNotice) and let the door's own Update drive the animation and
		// the later shuttleOpen sound from the same start moment. This is the
		// path the earlier fix missed — the sound existed only in
		// TrapStateActions.ApplyShuttleDoor, which is the HOST executor, not the
		// guest replay.
		if (ShuttleDoorReplayState.ShouldReplayTriggerSound(elapsedSeconds))
		{
			if (TrapStateActions.ApplyShuttleDoor(door))
			{
				_log.LogInformation("[TrapEvent] replayed ShuttleDoorOpened at {Pos} (live).", position);
			}
			else
			{
				_log.LogWarning("[TrapEvent] ShuttleDoorOpened at {Pos} already consumed locally — duplicate dropped.", position);
			}

			return;
		}

		// Late-joiner snapshot: jump to the current elapsed point — no sounds
		// (the host's door is not re-playing its opening either).
		var state = ShuttleDoorReplayState.FromElapsed(elapsedSeconds);
		Traverse.Create(door).Field("activated").SetValue(true);
		Traverse.Create(door).Field("progress").SetValue(state.Progress);
		Traverse.Create(door).Field("playedSound").SetValue(state.PlayedSound);
		Traverse.Create(door).Field("didTalk").SetValue(state.DidTalk);

		_log.LogInformation("[TrapEvent] replayed ShuttleDoorOpened at {Pos} at elapsed {Elapsed:F1} s.", position, elapsedSeconds);
	}

	/// <summary>
	/// The spike's replay: activated + the stab animation jumped to its END
	/// (the host's spike stabbed long ago — re-running Stab() would play the
	/// sound and the animation over the late joiner's head). The stab-hit
	/// sprite (CheckStab's victim sprite) is NOT restored — the snapshot
	/// carries no hit record; the spike still reads as spent.
	/// </summary>
	private void ReplaySpike(Vector2 position, float elapsedSeconds)
	{
		var spike = TrapEffectApplier.FindTrap<SpikeStabberScript>(position);
		if (spike == null) // Unity object — ==
		{
			LogGoneWithNearest<SpikeStabberScript>(EntityEventKind.SpikeStabbed, position);
			return;
		}

		if (Traverse.Create(spike).Field("activated").GetValue<bool>())
		{
			_log.LogWarning("[TrapEvent] SpikeStabbed at {Pos} already consumed locally — duplicate dropped.", position);
			return;
		}

		Traverse.Create(spike).Field("activated").SetValue(true);
		spike.GetComponent<Animator>().Play("SpikeStab", -1, 1f); // the animation's END frame — the spent state
		spike.GetComponent<BuildingEntity>().description = Locale.GetBuilding("spikestabberdscused");
		_log.LogInformation("[TrapEvent] replayed SpikeStabbed at {Pos} at elapsed {Elapsed:F1} s.", position, elapsedSeconds);
	}

	/// <summary>
	/// A snapshot's position key found NO entity of the expected kind — the
	/// regenerated world has no such trap there (generation divergence: the
	/// trap layout is per-side random) or the entity is gone. Report the
	/// NEAREST same-kind entity's position — the divergence diagnostic (the
	/// fingerprinted blocks are identical; the entity layout is not covered by
	/// any fingerprint).
	/// </summary>
	private void LogGoneWithNearest<T>(EntityEventKind kind, Vector2 position) where T : Component
	{
		var nearest = UnityEngine.Object.FindObjectsOfType<T>()
			.Select(t => (Trap: t, Distance: Vector2.Distance(t.transform.position, position)))
			.OrderBy(x => x.Distance)
			.FirstOrDefault();
		if (nearest.Trap != null) // Unity object — ==
		{
			_log.LogWarning("[TrapEvent] {Kind} at {Pos} has no entity — nearest {Type} at ({X:F1},{Y:F1}), {Dist:F1} away (generation divergence?).",
				kind, position, typeof(T).Name, nearest.Trap.transform.position.x, nearest.Trap.transform.position.y, nearest.Distance);
		}
		else
		{
			_log.LogInformation("[TrapEvent] {Kind} at {Pos} already gone — no {Type} anywhere.", kind, position, typeof(T).Name);
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

	/// <summary>The unstable crystal exploded (on the host or another guest):
	/// replay the explosion with the crystal's own parameters — pure-visual
	/// five-piece + real-body effect + remote-death consumption. The health
	/// &lt; 0.5 check is the consumption mark (already dead = a duplicate, dropped);
	/// the 5 s pre-explosion ticking is a recorded gap (the trigger side's own
	/// experience — same as the mine's 0.8 s press visual).</summary>
	private void ReplayCrystalUnstableExplosion(Vector2 position)
	{
		var crystal = TrapEffectApplier.FindTrap<CrystalBehaviour>(position);
		if (crystal != null && crystal.build.health < 0.5f) // Unity object — ==
		{
			_log.LogWarning("[TrapEvent] unstable crystal at {Pos} already dead — duplicate dropped.", position);
			return;
		}

		var size = crystal != null ? crystal.crystalSize : 1f; // Unity object — ==
		var param = TrapEffectApplier.CrystalUnstableExplosionParams(position, size);
		ReplayExplosionVisual(param);
		ExplosionBodyEffect.ApplyToLocalBodies(param);

		if (crystal != null) // Unity object — ==
		{
			crystal.build.health = 0f;
			crystal.gameObject.AddComponent<RemoteEntityDeath>();
		}
		else
		{
			_log.LogInformation("[TrapEvent] unstable crystal at {Pos} already gone — visual only.", position);
		}

		_log.LogInformation("[TrapEvent] replayed unstable-crystal explosion at {Pos}.", position);
	}

	/// <summary>
	/// Replay a plain player-item explosion (dynamite) on the receiving side:
	/// the pure-visual five-piece plus the real-body effect segment. Used by
	/// the dynamite event (DynamiteExplosionSync); the trap replays call the
	/// same two pieces through their own kind-specific consumption checks.
	/// </summary>
	internal void ReplayExplosion(ExplosionParams param)
	{
		ReplayExplosionVisual(param);
		ExplosionBodyEffect.ApplyToLocalBodies(param);
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
