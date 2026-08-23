using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.World;
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
/// consumption and create the bought item into the wrong inventory. The state
/// transitions themselves live in the pure <see cref="TradeStockMachine"/>
/// (Runtime, tested); this class maps TraderScript ↔ the stock DTO, draws the
/// random values and applies the result — a thin shell.
/// </summary>
internal sealed class TradeExecutor
{
	/// <summary>MeetPlayer (TraderScript.cs:107-154): the reputation re-roll — the
	/// body-state chain was computed on the acting side (deterministic) and
	/// carried; the random base and the bandage stock entry land here.</summary>
	internal void ExecuteMeetPlayer(TraderScript trader, TraderActionMsg msg)
	{
		var state = Read(trader);
		var bandageValue = Item.GetItem("bandage").value; // read once — used only by the bleeding branch
		Write(trader, TradeStockMachine.MeetPlayer(state,
			Random.Range(75f, 135f),
			msg.ReputationOffset, msg.ReputationScale, msg.ReputationPostOffset,
			msg.PlayerFlags,
			bandageValue));
	}

	/// <summary>TryPurchase (TraderScript.cs:747-804): validate against the stock,
	/// then charge and remove — WITHOUT creating the item (the acting side
	/// created its copy; a second one would land in the wrong inventory).</summary>
	internal bool ExecutePurchase(TraderScript trader, string itemId)
	{
		var state = Read(trader);
		var item = trader.items.FirstOrDefault(i => i.id == itemId); // the game's own entry — ItemPrice reads its preference/info
		if (item == null)
		{
			return false;
		}

		var price = trader.ItemPrice(item);
		var (accepted, result) = TradeStockMachine.TryPurchase(state, itemId, price);
		Write(trader, result);
		return accepted;
	}

	/// <summary>GiveItem (TraderScript.cs:604-639): credit the value — the item
	/// itself was destroyed on the acting side (the item domain reported it).
	/// Returns false when the credit would exceed the lifetime cap (the acting
	/// side's own cap check ran against its local value and can race a
	/// concurrent give — the overwrite restores the authoritative total).</summary>
	internal bool ExecuteGiveItem(TraderScript trader, int value)
	{
		var state = Read(trader);
		var (accepted, result) = TradeStockMachine.TryGiveItem(state, value);
		Write(trader, result);
		return accepted;
	}

	/// <summary>TryHaggle (TraderScript.cs:220-265): the reputation roll and the
	/// cannibal's bite credit — the player-side effects (exp, the bitten limb)
	/// already happened on the acting side.</summary>
	internal void ExecuteHaggle(TraderScript trader)
	{
		var state = Read(trader);
		Write(trader, TradeStockMachine.Haggle(state,
			Random.Range(-25f, 25f),
			Random.Range(20f, 28f),
			Random.Range(6, 11)));
	}

	/// <summary>Threaten (TraderScript.cs:517-545): reputation cut, then the
	/// outcome roll — the free items or the hostility (the acting player's
	/// held gun lerps the roll toward success).</summary>
	internal void ExecuteThreaten(TraderScript trader, bool hasGun)
	{
		var state = Read(trader);
		Write(trader, TradeStockMachine.Threaten(state, hasGun, Random.value, Random.Range(2, 4)));
	}

	/// <summary>TryHug (TraderScript.cs:448-481): the reputation gate and the
	/// one-shot hug effects (the pushed ragdoll already happened on the acting
	/// side).</summary>
	internal void ExecuteHug(TraderScript trader, bool dirty)
	{
		var state = Read(trader);
		Write(trader, TradeStockMachine.Hug(state, dirty));
	}

	/// <summary>AskToMove (TraderScript.cs:89-104): the reputation gate and the
	/// move flag — the destination is deterministic (the move range's midpoint,
	/// TraderScript.cs:99-103), computed here against the trader's own range.</summary>
	internal void ExecuteMoveTo(TraderScript trader)
	{
		var state = Read(trader);
		var result = TradeStockMachine.MoveTo(state);
		Write(trader, result);
		if (result.DidMove)
		{
			Traverse.Create(trader).Field("desiredPos").SetValue(new Vector2((trader.MoveRange.min + trader.MoveRange.max) * 0.5f, trader.transform.position.y));
		}
	}

	internal static TradeStockState Read(TraderScript trader)
	{
		var fields = Traverse.Create(trader);
		return new TradeStockState
		{
			Reputation = trader.reputation,
			Hostility = trader.hostility,
			ValueGiven = trader.valueGiven,
			TotalValueGiven = trader.totalValueGiven,
			FreeAmount = fields.Field("freeAmount").GetValue<int>(),
			FreeDressing = fields.Field("freeDressing").GetValue<bool>(),
			DidHug = fields.Field("didHug").GetValue<bool>(),
			StartedConvo = trader.startedConvo,
			DidMove = trader.didMove,
			HaggleAmount = trader.haggleAmount,
			Character = trader.character,
			BuildHealth = fields.Field("build").GetValue<BuildingEntity>().health,
			MinHugReputation = trader.minHugReputation,
			Items = [.. trader.items.Select(i => new TraderItemMsg
			{
				Id = i.id,
				Value = i.value,
				Preference = (byte)i.preference,
				Bought = i.bought,
			})],
		};
	}

	private static void Write(TraderScript trader, TradeStockState state)
	{
		var fields = Traverse.Create(trader);
		trader.reputation = state.Reputation;
		trader.hostility = state.Hostility;
		trader.valueGiven = (int)state.ValueGiven;
		trader.totalValueGiven = (int)state.TotalValueGiven;
		fields.Field("freeAmount").SetValue(state.FreeAmount);
		fields.Field("freeDressing").SetValue(state.FreeDressing);
		fields.Field("didHug").SetValue(state.DidHug);
		trader.startedConvo = state.StartedConvo;
		trader.didMove = state.DidMove;
		trader.haggleAmount = state.HaggleAmount;
		trader.items = [.. state.Items.Select(i => new TraderItem
		{
			id = i.Id,
			value = i.Value,
			preference = (TraderScript.TraderItemPreference)i.Preference,
			bought = i.Bought,
		})];
	}
}
