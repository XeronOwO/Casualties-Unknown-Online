using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.GameAdapter.Character;
using CasualtiesUnknownOnline.GameAdapter.Items;
using CasualtiesUnknownOnline.Runtime.Configuration;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// The host-authoritative trader-recruit domain (KrokMP-inspired co-op revive):
/// the acting side locates a nearby trader and requests a recruit; the host
/// re-validates the trader gates and the dead player's authoritative snapshot,
/// then sends the revived physiological state directly to that player. The
/// target's local body is healed in place — no inventory wipe, no position
/// teleport, no full character restore. The acting side has no vanilla game
/// method to run, so this is a dedicated request/result path, not a
/// TraderActionKind.
/// </summary>
internal sealed class TraderRecruitCoordinator(
	SessionService session,
	WorldService world,
	ICharacterDataControl characterData,
	CharacterDataSync characterDataSync,
	IOptionsMonitor<RespawnOptions> respawnOptions,
	ItemService items,
	ItemIdAllocator itemIds,
	ILogger<TraderRecruitCoordinator> log)
{
	private const float PositionTolerance = 2f;

	private readonly SessionService _session = session;
	private readonly WorldService _world = world;
	private readonly ICharacterDataControl _characterData = characterData;
	private readonly CharacterDataSync _characterDataSync = characterDataSync;
	private readonly IOptionsMonitor<RespawnOptions> _respawnOptions = respawnOptions;
	private readonly ItemService _items = items;
	private readonly ItemIdAllocator _itemIds = itemIds;
	private readonly ILogger<TraderRecruitCoordinator> _log = log;

	/// <summary>Host-side used-trader registry (one recruit per trader instance
	/// per world). Cleared with the session; the trader objects themselves are
	/// unmodified.</summary>
	private readonly HashSet<int> _usedTraders = [];

	internal void BindToSession()
	{
		_world.TraderRecruitRequestReceived += OnTraderRecruitRequestReceived;
		_world.TraderRecruitResultReceived += OnTraderRecruitResultReceived;
	}

	internal void Unbind()
	{
		_world.TraderRecruitRequestReceived -= OnTraderRecruitRequestReceived;
		_world.TraderRecruitResultReceived -= OnTraderRecruitResultReceived;
	}

	internal void Reset() => _usedTraders.Clear();

	/// <summary>
	/// Online UI / plugin entry: the local player requests a recruit of a dead
	/// in-world teammate at the nearest trader. Host: handled immediately.
	/// Guest: reported to the host (the host remains the authority — the acting
	/// side only located the trader).
	/// </summary>
	internal bool TryRequest(ulong targetSteamId)
	{
		if (!_session.SessionActive || !_session.LocalInWorld)
		{
			return false;
		}

		var body = PlayerCamera.main != null ? PlayerCamera.main.body : null; // Unity object — ==
		if (body == null)
		{
			return false;
		}

		var trader = FindNearestTrader(body.transform.position);
		if (trader == null) // Unity object — ==
		{
			_log.LogWarning("[TradeRecruit] no trader within {Range} — request refused.", TraderRecruitPolicy.RecruitRange);
			return false;
		}

		var msg = new TraderRecruitRequestMsg
		{
			TargetSteamId = targetSteamId,
			TraderPosition = new NetVector2Msg(trader.transform.position.x, trader.transform.position.y),
		};

		if (_session.Role == SessionRole.Host)
		{
			HandleHostRequest(_session.LocalSteamId, msg, trader);
		}
		else
		{
			_world.SendTraderRecruitRequest(msg);
			_log.LogInformation("[TradeRecruit] request sent target={Target} trader=({X:0.0},{Y:0.0}).",
				targetSteamId, msg.TraderPosition.X, msg.TraderPosition.Y);
		}

		return true;
	}

	/// <summary>Host: a guest's recruit request arrived — validate and execute.</summary>
	private void OnTraderRecruitRequestReceived(ulong sender, TraderRecruitRequestMsg msg)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive || !_session.LocalInWorld)
		{
			return;
		}

		var trader = FindTraderAt(msg.TraderPosition);
		if (trader == null) // Unity object — ==
		{
			_log.LogWarning("[TradeRecruit] trader not found at ({X:0.0},{Y:0.0}) — dropped.", msg.TraderPosition.X, msg.TraderPosition.Y);
			return;
		}

		HandleHostRequest(sender, msg, trader);
	}

	/// <summary>Guest: the host's authoritative revive result arrived — apply it
	/// to the local Body (the host already persisted the revived snapshot).</summary>
	private void OnTraderRecruitResultReceived(TraderRecruitResultMsg msg)
	{
		if (_session.Role != SessionRole.Guest || !_session.SessionActive
			|| msg.TargetSteamId != _session.LocalSteamId || msg.Health is null)
		{
			return;
		}

		var body = PlayerCamera.main != null ? PlayerCamera.main.body : null; // Unity object — ==
		if (body == null) // Unity object — ==
		{
			_log.LogWarning("[TradeRecruit] revive result for {Target} but no local body — dropped.", msg.TargetSteamId);
			return;
		}

		_log.LogInformation("[TradeRecruit] applying revive to local body health={Health} gifts={Gifts}.", msg.Health.BrainHealth, msg.Items.Count);
		ApplyRevive(body, msg.Health, msg.Limbs, msg.Items);
	}

	/// <summary>
	/// Host-side execute: re-check every gate against the authoritative trader
	/// and character snapshots, persist the revived snapshot, and deliver the
	/// result (wire for a guest, direct local apply for the host itself).
	/// </summary>
	private void HandleHostRequest(ulong requester, TraderRecruitRequestMsg msg, TraderScript trader)
	{
		var target = msg.TargetSteamId;
		if (requester == target || requester == 0 || target == 0)
		{
			_log.LogWarning("[TradeRecruit] refused: invalid requester/target pair {Requester}→{Target}.", requester, target);
			return;
		}

		if (!RespawnPolicy.CanUseTraderRecruit(_respawnOptions.CurrentValue))
		{
			_log.LogInformation("[TradeRecruit] refused: trader revive disabled by host rules (permadeath={Permadeath}, fromTrader={FromTrader}).",
				_respawnOptions.CurrentValue.Permadeath, _respawnOptions.CurrentValue.ReviveFromTrader);
			return;
		}

		if (!IsInWorld(requester) || !IsInWorld(target))
		{
			_log.LogWarning("[TradeRecruit] refused: {Requester} or {Target} is not in-world.", requester, target);
			return;
		}

		var traderState = TradeExecutor.Read(trader);
		if (!TraderRecruitPolicy.CanRecruit(traderState, used: _usedTraders.Contains(trader.GetInstanceID())))
		{
			_log.LogInformation("[TradeRecruit] refused: trader gates fail (rep={Rep} hostility={Host} build={Build} used={Used}).",
				traderState.Reputation, traderState.Hostility, traderState.BuildHealth, _usedTraders.Contains(trader.GetInstanceID()));
			return;
		}

		var requesterData = GetCharacterData(requester);
		if (requesterData?.Health is not { Alive: true, Conscious: true })
		{
			_log.LogInformation("[TradeRecruit] refused: {Requester} is not alive/conscious.", requester);
			return;
		}

		var targetData = GetCharacterData(target);
		if (!TraderRecruitPolicy.IsDead(targetData))
		{
			_log.LogInformation("[TradeRecruit] refused: {Target} is not dead (or no snapshot).", target);
			return;
		}

		var revived = TraderRecruitPolicy.PrepareRevive(targetData!);
		var giftItems = BuildTraderGiftItems(traderState, revived);
		if (giftItems.Count > 0)
		{
			revived.Items.AddRange(giftItems);
			if (target != _session.LocalSteamId)
			{
				foreach (var gift in giftItems)
				{
					_items.AdoptTransferredItem(target, gift.InstanceId, gift);
				}
			}

			_log.LogInformation(
				"[TradeRecruit] granted {Count} trader item(s) to {Target}: {Items}.",
				giftItems.Count, target, string.Join(", ", giftItems.Select(x => x.ItemId)));
		}

		SaveCharacterData(target, revived);
		_usedTraders.Add(trader.GetInstanceID());

		_log.LogInformation(
			"[TradeRecruit] {Requester} recruited {Target} at trader ({X:0.0},{Y:0.0}); health={Health}.",
			requester, target, trader.transform.position.x, trader.transform.position.y, revived.Health?.BrainHealth);

		if (target == _session.LocalSteamId)
		{
			var body = PlayerCamera.main != null ? PlayerCamera.main.body : null; // Unity object — ==
			if (body != null && revived.Health is { } health) // Unity object — ==
			{
				ApplyRevive(body, health, revived.Limbs, giftItems);
			}
		}
		else
		{
			_world.SendTraderRecruitResult(target, new TraderRecruitResultMsg
			{
				TargetSteamId = target,
				Health = revived.Health,
				Limbs = [.. revived.Limbs],
				Items = [.. giftItems],
			});
		}
	}

	/// <summary>
	/// Build the host-selected trader-stock gift items for a successful recruit.
	/// The count is capped by the target's empty inventory slots and by the
	/// trader's distinct stock; each item is captured from the prefab (fresh
	/// state, no temporary instantiation) and allocated a host instance id.
	/// </summary>
	private List<CharacterItemMsg> BuildTraderGiftItems(
		TradeStockState traderState,
		CharacterDataMsg revived)
	{
		var emptySlots = TraderRecruitPolicy.FindEmptySlots(revived);
		if (emptySlots.Count == 0 || traderState.Items.Count == 0)
		{
			return [];
		}

		var count = Random.Range(TraderRecruitPolicy.MinGiftItems, TraderRecruitPolicy.MaxGiftItems + 1);
		count = Mathf.Min(count, emptySlots.Count);
		var selected = TraderRecruitPolicy.SelectGiftItemIds(traderState, count, n => Random.Range(0, n));

		var gifts = new List<CharacterItemMsg>();
		foreach (var itemId in selected)
		{
			if (gifts.Count >= emptySlots.Count)
			{
				break;
			}

			var gift = CreateGiftItem(itemId, emptySlots[gifts.Count]);
			if (gift != null)
			{
				gifts.Add(gift);
			}
		}

		return gifts;
	}

	/// <summary>
	/// Capture a fresh wire item fact from the prefab for a trader-stock id. No
	/// temporary scene instance is created, so no item-domain report can fire;
	/// the host only allocates the instance id that the recipient will bind.
	/// </summary>
	private CharacterItemMsg? CreateGiftItem(string itemId, int slot)
	{
		var prefab = (GameObject?)Resources.Load(itemId);
		if (prefab == null) // Unity object — ==
		{
			_log.LogWarning("[TradeRecruit] gifted item {ItemId} has no prefab — skipped.", itemId);
			return null;
		}

		var item = prefab.GetComponent<Item>();
		if (item == null) // Unity object — ==
		{
			_log.LogWarning("[TradeRecruit] gifted prefab {ItemId} has no Item component — skipped.", itemId);
			return null;
		}

		var gift = ItemStateCodec.CaptureItem(item, slot);
		gift.InstanceId = _itemIds.AllocateId();
		_log.LogInformation("[TradeRecruit] prepared gift {ItemId} (id {InstanceId}) for slot {Slot}.", itemId, gift.InstanceId, slot);
		return gift;
	}

	private void ApplyRevive(
		Body body,
		CharacterHealthMsg health,
		IReadOnlyList<CharacterLimbMsg> limbs,
		IReadOnlyList<CharacterItemMsg>? gifts = null)
	{
		using (CallContext.Enter(CallContext.Origin.RemoteApply))
		{
			_characterDataSync.ApplyHealState(body, health, limbs);
			if (gifts is { Count: > 0 })
			{
				ApplyTraderGiftItems(body, gifts);
			}

			// Re-report immediately so the peer clones and the host's saved
			// snapshot see the revived state without waiting for the next 1 Hz tick.
			_characterDataSync.ReportInventoryChanged(body);
		}
	}

	/// <summary>
	/// Place gifted items into the local body. The host-chosen slot is preferred;
	/// if the live body has since filled it, the body's own first empty slot is
	/// the fallback (the immediate re-report carries the real slot back).
	/// </summary>
	private void ApplyTraderGiftItems(Body body, IReadOnlyList<CharacterItemMsg> gifts)
	{
		foreach (var gift in gifts)
		{
			var slot = gift.SlotIndex;
			if (slot < 0 || slot >= body.slots.Length || body.HoldingItem(slot))
			{
				var fallback = body.FirstEmptySlot();
				if (fallback is not { } empty)
				{
					_log.LogWarning("[TradeRecruit] cannot place gift {ItemId} (id {InstanceId}) — no empty slot.", gift.ItemId, gift.InstanceId);
					continue;
				}

				slot = empty;
				gift.SlotIndex = slot;
			}

			ItemStateCodec.RestoreItem(gift, body);
			_log.LogInformation("[TradeRecruit] placed gift {ItemId} (id {InstanceId}) in slot {Slot}.", gift.ItemId, gift.InstanceId, slot);
		}
	}

	private CharacterDataMsg? GetCharacterData(ulong steamId) =>
		steamId == _session.LocalSteamId
			? _characterData.GetHostCharacterData()
			: _characterData.GetSavedCharacter(steamId);

	private void SaveCharacterData(ulong steamId, CharacterDataMsg data)
	{
		if (steamId == _session.LocalSteamId)
		{
			_characterData.SaveHostCharacterData(data);
		}
		else
		{
			_characterData.SaveCharacterData(steamId, data);
		}
	}

	private bool IsInWorld(ulong steamId) =>
		steamId == _session.LocalSteamId ? _session.LocalInWorld : _session.IsRemoteInWorld(steamId);

	private static TraderScript? FindNearestTrader(Vector2 position)
	{
		TraderScript? best = null; // Unity object — ==
		var bestDistance = float.MaxValue;
		foreach (var trader in UnityEngine.Object.FindObjectsOfType<TraderScript>())
		{
			var distance = Vector2.Distance(trader.transform.position, position);
			if (distance <= TraderRecruitPolicy.RecruitRange && distance < bestDistance)
			{
				best = trader;
				bestDistance = distance;
			}
		}

		return best;
	}

	/// <summary>Position-keyed trader lookup — same identity rule as the trade domain.</summary>
	private static TraderScript? FindTraderAt(NetVector2Msg position)
	{
		foreach (var trader in UnityEngine.Object.FindObjectsOfType<TraderScript>())
		{
			if (Vector2.Distance(trader.transform.position, new Vector2(position.X, position.Y)) < PositionTolerance)
			{
				return trader;
			}
		}

		return null;
	}
}
