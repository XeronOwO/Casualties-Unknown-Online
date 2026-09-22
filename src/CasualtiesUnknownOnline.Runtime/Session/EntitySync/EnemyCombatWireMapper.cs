using CasualtiesUnknownOnline.Application.Kernel;
using CasualtiesUnknownOnline.Protocol.Wire;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.EntitySync;

/// <summary>
/// The legacy protobuf half of the enemy-combat mapping: the messages a version
/// adapter and the Game Adapter still speak, projected onto the kernel's wire
/// DTO. The kernel facts themselves map through
/// <see cref="KernelEnemyCombatWireMapper"/>, which owns the pure conversions.
/// </summary>
public static class EnemyCombatWireMapper
{
	public static WireEnemyCombat ToWire(EnemyBiteMsg msg) =>
		new()
		{
			VictimSteamId = msg.VictimSteamId,
			Limb = KernelLimbWireMapper.ToWire(EnemyCombatKernelCodec.FromCharacterLimb(msg.Limb)),
			VenomTotal = msg.VenomTotal,
			Adrenaline = msg.Adrenaline,
			Happiness = msg.Happiness,
		};

	public static WireEnemyCombat ToWire(EnemyLungeMsg msg) =>
		new()
		{
			VictimSteamId = msg.VictimSteamId,
			Limb = KernelLimbWireMapper.ToWire(EnemyCombatKernelCodec.FromCharacterLimb(msg.Limb)),
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
}
