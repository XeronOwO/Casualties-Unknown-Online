using System;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Entities;
using CasualtiesUnknownOnline.Protocol.Wire;

namespace CasualtiesUnknownOnline.Application.Kernel;

/// <summary>
/// Pure conversions between the enemy-combat kernel facts and the Protocol wire
/// DTOs: the results the kernel journals and the commands a peer submits. The
/// legacy protobuf messages a version adapter and the Game Adapter still speak
/// are NOT part of this vocabulary — those conversions stay in the Runtime, on
/// the far side of the legacy message set.
/// </summary>
public static class KernelEnemyCombatWireMapper
{
	public static WireEnemyCombat ToWire(EnemyBiteResultEvent e) =>
		new()
		{
			VictimSteamId = e.VictimSteamId,
			Limb = ToWireLimb(e.Limb),
			VenomTotal = e.VenomTotal,
			Adrenaline = e.Adrenaline,
			Happiness = e.Happiness,
		};

	public static WireEnemyCombat ToWire(EnemyLungeResultEvent e) =>
		new()
		{
			VictimSteamId = e.VictimSteamId,
			Limb = ToWireLimb(e.Limb),
			Adrenaline = e.Adrenaline,
			Stamina = e.Stamina,
		};

	public static WireEnemyCombat ToWire(EnemyEffectResultEvent e) =>
		new()
		{
			VictimSteamId = e.VictimSteamId,
			EffectKind = (int)e.Kind,
			HorrifiedLevel = e.HorrifiedLevel,
			FocusedLevel = e.FocusedLevel,
			Adrenaline = e.Adrenaline,
			Energy = e.Energy,
			Stamina = e.Stamina,
			Happiness = e.Happiness,
			Caffeinated = e.Caffeinated,
			SepticShock = e.SepticShock,
			Shock = e.Shock,
			EyePanicTime = e.EyePanicTime,
		};

	public static EnemyBiteResultEvent FromWireBiteResult(WireEnemyCombat wire) =>
		new(
			wire.VictimSteamId,
			FromWireLimb(wire.Limb) ?? throw new InvalidOperationException("enemy bite result lacks limb payload"),
			wire.VenomTotal,
			wire.Adrenaline,
			wire.Happiness);

	public static EnemyLungeResultEvent FromWireLungeResult(WireEnemyCombat wire) =>
		new(
			wire.VictimSteamId,
			FromWireLimb(wire.Limb) ?? throw new InvalidOperationException("enemy lunge result lacks limb payload"),
			wire.Adrenaline,
			wire.Stamina);

	public static EnemyEffectResultEvent FromWireEffectResult(WireEnemyCombat wire) =>
		new(
			wire.VictimSteamId,
			(EnemyCombatEffectKind)wire.EffectKind,
			wire.HorrifiedLevel,
			wire.FocusedLevel,
			wire.Adrenaline,
			wire.Energy,
			wire.Stamina,
			wire.Happiness,
			wire.Caffeinated,
			wire.SepticShock,
			wire.Shock,
			wire.EyePanicTime);

	public static RecordEnemyBiteCommand FromWireBiteCommand(
		WireEnemyCombat wire,
		OperationId operation,
		ActorId actor,
		RunEpoch epoch,
		AuthorityKind authority) =>
		new(
			operation,
			actor,
			epoch,
			authority,
			wire.VictimSteamId,
			FromWireLimb(wire.Limb) ?? throw new InvalidOperationException("enemy bite command lacks limb payload"),
			wire.VenomTotal,
			wire.Adrenaline,
			wire.Happiness);

	public static RecordEnemyLungeCommand FromWireLungeCommand(
		WireEnemyCombat wire,
		OperationId operation,
		ActorId actor,
		RunEpoch epoch,
		AuthorityKind authority) =>
		new(
			operation,
			actor,
			epoch,
			authority,
			wire.VictimSteamId,
			FromWireLimb(wire.Limb) ?? throw new InvalidOperationException("enemy lunge command lacks limb payload"),
			wire.Adrenaline,
			wire.Stamina);

	public static RecordEnemyEffectCommand FromWireEffectCommand(
		WireEnemyCombat wire,
		OperationId operation,
		ActorId actor,
		RunEpoch epoch,
		AuthorityKind authority) =>
		new(
			operation,
			actor,
			epoch,
			authority,
			wire.VictimSteamId,
			(EnemyCombatEffectKind)wire.EffectKind,
			wire.HorrifiedLevel,
			wire.FocusedLevel,
			wire.Adrenaline,
			wire.Energy,
			wire.Stamina,
			wire.Happiness,
			wire.Caffeinated,
			wire.SepticShock,
			wire.Shock,
			wire.EyePanicTime);

	private static WirePlayerInteractionLimb ToWireLimb(EnemyCombatLimb limb) =>
		KernelLimbWireMapper.ToWire(limb);

	private static EnemyCombatLimb? FromWireLimb(WirePlayerInteractionLimb? limb) =>
		limb is null ? null : KernelLimbWireMapper.FromWireEnemyLimb(limb);
}
