using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Mapster;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// Mapster configuration for the character-data surface (character-data-plan):
/// game types (Body/Limb/Skills — public fields) ↔ CharacterDataMsg. The
/// SaveSystem-aligned field set is complete, so mapping is configure-once and
/// new fields map automatically. Field names match modulo case (bloodVolume ↔
/// BloodVolume) — NameMatchingStrategy.Flexible covers that; Skills names are
/// genuinely different (STR → Strength), mapped explicitly. Call
/// <see cref="Configure"/> once at startup before any capture/restore.
/// </summary>
internal static class CharacterDataMapper
{
	public static void Configure()
	{
		TypeAdapterConfig.GlobalSettings.Default.NameMatchingStrategy(NameMatchingStrategy.Flexible);

		TypeAdapterConfig<Skills, CharacterSkillsMsg>.NewConfig()
			.Map(d => d.Strength, s => s.STR)
			.Map(d => d.Resistance, s => s.RES)
			.Map(d => d.Intelligence, s => s.INT)
			.Map(d => d.ExpStrength, s => s.expSTR)
			.Map(d => d.ExpResistance, s => s.expRES)
			.Map(d => d.ExpIntelligence, s => s.expINT);

		TypeAdapterConfig<CharacterSkillsMsg, Skills>.NewConfig()
			.Map(d => d.STR, s => s.Strength)
			.Map(d => d.RES, s => s.Resistance)
			.Map(d => d.INT, s => s.Intelligence)
			.Map(d => d.expSTR, s => s.ExpStrength)
			.Map(d => d.expRES, s => s.ExpResistance)
			.Map(d => d.expINT, s => s.ExpIntelligence);

		// Limb → CharacterLimbMsg is explicitly configured because Mapster's
		// dynamic mapping sees the UnityEngine.Component.GetComponents<T>()
		// generic method and tries to use it as the source for the new
		// CharacterLimbMsg.Components collection, which fails to compile
		// ("Method T[] GetComponents[T]() is a generic method definition").
		// Component state is deliberately NOT mapped here: the Game Adapter
		// captures/restores dynamic limb components through
		// LimbComponentStateCodec, and Index/IsHead/IsVital are assigned by
		// the character-data capture loop after the map.
		TypeAdapterConfig<Limb, CharacterLimbMsg>.NewConfig()
			.Ignore(d => d.Components)
			.Ignore(d => d.Index)
			.Ignore(d => d.IsHead)
			.Ignore(d => d.IsVital);
	}
}
