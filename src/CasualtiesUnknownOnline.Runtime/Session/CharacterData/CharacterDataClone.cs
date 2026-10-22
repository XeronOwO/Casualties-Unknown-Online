using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.CharacterData;

/// <summary>
/// A deep copy of one character snapshot, in the one place every writer and consumer can share it.
///
/// <para>
/// It exists because a snapshot is a carrier several owners hold at once — the host's saved-character
/// table, the clone presentation's fact table, an interaction service's edited copy — and an object
/// one of them keeps must not be mutated by another. The field list lives HERE, once: an interaction
/// service that drops a field from its clone silently degrades the next cut, the next reconnect
/// restore and the next continue (that lesson is written into the fields below).
/// </para>
///
/// <para>
/// What is DEEP and what is SHARED is a contract, not an accident. The ITEM and LIMB trees are deep
/// (nodes, their contents, their lists): they are the placement carrier a cut writes, and the clone
/// presentation mutates them when a carried-sync moves, adds or removes an item. <c>Health</c> and
/// <c>Skills</c> are shared leaves: the host's vitals model and the clone presentation write through
/// the same objects by design, and the next 1 Hz report replaces them. The store therefore copies a
/// REPORT before storing it (<c>CharacterDataStore.SaveReportedCharacterData</c>), never the host's
/// authority writes, which are meant to change the stored snapshot.
/// </para>
/// </summary>
internal static class CharacterDataClone
{
	internal static CharacterDataMsg Of(CharacterDataMsg source) => new()
	{
		Skills = source.Skills,
		Health = source.Health,
		Limbs = [.. source.Limbs.Select(Limb)],
		Items = [.. source.Items.Select(Item)],
		HandSlot = source.HandSlot,
		OwnerSteamId = source.OwnerSteamId,
		Position = source.Position,
		SlotCount = source.SlotCount,
		// The native character fields ride along like every other field: losing them is not
		// cosmetic — the snapshot is SAVED over the stored character, so a dropped field would
		// silently degrade the next cut, the next reconnect restore and the next continue (S3.4b).
		NativeFields = source.NativeFields,
		// The local favourite marks ride along for the same reason and with the same consequence.
		FavouriteMarks = source.FavouriteMarks,
		// The capture layer and the respawn identity ride along too: a dropped layer stamp would
		// make the next restore apply a position from a layer this player is no longer in, and a
		// dropped respawn flag would turn the next-level revive back into an ordinary handback a
		// preserved body refuses.
		Generation = source.Generation,
		Respawn = source.Respawn,
	};

	internal static CharacterItemMsg Item(CharacterItemMsg item) => new()
	{
		InstanceId = item.InstanceId,
		ItemId = item.ItemId,
		Condition = item.Condition,
		SlotIndex = item.SlotIndex,
		Favourited = item.Favourited,
		Components = item.Components,
		Contents = [.. item.Contents.Select(Item)],
		Liquids = [.. item.Liquids],
	};

	internal static CharacterLimbMsg Limb(CharacterLimbMsg limb) => new()
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
		ConnectedLimbIndices = [.. limb.ConnectedLimbIndices],
		DistanceToHeart = limb.DistanceToHeart,
	};
}
