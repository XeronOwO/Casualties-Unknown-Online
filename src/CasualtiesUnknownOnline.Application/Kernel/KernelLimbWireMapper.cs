using System.Linq;
using CasualtiesUnknownOnline.GameState.Domains.Entities;
using CasualtiesUnknownOnline.GameState.Domains.Players;
using CasualtiesUnknownOnline.Protocol.Wire;

namespace CasualtiesUnknownOnline.Application.Kernel;

/// <summary>
/// The limb wire vocabulary, in one place: the player-interaction limb, the
/// enemy-combat limb and their shared wire form. The mapping is pure
/// kernel <-> wire, so it belongs to this layer rather than to the Runtime
/// mappers that also carry the legacy message set; those mappers keep their
/// legacy work and forward the limb conversion here.
/// </summary>
public static class KernelLimbWireMapper
{
	public static WirePlayerInteractionLimb ToWire(PlayerInteractionLimb limb) =>
		new()
		{
			Index = limb.Index,
			SkinHealth = limb.SkinHealth,
			MuscleHealth = limb.MuscleHealth,
			Broken = limb.Broken,
			Dislocated = limb.Dislocated,
			Splinted = limb.Splinted,
			Infected = limb.Infected,
			InfectionAmount = limb.InfectionAmount,
			BleedAmount = limb.BleedAmount,
			DisinfectionTime = limb.DisinfectionTime,
			Pain = limb.Pain,
			DislocationTimer = limb.DislocationTimer,
			BoneHealTimer = limb.BoneHealTimer,
			BlockedBleeding = limb.BlockedBleeding,
			Shrapnel = limb.Shrapnel,
			FurBloodAmount = limb.FurBloodAmount,
			BandageSlowAmount = limb.BandageSlowAmount,
			SkinHealAmount = limb.SkinHealAmount,
			Dismembered = limb.Dismembered,
			Components = [.. limb.Components.Select(KernelComponentWireMapper.ToWire)],
			IsHead = limb.IsHead,
			IsVital = limb.IsVital,
		};

	public static PlayerInteractionLimb FromWire(WirePlayerInteractionLimb limb) =>
		new()
		{
			Index = limb.Index,
			SkinHealth = limb.SkinHealth,
			MuscleHealth = limb.MuscleHealth,
			Broken = limb.Broken,
			Dislocated = limb.Dislocated,
			Splinted = limb.Splinted,
			Infected = limb.Infected,
			InfectionAmount = limb.InfectionAmount,
			BleedAmount = limb.BleedAmount,
			DisinfectionTime = limb.DisinfectionTime,
			Pain = limb.Pain,
			DislocationTimer = limb.DislocationTimer,
			BoneHealTimer = limb.BoneHealTimer,
			BlockedBleeding = limb.BlockedBleeding,
			Shrapnel = limb.Shrapnel,
			FurBloodAmount = limb.FurBloodAmount,
			BandageSlowAmount = limb.BandageSlowAmount,
			SkinHealAmount = limb.SkinHealAmount,
			Dismembered = limb.Dismembered,
			Components = [.. limb.Components.Select(KernelComponentWireMapper.FromWire)],
			IsHead = limb.IsHead,
			IsVital = limb.IsVital,
		};

	/// <summary>
	/// An enemy-combat limb in its wire form: the kernel carries two limb shapes
	/// with the same fields, and the wire form is the player-interaction one, so
	/// the enemy limb is converted through that shape rather than given a second
	/// wire spelling.
	/// </summary>
	public static WirePlayerInteractionLimb ToWire(EnemyCombatLimb limb) =>
		ToWire(ToPlayerInteractionLimb(limb));

	public static EnemyCombatLimb FromWireEnemyLimb(WirePlayerInteractionLimb limb) =>
		FromPlayerInteractionLimb(FromWire(limb));

	private static PlayerInteractionLimb ToPlayerInteractionLimb(EnemyCombatLimb limb) =>
		new()
		{
			Index = limb.Index,
			SkinHealth = limb.SkinHealth,
			MuscleHealth = limb.MuscleHealth,
			Broken = limb.Broken,
			Dislocated = limb.Dislocated,
			Splinted = limb.Splinted,
			Infected = limb.Infected,
			InfectionAmount = limb.InfectionAmount,
			BleedAmount = limb.BleedAmount,
			DisinfectionTime = limb.DisinfectionTime,
			Pain = limb.Pain,
			DislocationTimer = limb.DislocationTimer,
			BoneHealTimer = limb.BoneHealTimer,
			BlockedBleeding = limb.BlockedBleeding,
			Shrapnel = limb.Shrapnel,
			FurBloodAmount = limb.FurBloodAmount,
			BandageSlowAmount = limb.BandageSlowAmount,
			SkinHealAmount = limb.SkinHealAmount,
			Dismembered = limb.Dismembered,
			Components = [.. limb.Components],
			IsHead = limb.IsHead,
			IsVital = limb.IsVital,
		};

	private static EnemyCombatLimb FromPlayerInteractionLimb(PlayerInteractionLimb limb) =>
		new()
		{
			Index = limb.Index,
			SkinHealth = limb.SkinHealth,
			MuscleHealth = limb.MuscleHealth,
			Broken = limb.Broken,
			Dislocated = limb.Dislocated,
			Splinted = limb.Splinted,
			Infected = limb.Infected,
			InfectionAmount = limb.InfectionAmount,
			BleedAmount = limb.BleedAmount,
			DisinfectionTime = limb.DisinfectionTime,
			Pain = limb.Pain,
			DislocationTimer = limb.DislocationTimer,
			BoneHealTimer = limb.BoneHealTimer,
			BlockedBleeding = limb.BlockedBleeding,
			Shrapnel = limb.Shrapnel,
			FurBloodAmount = limb.FurBloodAmount,
			BandageSlowAmount = limb.BandageSlowAmount,
			SkinHealAmount = limb.SkinHealAmount,
			Dismembered = limb.Dismembered,
			Components = [.. limb.Components],
			IsHead = limb.IsHead,
			IsVital = limb.IsVital,
		};
}
