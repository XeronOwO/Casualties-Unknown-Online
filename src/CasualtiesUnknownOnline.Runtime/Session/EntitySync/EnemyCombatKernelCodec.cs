using System.Linq;
using CasualtiesUnknownOnline.GameState.Domains.Entities;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.GameState.Domains.Players;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.EntitySync;

/// <summary>
/// Pure conversions between kernel-shaped enemy-combat result payloads and the
/// Runtime presentation messages consumed by the Game Adapter. The kernel never
/// references these Runtime DTOs; this codec is the projection boundary.
/// </summary>
public static class EnemyCombatKernelCodec
{
	public static EnemyCombatLimb FromCharacterLimb(CharacterLimbMsg limb) =>
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
			Components = [.. limb.Components.Select(ToKernelComponent)],
			IsHead = limb.IsHead,
			IsVital = limb.IsVital,
		};

	public static CharacterLimbMsg ToCharacterLimb(EnemyCombatLimb limb) =>
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
			Components = [.. limb.Components.Select(ToComponentMessage)],
			IsHead = limb.IsHead,
			IsVital = limb.IsVital,
		};

	public static PlayerInteractionLimb ToPlayerInteractionLimb(EnemyCombatLimb limb) =>
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

	public static EnemyCombatLimb FromPlayerInteractionLimb(PlayerInteractionLimb limb) =>
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

	public static EnemyBiteMsg ToBiteMessage(EnemyBiteResultEvent e) =>
		new()
		{
			VictimSteamId = e.VictimSteamId,
			Limb = ToCharacterLimb(e.Limb),
			VenomTotal = e.VenomTotal,
			Adrenaline = e.Adrenaline,
			Happiness = e.Happiness,
		};

	public static EnemyLungeMsg ToLungeMessage(EnemyLungeResultEvent e) =>
		new()
		{
			VictimSteamId = e.VictimSteamId,
			Limb = ToCharacterLimb(e.Limb),
			Adrenaline = e.Adrenaline,
			Stamina = e.Stamina,
		};

	public static EnemyEffectMsg ToEffectMessage(EnemyEffectResultEvent e) =>
		new()
		{
			VictimSteamId = e.VictimSteamId,
			Kind = ToRuntimeEffectKind(e.Kind),
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

	public static EnemyEffectKind ToRuntimeEffectKind(EnemyCombatEffectKind kind) =>
		kind switch
		{
			EnemyCombatEffectKind.ElderHorrorTick => EnemyEffectKind.ElderHorrorTick,
			EnemyCombatEffectKind.ElderHorrorDefeat => EnemyEffectKind.ElderHorrorDefeat,
			EnemyCombatEffectKind.XalorisSepticTick => EnemyEffectKind.XalorisSepticTick,
			EnemyCombatEffectKind.GrabberGrabbed => EnemyEffectKind.GrabberGrabbed,
			_ => throw new System.ArgumentOutOfRangeException(nameof(kind), kind, "unknown enemy combat effect kind"),
		};

	public static EnemyCombatEffectKind FromRuntimeEffectKind(EnemyEffectKind kind) =>
		kind switch
		{
			EnemyEffectKind.ElderHorrorTick => EnemyCombatEffectKind.ElderHorrorTick,
			EnemyEffectKind.ElderHorrorDefeat => EnemyCombatEffectKind.ElderHorrorDefeat,
			EnemyEffectKind.XalorisSepticTick => EnemyCombatEffectKind.XalorisSepticTick,
			EnemyEffectKind.GrabberGrabbed => EnemyCombatEffectKind.GrabberGrabbed,
			_ => throw new System.ArgumentOutOfRangeException(nameof(kind), kind, "unknown enemy combat effect kind"),
		};

	private static ItemComponentState ToKernelComponent(ComponentStateMsg component) =>
		new(
			component.TypeName,
			[.. component.Fields.Select(f => new ItemComponentField(
				f.Name,
				(ItemComponentFieldKind)f.Kind,
				f.FloatValue,
				f.IntValue,
				f.BoolValue,
				f.StringValue,
				f.StringList))]);

	private static ComponentStateMsg ToComponentMessage(ItemComponentState component) =>
		new()
		{
			TypeName = component.TypeName,
			Fields = [.. component.Fields.Select(f => new ComponentFieldMsg
			{
				Name = f.Name,
				Kind = (int)f.Kind,
				FloatValue = f.FloatValue,
				IntValue = f.IntValue,
				BoolValue = f.BoolValue,
				StringValue = f.StringValue,
				StringList = [.. f.StringList],
			})],
		};
}
