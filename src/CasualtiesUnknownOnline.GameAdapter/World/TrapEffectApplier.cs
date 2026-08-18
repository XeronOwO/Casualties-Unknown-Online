using System;
using CasualtiesUnknownOnline.Runtime.Protocol;
using HarmonyLib;
using Microsoft.Extensions.Logging;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// Host executor for entity events (a guest-triggered event must also happen
/// on the HOST's world). Explosion family: a guest-triggered explosion must
/// hurt the host's real body, form the crater and kill the host's copy of the
/// trap — find the trap, mark it exploded (so its OnDestroy cannot chain-
/// explode a second time), kill it as a REMOTE death (no drop roll — the
/// guest's side rolled and reported them), then explode with the trap's
/// literal parameters. State family: apply the transition to the host's copy
/// (e.g. the shuttle door opens), the entity's own animation drives the rest.
/// The host's own trigger never passes through here — it ran naturally, this
/// domain only applies what a remote side triggered.
/// </summary>
internal sealed class TrapEffectApplier(ILogger<TrapEffectApplier> log)
{
	private readonly ILogger<TrapEffectApplier> _log = log;

	internal void ApplyEvent(EntityEventKind kind, Vector2 position, byte extra)
	{
		switch (kind)
		{
			case EntityEventKind.MinePressed:
				ApplyState<MineScript>(position, kind, TrapStateActions.ApplyMinePressed);
				break;
			case EntityEventKind.MineExploded:
				ApplyMineExplosion(position);
				break;
			case EntityEventKind.ShuttleDoorOpened:
				ApplyState<ShuttleStartOpen>(position, kind, TrapStateActions.ApplyShuttleDoor);
				break;
			case EntityEventKind.LifepodHeatChanged:
				ApplyState<LifepodController>(position, kind, c => TrapStateActions.ApplyHeat(c, extra));
				break;
			case EntityEventKind.LifepodShowerActivated:
				ApplyState<LifepodController>(position, kind, TrapStateActions.ApplyShower);
				break;
			case EntityEventKind.BioTerminalUnlocked:
				ApplyState<BioTerminalScript>(position, kind, TrapStateActions.ApplyBioTerminal);
				break;
			case EntityEventKind.ScrapEaterProgress:
				ApplyState<ScrapEaterScript>(position, kind, e => TrapStateActions.ApplyScrapEater(e, extra));
				break;
			case EntityEventKind.MedStationHealed:
				ApplyState<MedStationScript>(position, kind, TrapStateActions.ApplyMedStation);
				break;
			case EntityEventKind.BatteryInserted:
				ApplyState<BatteryRecharger>(position, kind, TrapStateActions.ApplyBattery);
				break;
			case EntityEventKind.SpikeStabbed:
				ApplyState<SpikeStabberScript>(position, kind, TrapStateActions.ApplySpike);
				break;
			case EntityEventKind.BearTrapClamped:
				ApplyState<BearTrap>(position, kind, TrapStateActions.ApplyBearTrapClamped);
				break;
			case EntityEventKind.BearTrapReleased:
				ApplyState<BearTrap>(position, kind, TrapStateActions.ApplyBearTrapReleased);
				break;
			case EntityEventKind.StalactiteDropped:
				ApplyState<StalactiteDropper>(position, kind, TrapStateActions.ApplyStalactite);
				break;
			case EntityEventKind.GeyserActivated:
				ApplyState<GeyserScript>(position, kind, TrapStateActions.ApplyGeyser);
				break;
			case EntityEventKind.SoundCannonFired:
				ApplyState<SoundCannon>(position, kind, TrapStateActions.ApplySoundCannon);
				break;
			case EntityEventKind.CaveTicksSpawned:
				ApplyState<CaveTickSpawner>(position, kind, TrapStateActions.ApplyCaveTicks);
				break;
			case EntityEventKind.CrystalFragileBroken:
				ApplyState<CrystalBehaviour>(position, kind, TrapStateActions.ApplyCrystalFragile);
				break;
			case EntityEventKind.TurretSelfDestructed:
				ApplyTurretSelfDestructed(position);
				break;
			case EntityEventKind.BarbedFenceHit:
				ApplyState<BarbedFence>(position, kind, TrapStateActions.ApplyBarbedFence);
				break;
			case EntityEventKind.CoilShocked:
				ApplyState<CoilScript>(position, kind, TrapStateActions.ApplyCoil);
				break;
			case EntityEventKind.CactusHit:
				ApplyState<CactusScript>(position, kind, TrapStateActions.ApplyCactus);
				break;
			case EntityEventKind.JumpPadLaunched:
				ApplyState<JumpPadScript>(position, kind, TrapStateActions.ApplyJumpPad);
				break;
			case EntityEventKind.BananaPlantSlip:
				ApplyState<BananaPlantSlip>(position, kind, TrapStateActions.ApplyBananaSlip);
				break;
			case EntityEventKind.CrystalElectricShocked:
				ApplyState<CrystalBehaviour>(position, kind, TrapStateActions.ApplyCrystalElectric);
				break;
			case EntityEventKind.TurretFired:
				ApplyState<TurretScript>(position, kind, TrapStateActions.ApplyTurretFired);
				break;
			case EntityEventKind.CrystalUnstableExploded:
				ApplyCrystalUnstableExplosion(position);
				break;
			case EntityEventKind.CrystalMetamorphicTriggered:
				ApplyState<CrystalBehaviour>(position, kind, TrapStateActions.ApplyCrystalMetamorphic);
				break;
			case EntityEventKind.CrystalMimicTriggered:
				// The enemies spawned on the triggering side; the host does NOT
				// spawn from the event — the EntitySpawned reports create and
				// relay the crystalenemy copies. This only consumes the latch
				// (so the host's own copy cannot spawn a second set) and plays
				// the live laugh.
				ApplyState<CrystalBehaviour>(position, kind, c => TrapStateActions.ApplyCrystalMimic(c, playSound: true));
				break;
			case EntityEventKind.CrystalShySwapped:
				ApplyState<CrystalBehaviour>(position, kind, TrapStateActions.ApplyCrystalShy);
				break;
			case EntityEventKind.CrystalEMPActivated:
				ApplyState<CrystalBehaviour>(position, kind, TrapStateActions.ApplyCrystalEMP);
				break;
			case EntityEventKind.GrabberGrabbed:
				// The grab's visuals are the player-side ragdoll/scream (each
				// side's own body); the tendril animation is Update-driven
				// everywhere — nothing to apply, the trace line is the record.
				break;
			default:
				_log.LogWarning("[TrapEvent] no host executor for {Kind}.", kind);
				break;
		}
	}

	/// <summary>A state-family event: apply the shared action to the host's
	/// entity at the position (the transition itself; the entity animates).
	/// The action reports whether it APPLIED — a false (the host's copy already
	/// consumed the one-shot: the two-trigger race) is DROPPED with a trace.</summary>
	private void ApplyState<T>(Vector2 position, EntityEventKind kind, Func<T, bool> action) where T : Component
	{
		var entity = FindTrap<T>(position);
		if (entity == null) // Unity object — ==
		{
			_log.LogInformation("[TrapEvent] {Kind} at {Pos} already gone — effect skipped.", kind, position);
			return;
		}

		if (action(entity))
		{
			_log.LogInformation("[TrapEvent] host applied {Kind} at {Pos}.", kind, position);
		}
		else
		{
			_log.LogWarning("[TrapEvent] {Kind} at {Pos} already consumed locally — duplicate dropped.", kind, position);
		}
	}

	/// <summary>Find the trap at a world position (world entities are generated
	/// deterministically, so the position IS the identity; the 3-unit radius
	/// tolerates the cell-centre snapshot keys).</summary>
	internal static T? FindTrap<T>(Vector2 position) where T : Component
	{
		foreach (var trap in Object.FindObjectsOfType<T>())
		{
			if (Vector2.Distance(trap.transform.position, position) < 3f)
			{
				return trap;
			}
		}

		return null;
	}

	private void ApplyMineExplosion(Vector2 position)
	{
		var mine = FindTrap<MineScript>(position);
		if (mine == null) // Unity object — == (already gone — a repeat event, or it died naturally)
		{
			_log.LogInformation("[TrapEvent] mine at {Pos} already gone — effect skipped, relay only.", position);
			return;
		}

		// The consumption check: the host's copy may have already exploded (the
		// host tripped the same mine itself — its explosion ran naturally) —
		// this duplicate report is dropped, only the relay flows.
		if (Traverse.Create(mine).Field("exploded").GetValue<bool>())
		{
			_log.LogWarning("[TrapEvent] mine at {Pos} already exploded locally — duplicate dropped, relay only.", position);
			return;
		}

		// exploded = true FIRST: the game's OnDestroy explodes when the mine
		// died without exploding (MineScript.cs:16-23) — the executor's own
		// CreateExplosion below IS the explosion; a second one from OnDestroy
		// would double the blast. RemoteEntityDeath: the guest rolled and
		// reported the drops, this side only removes the entity.
		Traverse.Create(mine).Field("exploded").SetValue(true);
		mine.build.health = 0f;
		mine.gameObject.AddComponent<RemoteEntityDeath>();

		WorldGeneration.CreateExplosion(new ExplosionParams { position = position + Vector2.up }); // natural consequences: host body damage, crater (rides the SetBlock relay), building damage (rides the CreateExplosion diff)
		_log.LogInformation("[TrapEvent] host applied mine explosion at {Pos}.", position);
	}

	/// <summary>The turret self-destructed on the guest's side — repeat it on the
	/// host's world with the turret's own parameters (TurretScript.cs:89-101).
	/// The health &lt; 0.5 check is the consumption mark (already dead = a
	/// duplicate, dropped).</summary>
	private void ApplyTurretSelfDestructed(Vector2 position)
	{
		var turret = FindTrap<TurretScript>(position);
		var build = turret != null ? Traverse.Create(turret).Field("build").GetValue<BuildingEntity>() : null; // Unity object — ==
		if (build == null || build.health < 0.5f) // Unity object — ==
		{
			_log.LogInformation("[TrapEvent] turret at {Pos} already gone/dead — effect skipped.", position);
			return;
		}

		// Remote death: no drop roll — the guest's side rolled and reported them.
		build.health = 0f;
		turret!.gameObject.AddComponent<RemoteEntityDeath>();
		var collider = turret.GetComponent<Collider2D>();
		if (collider != null) // Unity object — ==
		{
			collider.enabled = false; // the game disables it before its own explosion (TurretScript.cs:99)
		}

		WorldGeneration.CreateExplosion(TurretExplosionParams(turret.transform.position));
		_log.LogInformation("[TrapEvent] host applied turret self-destruct at {Pos}.", position);
	}

	/// <summary>The turret's literal explosion parameters (TurretScript.cs:93-100) — shared with the replay side.</summary>
	internal static ExplosionParams TurretExplosionParams(Vector2 position) => new()
	{
		position = position,
		range = 9f,
		structuralDamage = 200f,
		boneBreakChance = 0f,
		dislocationChance = 0.05f,
		muscleDamage = new RangeF(0f, 35f),
		velocity = 15f,
		disfigureChance = 0.2f,
	};

	/// <summary>The unstable crystal exploded on the guest's side — repeat it on
	/// the host's world with the crystal's own parameters (CrystalUnstable.cs:
	/// 51-61). The health &lt; 0.5 check is the consumption mark (the host's copy
	/// already exploded — the two-trigger race, or the host touched it itself
	/// and its 5 s timer ran naturally; the relay still flows).</summary>
	private void ApplyCrystalUnstableExplosion(Vector2 position)
	{
		var crystal = FindTrap<CrystalBehaviour>(position);
		if (crystal == null) // Unity object — ==
		{
			_log.LogInformation("[TrapEvent] unstable crystal at {Pos} already gone — effect skipped, relay only.", position);
			return;
		}

		if (crystal.build.health < 0.5f)
		{
			_log.LogWarning("[TrapEvent] unstable crystal at {Pos} already destroyed — duplicate dropped, relay only.", position);
			return;
		}

		// Remote death: no drop roll — the guest's side rolled and reported them.
		crystal.build.health = 0f;
		crystal.gameObject.AddComponent<RemoteEntityDeath>();

		WorldGeneration.CreateExplosion(CrystalUnstableExplosionParams(position, crystal.crystalSize));
		_log.LogInformation("[TrapEvent] host applied unstable-crystal explosion at {Pos}.", position);
	}

	/// <summary>The unstable crystal's literal explosion parameters (CrystalUnstable.cs:51-61) — shared with the replay side.</summary>
	internal static ExplosionParams CrystalUnstableExplosionParams(Vector2 position, float crystalSize) => new()
	{
		position = position,
		range = 16f * Mathf.Lerp(crystalSize, 1f, 0.5f),
		structuralDamage = 1000f * crystalSize,
		boneBreakChance = 0.1f * crystalSize,
		dislocationChance = 0.05f * crystalSize,
		muscleDamage = new RangeF(0f, 30f * crystalSize),
		velocity = 20f * crystalSize,
		disfigureChance = 0.05f * crystalSize,
	};
}
