using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.World;
using HarmonyLib;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// The trade domain coordinator: the trader state is host-authoritative. The
/// acting side ran the game method in full (its player-side effects are
/// immediate) and reports; the host executes the trader-side change
/// (<see cref="TradeExecutor"/>) and broadcasts the full state overwrite to
/// every member (the acting side included — its provisional local state is
/// replaced, which also rolls back a rejected concurrent purchase); the guests
/// apply the overwrite. The deterministic game paths (hostility MoveTowards,
/// LightBroken's flat -40, the last-slept bump) run on both sides from the
/// broadcasted base. World entry sends every trader's snapshot; a 5 s
/// fallback covers missed broadcasts and a new layer's traders.
/// </summary>
internal sealed class TradeStateSync(
	IWorldControl world,
	ISessionControl session,
	TradeExecutor executor,
	ILogger<TradeStateSync> log)
{
	private const float SnapshotInterval = 5f; // the unreliable fallback broadcast
	private const float PositionTolerance = 2f; // matching tolerance (the trader's transform is the position key)

	private readonly IWorldControl _world = world;
	private readonly ISessionControl _session = session;
	private readonly TradeExecutor _executor = executor;
	private readonly ILogger<TradeStateSync> _log = log;
	private float _lastSnapshot;
	private Item? _pendingPurchase; // the acting side's last locally-bought item — destroyed on a rejected purchase (the overwrite alone would leave it in the inventory)

	internal void BindToSession()
	{
		_world.TraderActionReceived += OnTraderActionReceived; // host: execute + broadcast
		_world.TraderStateReceived += OnTraderStateReceived; // guest: apply
	}

	internal void Unbind()
	{
		_world.TraderActionReceived -= OnTraderActionReceived;
		_world.TraderStateReceived -= OnTraderStateReceived;
	}

	internal void Update()
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return;
		}

		if (Time.unscaledTime - _lastSnapshot <= SnapshotInterval)
		{
			return;
		}

		_lastSnapshot = Time.unscaledTime;
		var traders = Object.FindObjectsOfType<TraderScript>(); // Unity object — the game's own registry
		_log.LogDebug("[Trade] snapshot n={N} members={M}.", traders.Length, _session.Members.Count());
		foreach (var member in _session.Members)
		{
			if (!member.InWorld)
			{
				continue;
			}

			foreach (var trader in traders)
			{
				_world.SendTraderState(member.SteamId, BuildStateMsg(trader, 0));
			}
		}
	}

	/// <summary>
	/// The patch-bridge entry: a trader interaction ran locally (the full game
	/// method). Host: the state is already authoritative — broadcast the
	/// overwrite. Guest: report it — the host executes the trader-side change
	/// and broadcasts; the acting side's provisional state is overwritten.
	/// </summary>
	internal void OnTraderActionReported(TraderScript trader, TraderActionKind action, string itemId, int itemValue, Item? purchaseItem)
	{
		if (!_session.SessionActive || trader == null) // Unity object — ==
		{
			return;
		}

		if (_session.Role == SessionRole.Host)
		{
			_world.BroadcastTraderState(BuildStateMsg(trader, 0));
			_log.LogInformation("[Trade] host broadcast action={Action} trader=({X:0.0},{Y:0.0}).",
				action, trader.transform.position.x, trader.transform.position.y);
			return;
		}

		if (action == TraderActionKind.Purchase)
		{
			_pendingPurchase = purchaseItem;
		}

		var body = PlayerCamera.main.body;
		var msg = new TraderActionMsg
		{
			Action = action,
			Position = new NetVector2Msg(trader.transform.position.x, trader.transform.position.y),
			ItemId = itemId,
			ItemValue = itemValue,
			PlayerFlags = ComputePlayerFlags(body),
		};
		if (action == TraderActionKind.MeetPlayer)
		{
			(msg.ReputationOffset, msg.ReputationScale, msg.ReputationPostOffset) = ComputeReputationChain(body);
		}

		_world.SendTraderAction(msg);
		_log.LogInformation("[Trade] report action={Action} trader=({X:0.0},{Y:0.0}) item={Item}.", action, msg.Position.X, msg.Position.Y, itemId);
	}

	/// <summary>Host: a guest's interaction arrived — execute the trader-side change and broadcast the authoritative state (with the rejection marker when the stock was already consumed).</summary>
	private void OnTraderActionReceived(ulong sender, TraderActionMsg msg)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return;
		}

		var trader = FindTraderAt(msg.Position);
		if (trader == null) // Unity object — ==
		{
			_log.LogWarning("[Trade] trader not found at ({X:0.0},{Y:0.0}) — dropped.", msg.Position.X, msg.Position.Y);
			return;
		}

		var accepted = msg.Action switch
		{
			TraderActionKind.MeetPlayer => Execute(() => _executor.ExecuteMeetPlayer(trader, msg), true),
			TraderActionKind.Purchase => _executor.ExecutePurchase(trader, msg.ItemId),
			TraderActionKind.GiveItem => _executor.ExecuteGiveItem(trader, msg.ItemValue),
			TraderActionKind.Haggle => Execute(() => _executor.ExecuteHaggle(trader), true),
			TraderActionKind.Threaten => Execute(() => _executor.ExecuteThreaten(trader, (msg.PlayerFlags & TraderActionMsg.FlagHasGun) != 0), true),
			TraderActionKind.Hug => Execute(() => _executor.ExecuteHug(trader, (msg.PlayerFlags & TraderActionMsg.FlagDirty) != 0), true),
			TraderActionKind.MoveTo => Execute(() => _executor.ExecuteMoveTo(trader), true),
			_ => false,
		};

		_world.BroadcastTraderState(BuildStateMsg(trader, accepted ? (byte)0 : (byte)msg.Action));
		_log.LogInformation("[Trade] executed action={Action} by={Sender} trader=({X:0.0},{Y:0.0}) accepted={Accepted} rep={Rep} items={N}.",
			msg.Action, sender, msg.Position.X, msg.Position.Y, accepted, trader.reputation, trader.items.Count);
	}

	/// <summary>Guest: the host's authoritative state arrived — overwrite the local trader (fields + stock), roll back a rejected purchase, refresh the open trade UI.</summary>
	private void OnTraderStateReceived(TraderStateMsg msg)
	{
		if (_session.Role == SessionRole.Host)
		{
			return;
		}

		var trader = FindTraderAt(msg.Position);
		if (trader == null) // Unity object — ==
		{
			_log.LogWarning("[Trade] apply: trader not found at ({X:0.0},{Y:0.0}) — dropped.", msg.Position.X, msg.Position.Y);
			return;
		}

		trader.reputation = msg.Reputation;
		trader.hostility = msg.Hostility;
		trader.valueGiven = msg.ValueGiven;
		trader.totalValueGiven = msg.TotalValueGiven;
		var fields = Traverse.Create(trader);
		fields.Field("freeAmount").SetValue(msg.FreeAmount);
		fields.Field("freeDressing").SetValue(msg.FreeDressing);
		fields.Field("didHug").SetValue(msg.DidHug);
		trader.didMove = msg.DidMove;
		trader.startedConvo = msg.StartedConvo;
		trader.haggleAmount = msg.HaggleAmount;
		trader.items = [.. msg.Items.Select(i => new TraderItem
		{
			id = i.Id,
			value = i.Value,
			preference = (TraderScript.TraderItemPreference)i.Preference,
			bought = i.Bought,
		})];

		if (msg.RejectedAction == (byte)TraderActionKind.Purchase && _pendingPurchase != null) // Unity object — ==
		{
			_log.LogInformation("[Trade] rejected purchase — rolling back the locally bought item {Item}.", _pendingPurchase.id);
			Object.Destroy(_pendingPurchase.gameObject); // its item domain report (no instance id, a backpack item) is a no-op
			_pendingPurchase = null;
		}
		else if (msg.RejectedAction != 0)
		{
			_log.LogInformation("[Trade] rejected action={Reject} — no rollback surface (give credit capped on the host).", msg.RejectedAction);
		}

		RefreshTradeUi(trader);
	}

	private static void RefreshTradeUi(TraderScript trader)
	{
		var camera = PlayerCamera.main;
		if (camera == null || !camera.tradeMenu.activeSelf || camera.currentTrader != trader) // Unity objects — ==
		{
			return;
		}

		camera.RefreshTraderInventories();
		camera.UpdateTradeTexts();
	}

	private static TraderStateMsg BuildStateMsg(TraderScript trader, byte rejectedAction)
	{
		var fields = Traverse.Create(trader);
		return new()
		{
			Position = new NetVector2Msg(trader.transform.position.x, trader.transform.position.y),
			Reputation = trader.reputation,
			Hostility = trader.hostility,
			ValueGiven = trader.valueGiven,
			TotalValueGiven = trader.totalValueGiven,
			FreeAmount = (byte)fields.Field("freeAmount").GetValue<int>(),
			FreeDressing = fields.Field("freeDressing").GetValue<bool>(),
			DidHug = fields.Field("didHug").GetValue<bool>(),
			DidMove = trader.didMove,
			StartedConvo = trader.startedConvo,
			HaggleAmount = trader.haggleAmount,
			RejectedAction = rejectedAction,
			Items = [.. trader.items.Select(i => new TraderItemMsg
			{
				Id = i.id,
				Value = i.value,
				Preference = (byte)i.preference,
				Bought = i.bought,
			})],
		};
	}

	/// <summary>Position-keyed trader lookup — both sides generated the same trader at the same place (WorldGeneration.cs:3438-3447), so the transform position is the identity.</summary>
	private static TraderScript? FindTraderAt(NetVector2Msg pos)
	{
		foreach (var trader in Object.FindObjectsOfType<TraderScript>()) // Unity object registry
		{
			if (Vector2.Distance(trader.transform.position, new Vector2(pos.X, pos.Y)) < PositionTolerance)
			{
				return trader;
			}
		}

		return null;
	}

	/// <summary>The acting player's state bits the trader's methods read (MeetPlayer's
	/// bandage + hostility, Threaten's success lerp, TryHug's failure gate).</summary>
	private static byte ComputePlayerFlags(Body body)
	{
		var flags = 0;
		if (body.totalBleedSpeed > 0.001f)
		{
			flags |= TraderActionMsg.FlagBleeding;
		}

		if (body.HoldingItem(body.handSlot) && body.GetItem(body.handSlot)?.Stats.HasTag("gun") == true)
		{
			flags |= TraderActionMsg.FlagHasGun;
		}

		if (body.dirtyness > 50f)
		{
			flags |= TraderActionMsg.FlagDirty;
		}

		return (byte)flags;
	}

	/// <summary>
	/// The MeetPlayer reputation chain (TraderScript.cs:112-137), split around the
	/// mindWipe ×0.7: the pre-scale additions, the scale and the post-scale
	/// additions — reputation = (base + Offset) × Scale + PostOffset. Deterministic
	/// (no random), so the acting side computes it from its own body.
	/// </summary>
	private static (float Offset, float Scale, float PostOffset) ComputeReputationChain(Body body)
	{
		var offset = body.skills.INTFrom10 * 4f;
		offset += WorldGeneration.GetRunSettingFloat("traderrepoffset");
		if (body.talker.impairedSpeech)
		{
			offset -= 20f;
		}

		if (body.disfigured)
		{
			offset -= 5f;
		}

		if (body.dirtyness > 50f)
		{
			offset -= body.dirtyness * 0.25f;
		}

		if (body.HoldingItem(body.handSlot) && body.GetItem(body.handSlot)?.Stats.HasTag("gun") == true)
		{
			offset -= 20f; // the reputation half of the gun penalty (hostility travels via the HasGun flag)
		}

		var scale = body.mindWipe != null ? 0.7f : 1f; // Unity object — ==
		var post = -(100f - body.brainHealth) * 0.5f;
		post += body.happiness * 0.5f;
		if (body.hearingLoss > 50f)
		{
			post -= 20f;
		}

		return (offset, scale, post);
	}

	private static bool Execute(System.Action action, bool accepted)
	{
		action();
		return accepted;
	}
}
