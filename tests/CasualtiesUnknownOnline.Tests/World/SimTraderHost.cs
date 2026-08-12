using System;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.World;

namespace CasualtiesUnknownOnline.Tests.World;

/// <summary>
/// The trade simulation's HOST-side trader: the GameAdapter's TradeExecutor
/// shell re-created in pure form — it holds one trader's authoritative
/// <see cref="TradeStockState"/>, executes a guest's interaction through the
/// REAL <see cref="TradeStockMachine"/> with the same random draws the shell
/// makes (Unity Random → seeded System.Random) and exposes the state as the
/// wire message. This is what the simulation asserts convergence against:
/// the guest's received state must equal this trader's state after every
/// interaction, under faults.
/// </summary>
internal sealed class SimTraderHost
{
	private readonly Random _random;

	internal SimTraderHost(TradeStockState initial, int seed)
	{
		State = initial;
		_random = new Random(seed);
	}

	/// <summary>The authoritative state — what the host broadcasts after every action.</summary>
	internal TradeStockState State { get; private set; }

	/// <summary>Execute one guest interaction like TradeExecutor does: the state
	/// transitions via TradeStockMachine (the random draws are the shell's), the
	/// outcome carries the rejection marker (0 = accepted, otherwise the action —
	/// TradeStateSync's rejected-purchase marker).</summary>
	internal byte Execute(TraderActionMsg msg)
	{
		var rejected = (byte)0;
		State = msg.Action switch
		{
			TraderActionKind.MeetPlayer => TradeStockMachine.MeetPlayer(State,
				Range(75f, 135f), // TradeExecutor.ExecuteMeetPlayer's Random.Range(75f, 135f)
				msg.ReputationOffset, msg.ReputationScale, msg.ReputationPostOffset,
				msg.PlayerFlags, BandageValue),
			TraderActionKind.Purchase => ExecutePurchase(msg.ItemId, out rejected),
			TraderActionKind.GiveItem => ExecuteGive(msg.ItemValue),
			TraderActionKind.Haggle => TradeStockMachine.Haggle(State,
				Range(-25f, 25f), Range(20f, 28f), Range(6, 11)), // the shell's three draws
			TraderActionKind.Threaten => TradeStockMachine.Threaten(State,
				(msg.PlayerFlags & TraderActionMsg.FlagHasGun) != 0,
				(float)_random.NextDouble(), Range(2, 4)), // Random.value + Random.Range(2, 4)
			TraderActionKind.Hug => TradeStockMachine.Hug(State,
				(msg.PlayerFlags & TraderActionMsg.FlagDirty) != 0),
			TraderActionKind.MoveTo => TradeStockMachine.MoveTo(State),
			_ => State,
		};
		return rejected;
	}

	/// <summary>Execute the purchase (TradeExecutor.ExecutePurchase): the game's own
	/// price (here: the entry's value — a deterministic game-side query both sides
	/// derive the same), then the machine. False = the stock was already consumed
	/// (rejected — RejectedAction rides the broadcast).</summary>
	private TradeStockState ExecutePurchase(string itemId, out byte rejected)
	{
		if (!State.Items.Any(i => i.Id == itemId))
		{
			rejected = (byte)TraderActionKind.Purchase;
			return State;
		}

		var price = State.Items.First(i => i.Id == itemId).Value; // the trader's ItemPrice(item) — deterministic
		var (accepted, result) = TradeStockMachine.TryPurchase(State, itemId, price);
		rejected = accepted ? (byte)0 : (byte)TraderActionKind.Purchase;
		return result;
	}

	private TradeStockState ExecuteGive(int value)
	{
		var (_, result) = TradeStockMachine.TryGiveItem(State, value);
		return result;
	}

	internal TraderStateMsg ToStateMsg() => new()
	{
		Position = new NetVector2Msg(TraderPosX, TraderPosY),
		Reputation = State.Reputation,
		Hostility = State.Hostility,
		ValueGiven = (int)State.ValueGiven, // the wire carries the game's int field (TradeExecutor.Write's cast)
		TotalValueGiven = (int)State.TotalValueGiven,
		FreeAmount = (byte)State.FreeAmount,
		FreeDressing = State.FreeDressing,
		DidHug = State.DidHug,
		DidMove = State.DidMove,
		StartedConvo = State.StartedConvo,
		HaggleAmount = State.HaggleAmount,
		Items = [.. State.Items],
	};

	internal const float TraderPosX = 123f;

	internal const float TraderPosY = 45f;

	private const int BandageValue = 10; // Item.GetItem("bandage").value on the acting side (deterministic)

	private float Range(float min, float max) => min + (float)_random.NextDouble() * (max - min);

	private int Range(int min, int max) => min + _random.Next(max - min);
}
