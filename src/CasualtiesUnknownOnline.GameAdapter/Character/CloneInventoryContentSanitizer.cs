using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// Pure display-domain sanitizer for clone-inventory content data. Remote clone
/// items are presentation-only display proxies: they must never carry an
/// item-domain <see cref="ItemInstanceId"/>, or the world-item lookup/application
/// paths can mistake a proxy for the owner's authoritative object and unparent it
/// into the world (the "guest's carried container contents periodically become
/// world drops on the host" family). The authoritative snapshot still carries the
/// id for matching/rendering decisions, but the renderer's restore input is
/// stripped here so the display restore never stamps a domain id onto a proxy.
/// </summary>
internal static class CloneInventoryContentSanitizer
{
	/// <summary>
	/// Returns a new recursive content tree with every <see cref="CharacterItemMsg.InstanceId"/>
	/// set to 0. The input tree is not mutated.
	/// </summary>
	internal static List<CharacterItemMsg> WithoutInstanceIds(IReadOnlyList<CharacterItemMsg> contents)
	{
		var result = new List<CharacterItemMsg>(contents.Count);
		foreach (var content in contents)
		{
			result.Add(WithoutInstanceIds(content));
		}

		return result;
	}

	internal static CharacterItemMsg WithoutInstanceIds(CharacterItemMsg item)
	{
		return new CharacterItemMsg
		{
			InstanceId = 0,
			ItemId = item.ItemId,
			Condition = item.Condition,
			SlotIndex = item.SlotIndex,
			Favourited = item.Favourited,
			Liquids = [.. item.Liquids],
			Components = [.. item.Components],
			Contents = WithoutInstanceIds(item.Contents),
		};
	}
}
