using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Items;
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
/// <para>
/// The GIVE half runs the REAL <see cref="TradeGiveJudge"/>: the fixture below
/// stands in for the kernel's judged rows and for the game tables' value (the
/// sim has neither), so the identity/ownership/repeat arms and the clamp are the
/// shipped implementation rather than a second copy — only the number's source
/// is a fixture, exactly as the purchase's price is.
/// </para>
/// </summary>
internal sealed class SimTraderHost
{
	private readonly Random _random;

	/// <summary>The judged item rows the give claim is checked against — what the kernel would answer by id.</summary>
	private readonly Dictionary<ulong, ItemState> _rows = [];

	/// <summary>The host's own worth per judged row (the fixture that stands in for the game's item/liquid tables).</summary>
	private readonly Dictionary<ulong, int> _values = [];

	private readonly TradeGiveJudge _giveJudge = new();

	internal SimTraderHost(TradeStockState initial, int seed)
	{
		State = initial;
		_random = new Random(seed);
	}

	/// <summary>Judge an item into this host's record — the fixture for the kernel row a real host would hold.</summary>
	internal void JudgeItem(ulong instanceId, ulong owner, int value, string definition = "bandage")
	{
		_rows[instanceId] = new ItemState(
			new ItemIdentity(instanceId, definition),
			1,
			ItemLocation.Carried(new ActorId(owner)))
		{
			Data = new ItemData(1f, false, -1, [], []),
		};
		_values[instanceId] = value;
	}

	private ItemState? FindItem(ulong instanceId) =>
		_rows.TryGetValue(instanceId, out var row) ? row : null;

	private GiveItemValue ValueOf(ItemState row) =>
		new(_values.TryGetValue(row.Identity.InstanceId, out var value) ? value : 0, null);

	/// <summary>The id the simulation's default judged give fixture uses (owned by the guest, worth 20).</summary>
	internal const ulong GiveFixtureInstanceId = 7001;

	internal const int GiveFixtureValue = 20;

	/// <summary>
	/// The default trader fixture shared by the hand-written simulations and
	/// the replay corpus — one source, so the two suites cannot drift apart.
	/// </summary>
	internal static TradeStockState CreateDefaultState() => new()
	{
		Reputation = 100f,
		Hostility = 0f,
		ValueGiven = 50f, // enough for the cheap items, not the expensive one
		TotalValueGiven = 10f,
		FreeAmount = 0,
		FreeDressing = false,
		DidHug = false,
		StartedConvo = false,
		DidMove = false,
		HaggleAmount = 0f,
		Character = 0,
		BuildHealth = 1000f,
		MinHugReputation = 20f,
		Items =
		[
			new TraderItemMsg { Id = "bandage", Value = 10, Preference = 0 }, // WantsTrade — the +7 rep purchase
			new TraderItemMsg { Id = "knife", Value = 20, Preference = 1 }, // Indifferent — the +4 rep purchase
			new TraderItemMsg { Id = "rifle", Value = 100, Preference = 2 }, // WantsKeep — too expensive for the initial credit
		],
	};

	/// <summary>The authoritative state — what the host broadcasts after every action.</summary>
	internal TradeStockState State { get; private set; }

	/// <summary>Execute one guest interaction like TradeExecutor does: the state
	/// transitions via TradeStockMachine (the random draws are the shell's), the
	/// outcome carries the rejection marker (0 = accepted, otherwise the action —
	/// TradeStateSync's rejected-purchase marker). <paramref name="sender"/> is
	/// the transport sender the give's ownership check reads.</summary>
	internal byte Execute(ulong sender, TraderActionMsg msg)
	{
		var rejected = (byte)0;
		State = msg.Action switch
		{
			TraderActionKind.MeetPlayer => TradeStockMachine.MeetPlayer(State,
				Range(75f, 135f), // TradeExecutor.ExecuteMeetPlayer's Random.Range(75f, 135f)
				msg.ReputationOffset, msg.ReputationScale, msg.ReputationPostOffset,
				msg.PlayerFlags, BandageValue),
			TraderActionKind.Purchase => ExecutePurchase(msg.ItemId, out rejected),
			TraderActionKind.GiveItem => ExecuteGive(sender, msg, out rejected),
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

	private TradeStockState ExecuteGive(ulong sender, TraderActionMsg msg, out byte rejected)
	{
		rejected = (byte)0;
		var verdict = _giveJudge.Judge(sender, msg.ItemInstanceId, msg.ItemValue, FindItem, ValueOf);
		if (verdict.Refusal is not null || verdict.Credit <= 0)
		{
			rejected = (byte)TraderActionKind.GiveItem;
			return State;
		}

		var (accepted, result) = TradeStockMachine.TryGiveItem(State, verdict.Credit);
		if (!accepted)
		{
			rejected = (byte)TraderActionKind.GiveItem;
			return State;
		}

		_giveJudge.RecordGiven(msg.ItemInstanceId); // the shell records the receipt only after the credit lands
		if (verdict.ClaimExceeded)
		{
			rejected = (byte)TraderActionKind.GiveItem; // the answer rides the broadcast; the credit still applied
		}

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
