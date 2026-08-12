using System;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The trade actions' state transitions (PURE — no Unity, every random is an
/// explicit input): the host executes the trader-side change of a guest's
/// interaction by reproducing only the trader-state lines of the game method
/// (TraderScript.cs — each cited below); the player-side effects (exp, bitten
/// limbs, the pushed ragdoll, the bought item landing in the acting side's
/// inventory) already happened on the acting side and are NOT replayed. The
/// GameAdapter's TradeExecutor maps TraderScript ↔ <see cref="TradeStockState"/>,
/// draws the random values and feeds them in — this machine is what the tests
/// lock.
/// </summary>
internal static class TradeStockMachine
{
	/// <summary>TraderScript.cs:825 — the lifetime credit cap.</summary>
	internal const int MaxValueGiven = 60;

	/// <summary>MeetPlayer (TraderScript.cs:107-154): the reputation re-roll — the
	/// body-state chain (offset/scale/post) was computed on the acting side
	/// (deterministic) and carried; the random base and the bandage stock entry
	/// land here.</summary>
	internal static TradeStockState MeetPlayer(TradeStockState s, float repRoll, float repOffset, float repScale, float repPostOffset, byte flags, int bandageValue)
	{
		var state = s with
		{
			StartedConvo = true,
			Reputation = (repRoll + repOffset) * repScale + repPostOffset,
		};
		if ((flags & TraderActionMsg.FlagHasGun) != 0)
		{
			state = state with { Hostility = state.Hostility + 50f };
		}

		if ((flags & TraderActionMsg.FlagBleeding) != 0)
		{
			var items = state.Items.Append(new TraderItemMsg
			{
				Id = "bandage",
				Preference = (byte)PreferenceIndifferent,
				Value = bandageValue,
			}).ToList();
			state = state with { FreeDressing = true, Items = [.. items.OrderBy(x => x.Preference)] }; // the game's OrderBy (TraderScript.cs:151) — the list order is the UI order
		}

		return state;
	}

	/// <summary>TryPurchase (TraderScript.cs:747-804): validate against the stock,
	/// then charge and remove — WITHOUT creating the item (the acting side
	/// created its copy; a second one would land in the wrong inventory).</summary>
	internal static (bool Accepted, TradeStockState State) TryPurchase(TradeStockState s, string itemId, int price)
	{
		if (s.BuildHealth < 200f)
		{
			return (false, s);
		}

		if (!s.Items.Any(i => i.Id == itemId))
		{
			return (false, s);
		}

		if (s.ValueGiven < price)
		{
			return (false, s with { Reputation = s.Reputation - 2f }); // the game's refusal penalty (TraderScript.cs:800) — the acting side already paid it locally, this keeps the authoritative value in step
		}

		var state = s with { ValueGiven = s.ValueGiven - price, FreeDressing = false };
		if (price > 0)
		{
			if (state.Items.Any(i => i.Id == itemId && i.Preference == PreferenceWantsTrade))
			{
				state = state with { Reputation = state.Reputation + 7f };
			}
			else if (state.Items.Any(i => i.Id == itemId && i.Preference == PreferenceIndifferent))
			{
				state = state with { Reputation = state.Reputation + 4f };
			}
		}

		if (state.FreeAmount > 0)
		{
			state = state with { FreeAmount = state.FreeAmount - 1 };
		}

		// Remove ONE entry — the game removes the sold TraderItem by reference
		// (TraderScript.cs:791), a duplicate listing stays for a second sale.
		var remaining = state.Items.ToList();
		remaining.RemoveAt(remaining.FindIndex(i => i.Id == itemId));
		return (true, state with { Items = remaining });
	}

	/// <summary>GiveItem (TraderScript.cs:604-639): credit the value — the item
	/// itself was destroyed on the acting side (the item domain reported it).
	/// False when the credit would exceed the lifetime cap (the acting side's own
	/// cap check ran against its local value and can race a concurrent give —
	/// the overwrite restores the authoritative total).</summary>
	internal static (bool Accepted, TradeStockState State) TryGiveItem(TradeStockState s, int value)
	{
		if (value <= 0 || s.TotalValueGiven >= MaxValueGiven)
		{
			return (false, s);
		}

		var capped = Math.Min(value, MaxValueGiven - s.TotalValueGiven);
		return (capped > 0, s with
		{
			ValueGiven = Math.Min(s.ValueGiven + capped, MaxValueGiven),
			TotalValueGiven = Math.Min(s.TotalValueGiven + capped, MaxValueGiven),
		});
	}

	/// <summary>TryHaggle (TraderScript.cs:220-265): the reputation roll and the
	/// cannibal's bite credit — the player-side effects (exp, the bitten limb)
	/// already happened on the acting side.</summary>
	internal static TradeStockState Haggle(TradeStockState s, float repRoll, float repRoll2, int biteRoll)
	{
		if (s.Character != 2)
		{
			return s with
			{
				HaggleAmount = s.HaggleAmount + 1f,
				Reputation = s.Reputation + repRoll / (s.HaggleAmount + 1f),
			};
		}

		if (s.TotalValueGiven >= MaxValueGiven)
		{
			return s;
		}

		return s with
		{
			Reputation = s.Reputation + repRoll2,
			ValueGiven = Math.Min(s.ValueGiven + biteRoll, MaxValueGiven),
			TotalValueGiven = Math.Min(s.TotalValueGiven + biteRoll, MaxValueGiven),
		};
	}

	/// <summary>Threaten (TraderScript.cs:517-545): reputation cut, then the
	/// outcome roll — the free items or the hostility (the acting player's held
	/// gun lerps the roll toward success).</summary>
	internal static TradeStockState Threaten(TradeStockState s, bool hasGun, float outcomeRoll, int freeRoll)
	{
		var state = s with { Reputation = s.Reputation * 0.3f };
		var num = hasGun ? outcomeRoll + (1f - outcomeRoll) * 0.25f : outcomeRoll; // Mathf.Lerp(num, 1f, 0.25f)
		if (state.Character == 2)
		{
			num *= 0.5f;
		}

		if (num > 0.6f && state.Reputation > 30f)
		{
			return state with { FreeAmount = state.FreeAmount + freeRoll };
		}

		if (num <= 0.3f)
		{
			return state with { Hostility = 100f };
		}

		return state;
	}

	/// <summary>TryHug (TraderScript.cs:448-481): the reputation gate and the
	/// one-shot hug effects (the pushed ragdoll already happened on the acting
	/// side).</summary>
	internal static TradeStockState Hug(TradeStockState s, bool dirty)
	{
		var state = s;
		if (state.Reputation < state.MinHugReputation || dirty)
		{
			if (!state.DidHug)
			{
				state = state with { Reputation = state.Reputation - 8f, DidHug = true };
			}
		}
		else if (!state.DidHug && state.Character != 2)
		{
			state = state with { Reputation = state.Reputation + 5f, DidHug = true };
		}

		return state.Reputation < 30f ? state with { Hostility = 100f } : state;
	}

	/// <summary>AskToMove (TraderScript.cs:89-104): the reputation gate and the
	/// move flag — the destination itself is deterministic (the move range's
	/// midpoint, TraderScript.cs:99-103) and computed by the adapter against the
	/// trader's range; this is the gate + the flag.</summary>
	internal static TradeStockState MoveTo(TradeStockState s)
	{
		if (s.Reputation < 70f)
		{
			return s with { Reputation = s.Reputation - 3f };
		}

		return s with { Reputation = s.Reputation - 1f, DidMove = true };
	}

	/// <summary>TraderScript.TraderItemPreference (TraderScript.cs:972-980) — the wire byte values.</summary>
	internal const byte PreferenceWantsTrade = 0;
	internal const byte PreferenceIndifferent = 1;
}
