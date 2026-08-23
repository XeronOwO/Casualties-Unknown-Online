using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The bounded character-data access used by the direct player-interaction
/// domain services. It owns no state: it is a thin projection over the session
/// membership and the character-data control surfaces, keeping the SteamId
/// local/remote branching in one place.
/// </summary>
internal sealed class PlayerCharacterAccess(ISessionControl session, ICharacterDataControl characters)
{
	private readonly ISessionControl _session = session;
	private readonly ICharacterDataControl _characters = characters;

	public CharacterDataMsg? GetCharacterData(ulong steamId) =>
		steamId == _session.LocalSteamId
			? _characters.GetHostCharacterData()
			: _characters.GetSavedCharacter(steamId);

	public void SaveCharacterData(ulong steamId, CharacterDataMsg data)
	{
		if (steamId == _session.LocalSteamId)
		{
			_characters.SaveHostCharacterData(data);
		}
		else
		{
			_characters.SaveCharacterData(steamId, data);
		}
	}

	public bool IsInWorld(ulong steamId) =>
		steamId == _session.LocalSteamId
			? _session.LocalInWorld
			: _session.TryGetMember(steamId, out var member) && member.InWorld;

	/// <summary>
	/// The first unoccupied backpack/hand slot in a character snapshot. SlotCount
	/// is carried by v26 snapshots; a 0 from an older peer falls back to the
	/// game's known minimum slot count (3) rather than refusing every transfer.
	/// </summary>
	public static int FirstEmptySlot(CharacterDataMsg data)
	{
		var count = data.SlotCount > 0 ? data.SlotCount : 3;
		var occupied = data.Items.Where(i => i.SlotIndex >= 0).Select(i => i.SlotIndex).ToHashSet();
		for (var slot = 0; slot < count; slot++)
		{
			if (!occupied.Contains(slot))
			{
				return slot;
			}
		}

		return -1;
	}

	public static CharacterDataMsg CloneCharacter(CharacterDataMsg source) => new()
	{
		Skills = source.Skills,
		Health = source.Health,
		Limbs = source.Limbs,
		Items = [.. source.Items],
		HandSlot = source.HandSlot,
		OwnerSteamId = source.OwnerSteamId,
		Position = source.Position,
	};

	public static CharacterItemMsg CloneItem(CharacterItemMsg item) => new()
	{
		InstanceId = item.InstanceId,
		ItemId = item.ItemId,
		Condition = item.Condition,
		SlotIndex = item.SlotIndex,
		Favourited = item.Favourited,
		Components = item.Components,
		Contents = item.Contents,
		Liquids = item.Liquids,
	};

	public static CharacterLimbMsg CloneLimb(CharacterLimbMsg limb) => new()
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
	};
}
