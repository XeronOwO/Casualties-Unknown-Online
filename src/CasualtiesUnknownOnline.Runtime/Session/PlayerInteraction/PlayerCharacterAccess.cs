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

	/// <summary>The deep copy lives in <see cref="CharacterDataClone"/> — the one field list every writer and consumer of a snapshot shares. Kept as a delegate here so the interaction services' call sites do not move.</summary>
	public static CharacterDataMsg CloneCharacter(CharacterDataMsg source) => CharacterDataClone.Of(source);

	public static CharacterItemMsg CloneItem(CharacterItemMsg item) => CharacterDataClone.Item(item);

	public static CharacterLimbMsg CloneLimb(CharacterLimbMsg limb) => CharacterDataClone.Limb(limb);
}
