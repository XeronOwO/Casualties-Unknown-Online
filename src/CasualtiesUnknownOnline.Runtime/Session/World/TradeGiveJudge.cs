using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.GameState.Domains.Items;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The GIVE half of <c>NetMsg.TraderAction</c> judged as DATA (decision 265): the reported credit is a claim
/// about an item the giver's own client destroyed, and the host holds that item's judged row. This type
/// answers three questions with facts the receiver owns, never with the report's word:
/// <list type="number">
/// <item>may this reporter speak for the item at all — the row must exist, be live, and be the sender's own
/// (or WORLD content, since the native gesture drags a ground item onto the trader; the walk is
/// <see cref="ItemLocationChain.IsWorldRooted"/>, the same rule the item domain's admission seam reads);</item>
/// <item>what is the item worth on the host's record — the value comes from a DELEGATE, because the numbers
/// are the game's own item and liquid tables and only the Game Adapter may read those, while the
/// arithmetic is <see cref="TradeStockMachine.GiveItemValue"/>;</item>
/// <item>is the claimed credit inside that number — <see cref="TradeStockMachine.JudgeGiveItem"/>.</item>
/// </list>
///
/// <para>
/// A claim above the host's number is CLAMPED, not refused: the host's row can be a frame behind the giver's
/// live item, and the direction that matters is an UNRECORDED GAIN (a repair, a liquid refill, a magazine
/// reload) — the host's number is then LOWER than the item the giver held, and refusing would destroy a
/// legitimate hand-in over timing. The credit is the lower of the two and the caller answers the clamp with
/// the reject marker on the authoritative state every side converges on; the honest giver's only cost is a few
/// credits, never the item. Decay moves the other way (the row over-states, the claim sits below it, nothing is
/// clamped). What IS refused is a claim the host cannot anchor: an unjudged instance, another member's item, a
/// terminal row, an installed limb component, a value the host's own tables cannot compute, and a second give
/// of an instance it has already credited — the receipt outlives the window before the item's own destroy
/// report marks the row terminal, so one instance credits once.
/// </para>
/// </summary>
internal sealed class TradeGiveJudge : ISessionReset
{
	/// <summary>Instance ids whose give this host already credited. Ids are never recycled, so the set is the whole fact; it is cleared with the session.</summary>
	private readonly HashSet<ulong> _given = [];

	/// <summary>
	/// Judge one give report. <paramref name="find"/> is the host's judged-row lookup and
	/// <paramref name="valueOf"/> derives the item's worth from that row (the adapter reads the game's tables
	/// there; a test/simulation supplies the fixture). <see cref="GiveItemVerdict.Refusal"/> is set when the
	/// claim is refused; a credit of 0 WITHOUT a refusal is the host's own record making the item worth
	/// nothing, which is answered the same way and never guessed at.
	/// </summary>
	internal GiveItemVerdict Judge(
		ulong sender,
		ulong itemInstanceId,
		int reportedValue,
		Func<ulong, ItemState?> find,
		Func<ItemState, GiveItemValue> valueOf)
	{
		if (itemInstanceId == 0)
		{
			return GiveItemVerdict.Refused("the report names no item instance, so the host holds no state to judge the credit against");
		}

		if (_given.Contains(itemInstanceId))
		{
			return GiveItemVerdict.Refused($"item #{itemInstanceId} was already given to a trader in this session");
		}

		if (find(itemInstanceId) is not { } row)
		{
			return GiveItemVerdict.Refused($"the host has never judged item #{itemInstanceId}");
		}

		var location = row.Location;
		if (location.Kind == ItemLocationKind.Terminal)
		{
			return GiveItemVerdict.Refused($"item #{itemInstanceId} is already gone (a terminal row)");
		}

		if (location.Kind == ItemLocationKind.Applied)
		{
			return GiveItemVerdict.Refused($"item #{itemInstanceId} is installed on a limb, not a hand item");
		}

		// Whose item may the reporter speak for? The SENDER's own carried/contained tree, or WORLD content
		// (the native gesture drags a ground item — or one interior to a container that ultimately lies in
		// the world — onto the trader). The holder is the CHAIN's root, not the child row's own owner field:
		// the kernel stamps a contained row's owner at creation and does not re-stamp it when the container
		// changes hands or a generation-time container is picked up.
		var senderHolds = ItemLocationChain.HolderOf(row, find) == sender;
		if (!senderHolds && !ItemLocationChain.IsWorldRooted(row, find))
		{
			return GiveItemVerdict.Refused(
				$"item #{itemInstanceId} is recorded as {location.Kind} of {location.Owner.Value} and is neither the reporter's own nor world content");
		}

		var value = valueOf(row);
		if (value.Refusal is not null)
		{
			return GiveItemVerdict.Refused(value.Refusal);
		}

		var claim = TradeStockMachine.JudgeGiveItem(reportedValue, value.Value);
		var detail =
			$"{row.Identity.DefinitionId} (#{itemInstanceId}, {location.Kind}, condition {row.Data.Condition:0.###}) is worth {value.Value} on the host's record; the report claimed {reportedValue}";
		return GiveItemVerdict.Judged(claim.Credit, claim.ClaimExceeded, detail);
	}

	/// <summary>Record that this instance's give was credited — a second report for it is then refused. Called only after the credit lands, so a give refused for another reason leaves the item giveable.</summary>
	internal void RecordGiven(ulong itemInstanceId) => _given.Add(itemInstanceId);

	/// <summary>Drop the session's receipts (the next session has its own trader states and fresh item rows).</summary>
	public void ResetSessionState() => _given.Clear();
}
