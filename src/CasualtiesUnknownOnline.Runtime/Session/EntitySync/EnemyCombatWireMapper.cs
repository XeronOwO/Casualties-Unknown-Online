using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Entities;
using CasualtiesUnknownOnline.Protocol.Wire;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using System;

namespace CasualtiesUnknownOnline.Runtime.Session.EntitySync;

/// <summary>
/// Pure conversions between enemy-combat result payloads and the Protocol wire
/// DTOs. Kept separate from <see cref="KernelWireMapper"/> so the enemy-combat
/// mapping can grow without pushing the core mapper over the architecture line
/// gate.
/// </summary>
public static class EnemyCombatWireMapper
{
	public static WireEnemyCombat ToWire(EnemyBiteMsg msg) =>
		new()
		{
			VictimSteamId = msg.VictimSteamId,
			Limb = ToWireLimb(EnemyCombatKernelCodec.FromCharacterLimb(msg.Limb)),
			VenomTotal = msg.VenomTotal,
			Adrenaline = msg.Adrenaline,
			Happiness = msg.Happiness,
		};

	public static WireEnemyCombat ToWire(EnemyLungeMsg msg) =>
		new()
		{
			VictimSteamId = msg.VictimSteamId,
			Limb = ToWireLimb(EnemyCombatKernelCodec.FromCharacterLimb(msg.Limb)),
			Adrenaline = msg.Adrenaline,
			Stamina = msg.Stamina,
		};

	public static WireEnemyCombat ToWire(EnemyEffectMsg msg) =>
		new()
		{
			VictimSteamId = msg.VictimSteamId,
			EffectKind = (int)msg.Kind,
			HorrifiedLevel = msg.HorrifiedLevel,
			FocusedLevel = msg.FocusedLevel,
			Adrenaline = msg.Adrenaline,
			Energy = msg.Energy,
			Stamina = msg.Stamina,
			Happiness = msg.Happiness,
			Caffeinated = msg.Caffeinated,
			SepticShock = msg.SepticShock,
			Shock = msg.Shock,
			EyePanicTime = msg.EyePanicTime,
		};

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
		PlayerInteractionWireMapper.ToWireLimb(EnemyCombatKernelCodec.ToPlayerInteractionLimb(limb));

	private static EnemyCombatLimb? FromWireLimb(WirePlayerInteractionLimb? limb) =>
		limb is null ? null : EnemyCombatKernelCodec.FromPlayerInteractionLimb(PlayerInteractionWireMapper.FromWireLimb(limb));
}
