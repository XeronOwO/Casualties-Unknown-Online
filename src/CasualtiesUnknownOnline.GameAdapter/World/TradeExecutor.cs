using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using HarmonyLib;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// The HOST side of the trade domain: the trader-side state change of a
/// guest's interaction, executed against the host's trader (the authority) —
/// the trader twin of TrapEffectApplier. The game method is NOT re-run for a
/// guest: the acting side already ran it in full (its player-side effects —
/// exp, bitten limbs, the pushed ragdoll, the bought item landing in its own
/// inventory — are immediate), and re-running would double-roll the random
/// consumption and create the bought item into the wrong inventory. Each
/// ExecuteXxx reproduces only the trader-state lines of the game method
/// (sources cited); the random consumptions happen HERE, once, on the host's
/// stream. A purchase that no longer resolves (the stock was concurrently
/// consumed) returns false — the coordinator broadcasts the state with the
/// rejection marker so the acting side rolls its item back.
/// </summary>
internal sealed class TradeExecutor
{
	/// <summary>MeetPlayer (TraderScript.cs:107-154): the reputation re-roll — the
	/// body-state chain was computed on the acting side (deterministic) and
	/// carried; the random base and the bandage stock entry land here.</summary>
	internal void ExecuteMeetPlayer(TraderScript trader, TraderActionMsg msg)
	{
		trader.startedConvo = true;
		trader.reputation = (Random.Range(75f, 135f) + msg.ReputationOffset) * msg.ReputationScale + msg.ReputationPostOffset;
		if ((msg.PlayerFlags & TraderActionMsg.FlagHasGun) != 0)
		{
			trader.hostility += 50f;
		}

		if ((msg.PlayerFlags & TraderActionMsg.FlagBleeding) != 0)
		{
			Traverse.Create(trader).Field("freeDressing").SetValue(true);
			trader.items.Add(new TraderItem
			{
				id = "bandage",
				preference = TraderScript.TraderItemPreference.Indifferent,
				value = Item.GetItem("bandage").value,
			});
			trader.items = [.. trader.items.OrderBy(x => (int)x.preference)]; // the game's OrderBy (TraderScript.cs:151) — the list order is the UI order
		}
	}

	/// <summary>TryPurchase (TraderScript.cs:747-804): validate against the stock,
	/// then charge and remove — WITHOUT creating the item (the acting side
	/// created its copy; a second one would land in the wrong inventory).</summary>
	internal bool ExecutePurchase(TraderScript trader, string itemId)
	{
		var fields = Traverse.Create(trader);
		if (fields.Field("build").GetValue<BuildingEntity>().health < 200f)
		{
			return false;
		}

		var item = trader.items.FirstOrDefault(i => i.id == itemId);
		if (item == null)
		{
			return false;
		}

		var price = trader.ItemPrice(item);
		if (trader.valueGiven < price)
		{
			trader.reputation -= 2f; // the game's refusal penalty (TraderScript.cs:800) — the acting side already paid it locally, this keeps the authoritative value in step
			return false;
		}

		trader.valueGiven -= price;
		if (price > 0)
		{
			if (item.preference == TraderScript.TraderItemPreference.WantsTrade)
			{
				trader.reputation += 7f;
			}
			else if (item.preference == TraderScript.TraderItemPreference.Indifferent)
			{
				trader.reputation += 4f;
			}
		}

		if (fields.Field("freeAmount").GetValue<int>() > 0)
		{
			fields.Field("freeAmount").SetValue(fields.Field("freeAmount").GetValue<int>() - 1);
		}

		fields.Field("freeDressing").SetValue(false);
		trader.items.Remove(item);
		return true;
	}

	/// <summary>GiveItem (TraderScript.cs:604-639): credit the value — the item
	/// itself was destroyed on the acting side (the item domain reported it).
	/// Returns false when the credit would exceed the lifetime cap (the acting
	/// side's own cap check ran against its local value and can race a
	/// concurrent give — the overwrite restores the authoritative total).</summary>
	internal bool ExecuteGiveItem(TraderScript trader, int value)
	{
		if (value <= 0 || trader.totalValueGiven >= TraderScript.MAX_VALUE_GIVEN)
		{
			return false;
		}

		var capped = System.Math.Min(value, TraderScript.MAX_VALUE_GIVEN - trader.totalValueGiven);
		trader.valueGiven = System.Math.Min(trader.valueGiven + capped, TraderScript.MAX_VALUE_GIVEN);
		trader.totalValueGiven = System.Math.Min(trader.totalValueGiven + capped, TraderScript.MAX_VALUE_GIVEN);
		return capped > 0;
	}

	/// <summary>TryHaggle (TraderScript.cs:220-265): the reputation roll and the
	/// cannibal's bite credit — the player-side effects (exp, the bitten limb)
	/// already happened on the acting side.</summary>
	internal void ExecuteHaggle(TraderScript trader)
	{
		if (trader.character != 2)
		{
			trader.haggleAmount += 1f;
			trader.reputation += Random.Range(-25f, 25f) / trader.haggleAmount;
			return;
		}

		if (trader.totalValueGiven < TraderScript.MAX_VALUE_GIVEN)
		{
			trader.reputation += Random.Range(20f, 28f);
			var bite = Random.Range(6, 11);
			trader.valueGiven = System.Math.Min(trader.valueGiven + bite, TraderScript.MAX_VALUE_GIVEN);
			trader.totalValueGiven = System.Math.Min(trader.totalValueGiven + bite, TraderScript.MAX_VALUE_GIVEN);
		}
	}

	/// <summary>Threaten (TraderScript.cs:517-545): reputation cut, then the
	/// outcome roll — the free items or the hostility (the acting player's
	/// held gun lerps the roll toward success).</summary>
	internal void ExecuteThreaten(TraderScript trader, bool hasGun)
	{
		trader.reputation *= 0.3f;
		var num = Random.value;
		if (hasGun)
		{
			num = Mathf.Lerp(num, 1f, 0.25f);
		}

		if (trader.character == 2)
		{
			num *= 0.5f;
		}

		if (num > 0.6f && trader.reputation > 30f)
		{
			var fields = Traverse.Create(trader);
			fields.Field("freeAmount").SetValue(fields.Field("freeAmount").GetValue<int>() + Random.Range(2, 4));
		}
		else if (num <= 0.3f)
		{
			trader.hostility = 100f;
		}
	}

	/// <summary>TryHug (TraderScript.cs:448-481): the reputation gate and the
	/// one-shot hug effects (the pushed ragdoll already happened on the acting
	/// side).</summary>
	internal void ExecuteHug(TraderScript trader, bool dirty)
	{
		var fields = Traverse.Create(trader);
		var didHug = fields.Field("didHug").GetValue<bool>();
		if (trader.reputation < trader.minHugReputation || dirty)
		{
			if (!didHug)
			{
				trader.reputation -= 8f;
				fields.Field("didHug").SetValue(true);
			}
		}
		else if (!didHug && trader.character != 2)
		{
			trader.reputation += 5f;
			fields.Field("didHug").SetValue(true);
		}

		if (trader.reputation < 30f)
		{
			trader.hostility = 100f;
		}
	}

	/// <summary>AskToMove (TraderScript.cs:89-104): the reputation gate and the
	/// move flag — the destination is deterministic (the move range's midpoint,
	/// TraderScript.cs:99-103), so every side's trader walks to the same spot.</summary>
	internal void ExecuteMoveTo(TraderScript trader)
	{
		if (trader.reputation < 70f)
		{
			trader.reputation -= 3f;
			return;
		}

		trader.reputation -= 1f;
		trader.didMove = true;
		Traverse.Create(trader).Field("desiredPos").SetValue(new Vector2((trader.MoveRange.min + trader.MoveRange.max) * 0.5f, trader.transform.position.y));
	}
}
