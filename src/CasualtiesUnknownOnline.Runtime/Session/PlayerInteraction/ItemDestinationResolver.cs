using System.Collections.Generic;
using CasualtiesUnknownOnline.GameState.Domains.Players;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// Where a custody transfer's item lands in the RECEIVER's inventory: the slot the native release named, the first free one when that is taken, the candidate
/// order of the ladder's container rungs, and the by-id lookup a container hand-over names. The rules are the receiving client's own answers, read against its
/// snapshot; nothing here decides authority or chooses which rung to try.
/// </summary>
/// <remarks>
/// Its own type because these rules change for reasons the take ladder does not (a slot rule, an ordering, a substitution policy), and because the give path and
/// the take path must agree about them: both callers ask the same resolver instead of each carrying a copy.
/// </remarks>
internal sealed class ItemDestinationResolver(ILogger log)
{
	/// <summary>The destination slot of a custody transfer: the slot the native release named when the recipient's own report shows it free, otherwise the first free slot, with the substitution logged — the item must never be lost to an unusable slot.</summary>
	internal int ResolveDestinationSlot(CharacterDataMsg target, ulong recipient, int requestedSlot, string logTag = "[Take]")
	{
		var count = target.SlotCount > 0 ? target.SlotCount : 3;
		var occupied = false;
		foreach (var item in target.Items)
		{
			if (item.SlotIndex == requestedSlot)
			{
				occupied = true;
				break;
			}
		}

		if (requestedSlot >= 0 && requestedSlot < count && !occupied)
		{
			return requestedSlot;
		}

		var fallback = PlayerCharacterAccess.FirstEmptySlot(target);
		if (requestedSlot >= 0 && fallback >= 0)
		{
			log.LogInformation(
				"{Tag} destination slot {Slot} is not free for {To}; using slot {Fallback} instead.",
				logTag,
				requestedSlot,
				recipient,
				fallback);
		}

		return fallback;
	}

	/// <summary>
	/// The slot the receiving client ANSWERED, or -1 when it is no longer free. The give path never substitutes on its own: the host records the receiver's
	/// resolution, and a destination that changed between the answer and the commit is refused before any custody moves, so the item stays with the giver.
	/// </summary>
	internal static int ExactDestinationSlot(CharacterDataMsg target, int answeredSlot)
	{
		var count = target.SlotCount > 0 ? target.SlotCount : 3;
		if (answeredSlot < 0 || answeredSlot >= count)
		{
			return -1;
		}

		foreach (var item in target.Items)
		{
			if (item.SlotIndex == answeredSlot)
			{
				return -1;
			}
		}

		return answeredSlot;
	}

	/// <summary>
	/// The ladder's candidates in rung order: the body's carried containers first (a slot of their own), then the worn ones. The adapter filters them to
	/// containers that can hold the item; the order is the ladder's, so the first fit is the rung the answer names.
	/// </summary>
	internal static List<CharacterItemMsg> ContainerCandidates(CharacterDataMsg target)
	{
		var candidates = new List<CharacterItemMsg>(target.Items.Count);
		foreach (var item in target.Items)
		{
			if (item.SlotIndex >= 0)
			{
				candidates.Add(item);
			}
		}

		foreach (var item in target.Items)
		{
			if (item.SlotIndex < 0)
			{
				candidates.Add(item);
			}
		}

		return candidates;
	}

	/// <summary>
	/// One item in a character snapshot, found by instance id anywhere in the tree — the destination of a container hand-over is named by id alone, and whether
	/// that item really is a container is the RECEIVING client's native decision, not this snapshot's.
	/// </summary>
	internal static CharacterItemMsg? FindCarriedItem(List<CharacterItemMsg> items, ulong instanceId)
	{
		foreach (var item in items)
		{
			if (item.InstanceId == instanceId)
			{
				return item;
			}

			if (FindCarriedItem(item.Contents, instanceId) is { } nested)
			{
				return nested;
			}
		}

		return null;
	}

	/// <summary>One line naming a destination, for the log: a slot, or the container's own parent slot.</summary>
	internal static string Describe(PlayerInventoryDestination destination, int slot, ulong containerId) =>
		destination == PlayerInventoryDestination.Container
			? $"inside container {containerId} (parent slot {slot})"
			: $"in slot {slot}";
}
