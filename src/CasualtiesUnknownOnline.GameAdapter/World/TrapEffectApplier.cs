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
}
