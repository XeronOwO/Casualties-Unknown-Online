using System.Linq;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.World;
using HarmonyLib;
using Microsoft.Extensions.Logging;
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
internal sealed class TradeExecutor(IItemControl items, ILogger<TradeExecutor> log)
{
	private readonly IItemControl _items = items;
	private readonly ILogger<TradeExecutor> _log = log;

	/// <summary>The give claim's data judgement (the identity/ownership/repeat arms and the clamp rule) — one implementation shared with the simulation, so the tested rule is the shipped one.</summary>
	private readonly TradeGiveJudge _giveJudge = new();
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

	/// <summary>
	/// GiveItem (TraderScript.cs:604-639): credit the value — the item itself was destroyed on the acting
	/// side (the item domain reported it). The reported credit is a CLAIM about a live item, so it is
	/// judged against the host's own row for the item the report names (decision 265) by
	/// <see cref="TradeGiveJudge"/>: the row must exist, be live and be the reporter's own (or world
	/// content); the item's worth comes from the row's recorded state through the game's own
	/// <c>ItemInfo.GetValue</c> tables and arithmetic (<see cref="TradeStockMachine.GiveItemValue"/>); and
	/// the claim is credited as it stands but never above that number. A claim above the host's number is
	/// CLAMPED and ANSWERED (the reject marker rides the state broadcast) instead of refused, because the
	/// host's row can be a frame behind the giver's live item (condition decays every frame, latency moves
	/// the report) and neither side may lose the item over a snapshot that moved. The value half is the
	/// only game-table read here; every refusal the judge names credits nothing.
	/// </summary>
	internal bool ExecuteGiveItem(TraderScript trader, ulong sender, ulong itemInstanceId, int reportedValue)
	{
		var verdict = _giveJudge.Judge(sender, itemInstanceId, reportedValue, _items.FindJudgedItem, ValueOf);
		if (verdict.Refusal is not null || verdict.Credit <= 0)
		{
			_log.LogWarning("[Trade] {Sender}'s give of item #{InstanceId} credited nothing: {Detail}. Nothing applied.",
				sender, itemInstanceId, verdict.Detail);
			return false;
		}

		var state = Read(trader);
		var (accepted, result) = TradeStockMachine.TryGiveItem(state, verdict.Credit);
		Write(trader, result);

		if (!accepted)
		{
			_log.LogWarning("[Trade] {Sender}'s give of item #{InstanceId} credited nothing — the lifetime cap is already reached ({Detail}). Nothing applied.",
				sender, itemInstanceId, verdict.Detail);
			return false;
		}

		_giveJudge.RecordGiven(itemInstanceId);
		if (verdict.ClaimExceeded)
		{
			_log.LogWarning("[Trade] {Sender}'s give claimed more than the host's own record makes the item worth — {Detail}. Credited {Credit}, the answer rides the state broadcast.",
				sender, verdict.Detail, verdict.Credit);
			return false;
		}

		_log.LogInformation("[Trade] {Sender}'s give credited {Credit} ({Detail}).", sender, verdict.Credit, verdict.Detail);
		return true;
	}

	/// <summary>
	/// The GAME-TABLE half of the give's value: the definition's own <c>value</c> and the liquid stacks'
	/// <c>valuePerLiter</c>, read from the host's own game data and handed to the pure
	/// <see cref="TradeStockMachine.GiveItemValue"/> with the row's recorded instance state (condition,
	/// liquid amounts, ammo rounds). Refusals here are the host's inability to compute a number at all, so
	/// they are named rather than guessed at.
	/// </summary>
	private static GiveItemValue ValueOf(ItemState row)
	{
		var definition = row.Identity.DefinitionId;
		if (!Item.GlobalItems.TryGetValue(definition, out var info)) // GetItem itself indexes and throws on an unknown definition
		{
			return new GiveItemValue(0, $"the host has no item definition '{definition}' for item #{row.Identity.InstanceId}");
		}

		var liquidValue = 0f;
		foreach (var stack in row.Data.Liquids)
		{
			if (Liquids.Registry is null || !Liquids.Registry.TryGetValue(stack.LiquidId, out var liquid))
			{
				return new GiveItemValue(0, $"item #{row.Identity.InstanceId} carries liquid '{stack.LiquidId}', which is not in this host's table");
			}

			liquidValue += stack.Amount * 0.001f * liquid.valuePerLiter;
		}

		var magazine = false;
		var rounds = 0;
		var maxRounds = 0;
		var ammo = row.Data.Components.FirstOrDefault(c => c.TypeName == nameof(AmmoScript));
		if (ammo is not null)
		{
			magazine = FieldOf(ammo, "itemType") == (int)AmmoScript.AmmoItemType.Magazine;
			rounds = FieldOf(ammo, "rounds");
			maxRounds = FieldOf(ammo, "maxRounds");
		}

		return new GiveItemValue(
			TradeStockMachine.GiveItemValue(info.value, row.Data.Condition, magazine, rounds, maxRounds, liquidValue),
			null);
	}

	private static int FieldOf(ItemComponentState component, string name) =>
		component.Fields.FirstOrDefault(f => f.Name == name)?.IntValue ?? 0;

	/// <summary>Drop the session's give receipts (the session ended — the next one has its own trader states and fresh item rows).</summary>
	internal void ResetSessionState() => _giveJudge.ResetSessionState();

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
