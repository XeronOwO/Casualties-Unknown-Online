using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using MapsterMapper;
using Microsoft.Extensions.Logging;
using UnityEngine;

using CasualtiesUnknownOnline.GameAdapter.Items;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// Character-data domain: the session-scoped save/restore (host stores the
/// guests' 1 Hz snapshots per SteamID; a reconnect or a death → re-enter cycle
/// gets the saved state back) and the remote clones' inventory rendering
/// source (the latest snapshot per SteamID). No reentry guards of its own —
/// nothing here mutates game items in a way that fires the item hooks.
/// </summary>
internal sealed class CharacterDataSync(
	SessionService session,
	CharacterDataStore characterData,
	IMapper mapper,
	CloneInventoryRenderer inventoryRenderer,
	CloneFactTable factTable,
	ILogger<CharacterDataSync> log)
{
	private readonly SessionService _session = session;
	private readonly CharacterDataStore _characterData = characterData;
	private readonly IMapper _mapper = mapper;
	private readonly CloneInventoryRenderer _inventoryRenderer = inventoryRenderer;
	private readonly CloneFactTable _factTable = factTable;
	private readonly ILogger<CharacterDataSync> _log = log;

	/// <summary>A clone's snapshot cache updated (SteamId) — the renderer re-renders that clone's carried items. Without this, the clone only rendered once at creation ("after the starting supplies, the peer never sees carried-item updates").</summary>
	public event Action<ulong>? CloneSnapshotUpdated
	{
		add => _factTable.CloneSnapshotUpdated += value;
		remove => _factTable.CloneSnapshotUpdated -= value;
	}

	private CharacterDataMsg? _pendingRestore; // guest side: host-sent restore, applied once the body exists
	private bool _restoreWipePending; // first pass wiped the slots (Destroy is end-of-frame) — items go in on the next frame
	private const float CharacterReportInterval = 1f; // guest → host character snapshot (1 Hz)
	private long _nextCharacterReportMs;

	/// <summary>Read-only view for the clone renderer: latest snapshot per SteamId.</summary>
	internal IReadOnlyDictionary<ulong, CharacterDataMsg> CloneData => _factTable.CloneData;

	/// <summary>Carried-fact event (the owner's fact-table entry updates and the clone re-renders immediately) — the fact table lives in CloneFactTable.</summary>
	internal void ApplyCarriedSync(ulong owner, CharacterItemMsg item, bool slotKnown) => _factTable.ApplyCarriedSync(owner, item, slotKnown);

	/// <summary>The owner's starting supplies with self-assigned ids arrived — merged into its fact table (clone render + snapshot divergence baseline).</summary>
	internal void ApplyCarriedInventory(ulong owner, IReadOnlyList<CharacterItemMsg> items) => _factTable.ApplyCarriedInventory(owner, items);

	/// <summary>A carried item left into the world — it leaves the owner's fact table (top-level or nested in a container's contents).</summary>
	internal void RemoveCarriedItem(ulong itemId) => _factTable.RemoveCarriedItem(itemId);

	internal void BindToSession()
	{
		_characterData.CharacterDataReceived += OnCharacterDataReceived;
		_characterData.HostCharacterDataReceived += OnHostCharacterDataReceived;
	}

	internal void Unbind()
	{
		_characterData.CharacterDataReceived -= OnCharacterDataReceived;
		_characterData.HostCharacterDataReceived -= OnHostCharacterDataReceived;
	}

	private void OnCharacterDataReceived(ulong sender, CharacterDataMsg data)
	{
		if (_session.Role == SessionRole.Host)
		{
			// A guest's 1 Hz report — render its clone's inventory (the slots
			// show what it is carrying; the new body renders on creation from
			// this cache).
			_factTable.ApplySnapshot(sender, data);
			_log.LogInformation("[CloneRender] host: char data from {Sender} ({Count} items).", sender, data.Items.Count);
			return;
		}

		// The host relays the OTHER guests' reports (OwnerSteamId stamped) —
		// render that guest's clone inventory on this side too; without the
		// relay a guest could never see what another guest carries/wears.
		if (data.OwnerSteamId != 0 && data.OwnerSteamId != _session.LocalSteamId)
		{
			_factTable.ApplySnapshot(data.OwnerSteamId, data);
			_log.LogInformation("[CloneRender] guest: char data relay of {Owner} ({Count} items).", data.OwnerSteamId, data.Items.Count);
			return;
		}

		// Our own report echoed back by the host (restore path) — may arrive
		// before the local body exists (still loading the run); apply once the
		// game has spawned it (TryApplyCharacterRestore).
		_pendingRestore = data;
		_log.LogInformation("Received character restore ({Items} items).", data.Items.Count);
	}

	/// <summary>Guest side: the host's own 1 Hz snapshot — render its clone's inventory (never applied to the local body).</summary>
	private void OnHostCharacterDataReceived(CharacterDataMsg data)
	{
		_factTable.ApplySnapshot(_session.HostSteamId, data);
		_log.LogInformation("[CloneRender] guest: host char data ({Count} items).", data.Items.Count);
	}

	/// <summary>Render a remote clone's carried state from its owner's character
	/// snapshot — pure display (the renderer's single entry; matching items
	/// stay and their component state refreshes, changed ones swap, the emptied
	/// disappear). Called when a clone appears and when a snapshot updates.</summary>
	internal void ApplyCloneInventory(Body clone, CharacterDataMsg data) => _inventoryRenderer.ApplyCloneInventory(clone, data);

	/// <summary>Pump: re-report the character snapshot on the 1 Hz interval (only when the body exists).</summary>
	internal void Update(Body? localBody)
	{
		if (localBody == null) // Unity object — ==
		{
			return;
		}

		if (_pendingRestore is not null || _restoreWipePending)
		{
			return; // restoring: a fresh-run snapshot would overwrite the host's saved character data
		}

		var nowMs = Environment.TickCount;
		if (nowMs < _nextCharacterReportMs)
		{
			return;
		}

		_nextCharacterReportMs = nowMs + (long)(CharacterReportInterval * 1000f);
		ReportCharacterData(CaptureCharacterData(localBody), throttled: true);
	}

	/// <summary>An inventory-internal move finished (SwapSlots/SwitchHands) — re-report right away (the 1 Hz throttle alone reads as a 1-2 s delay on the peer's clone).</summary>
	internal void ReportInventoryChanged(Body? localBody)
	{
		if (localBody != null && _session.SessionActive && _pendingRestore is null && !_restoreWipePending) // Unity object — ==
		{
			_log.LogInformation("[CloneRender] inventory changed — immediate re-report.");
			ReportCharacterData(CaptureCharacterData(localBody), throttled: false);
		}
	}

	private void ReportCharacterData(CharacterDataMsg data, bool throttled)
	{
		if (_session.Role == SessionRole.Host)
		{
			// Host → guests: their clones of the host render its carried items.
			if (throttled)
			{
				_log.LogInformation("[CloneRender] host broadcasting char data ({Count} items).", data.Items.Count);
			}

			_characterData.BroadcastHostCharacterData(data);
		}
		else
		{
			if (throttled)
			{
				_log.LogInformation("[CloneRender] guest reporting char data ({Count} items).", data.Items.Count);
			}

			_characterData.ReportCharacterData(data);
		}
	}

	/// <summary>Host side: a NEW run started (the host clicked start) — the previous run's saved characters are void (see CharacterDataStore.ClearSavedCharacters).</summary>
	internal void ClearSavedCharacters() => _characterData.ClearSavedCharacters();

	/// <summary>Leaving the world (death, menu) — push a final snapshot so the host's save carries the state at the moment of leaving, not the last 1 Hz report (a death → re-enter cycle would otherwise restore the pre-death state).</summary>
	internal void NotifyBodyLeft(Body prevBody)
	{
		if (_pendingRestore is null && !_restoreWipePending)
		{
			_characterData.ReportCharacterData(CaptureCharacterData(prevBody));
		}
	}

	private void TryApplyCharacterRestore(Body body)
	{
		if (_pendingRestore is null)
		{
			return;
		}

		// Apply only once world generation finished: the game hands out the
		// starting supplies inside generation (WorldPlacePlayer), and the
		// restore wipes the slots first — applying during generation would
		// race that handout (observed: the default lantern ending up on the
		// ground instead of in the restored inventory).
		if (HarmonyTraverse.IsGenerating())
		{
			return;
		}

		if (_restoreWipePending)
		{
			// Second pass (next frame): the wipe's Destroy ran at the end of
			// the previous frame, so the slots are actually empty now and
			// PickUpItem succeeds — it silently refuses a non-empty slot
			// (Body.cs:1388), which stranded the restored items on the ground.
			ApplyRestoredItems(body, _pendingRestore);
			_pendingRestore = null;
			_restoreWipePending = false;
			return;
		}

		ApplyRestoredStatsAndWipe(body, _pendingRestore);
		_restoreWipePending = true;
	}

	/// <summary>Pump entry used by the run coordinator: applies a pending host restore once the local body exists.</summary>
	internal void UpdateRestore(Body localBody)
	{
		if (_pendingRestore is not null)
		{
			TryApplyCharacterRestore(localBody);
		}
	}

	private CharacterDataMsg CaptureCharacterData(Body body)
	{
		var msg = new CharacterDataMsg
		{
			Skills = _mapper.Map<CharacterSkillsMsg>(body.skills),
			Health = _mapper.Map<CharacterHealthMsg>(body),
			// Wire encoding is handSlot + 1 (0 = none) — protobuf-net omits
			// 0-valued ints, and hand slot 0 is valid (see CharacterDataMsg.HandSlot).
			HandSlot = body.handSlot + 1,
			// The reconnect restore returns the character to its LEAVE spot, not
			// the fresh world's landing spot.
			Position = new NetVector2Msg(body.transform.position.x, body.transform.position.y),
		};

		// Limb has no Index field — Mapster maps the rest, the loop assigns it.
		for (var i = 0; i < body.limbs.Length; i++)
		{
			var limbMsg = _mapper.Map<CharacterLimbMsg>(body.limbs[i]);
			limbMsg.Index = i;
			msg.Limbs.Add(limbMsg);
		}

		// Items: id ↔ ItemId is a rename, not a case variant — keep it manual.
		// Capture is recursive: container contents ride inside the parent item
		// (Contents), and [Saveable] component state (liquids, batteries, ammo,
		// …) rides along — the wire form of the official save's SavedItem +
		// component dictionaries (SaveSystem.SaveGame), so a restore is complete.
		for (var slot = 0; slot < body.slots.Length; slot++)
		{
			var item = body.GetItem(slot);
			if (item == null) // Unity object — ==
			{
				continue;
			}

			msg.Items.Add(ItemStateCodec.CaptureItem(item, slot));
		}

		// Wearables: items worn on body parts (mouth/hat/back/eyes… —
		// WearWearable parents them to the limb, Body.cs:1508), which are NOT
		// backpack slots — without this pass a worn item (e.g. a plastic chunk
		// held in the mouth) shows on the peer's clone as "still carried".
		// SlotIndex encodes the limb: -(limbIndex + 2) — negative, so it can
		// never collide with a real slot.
		for (var i = 0; i < body.limbs.Length; i++)
		{
			var limb = body.limbs[i].transform;
			for (var c = 0; c < limb.childCount; c++)
			{
				var worn = limb.GetChild(c).GetComponent<Item>();
				if (worn != null) // Unity object — ==
				{
					msg.Items.Add(ItemStateCodec.CaptureItem(worn, -(i + 2)));
				}
			}
		}

		return msg;
	}

	private void ApplyRestoredStatsAndWipe(Body body, CharacterDataMsg data)
	{
		_log.LogInformation("Applying character restore ({Items} items).", data.Items.Count);

		if (data.Position is { } pos)
		{
			// The disconnect spot — applied before the wipe (the position has no
			// Destroy semantics to wait for). Zero velocity: the body must not
			// keep the fresh spawn's momentum into the restored spot.
			body.transform.position = new Vector3(pos.X, pos.Y, 0f);
			body.rb.velocity = Vector2.zero;
			_log.LogInformation("Character restore position: ({X:F1},{Y:F1}).", pos.X, pos.Y);
		}

		// Wipe the fresh-run default state first: this new run already got its
		// starting supplies (WorldGeneration.WorldPlacePlayer) and random vitals
		// (Body.Start) — restoring on top would duplicate items and leave
		// random hunger/thirst. Destroy is end-of-frame; the items are re-added
		// on the next frame (TryApplyCharacterRestore's second pass), so the
		// slots are actually empty when PickUpItem runs — it silently refuses
		// a non-empty slot (Body.cs:1388) and the item would be stranded.
		for (var slot = 0; slot < body.slots.Length; slot++)
		{
			var holder = body.slots[slot].transform;
			for (var i = holder.childCount - 1; i >= 0; i--)
			{
				UnityEngine.Object.Destroy(holder.GetChild(i).gameObject);
			}
		}

		if (data.Skills is { } skills)
		{
			_mapper.Map(skills, body.skills);
			body.skills.UpdateExpBoundaries(); // min/max derive from STR/RES/INT (Skills.cs:61)
		}

		if (data.Health is { } health)
		{
			// Target-driven: only writable Body members that exist in the source
			// are touched — alive/conscious (derived properties, Body.cs:203/213)
			// are read-only and skipped automatically.
			_mapper.Map(health, body);
		}

		foreach (var limbData in data.Limbs)
		{
			if (limbData.Index < 0 || limbData.Index >= body.limbs.Length)
			{
				continue;
			}

			_mapper.Map(limbData, body.limbs[limbData.Index]);
		}
	}

	private void ApplyRestoredItems(Body body, CharacterDataMsg data)
	{
		foreach (var itemData in data.Items)
		{
			if (itemData.SlotIndex < 0)
			{
				RestoreWearable(itemData, body);
			}
			else
			{
				ItemStateCodec.RestoreItem(itemData, body);
			}
		}

		var handSlot = data.HandSlot - 1; // wire encoding: handSlot + 1
		if (handSlot >= 0 && handSlot < body.slots.Length)
		{
			body.handSlot = handSlot;
		}
	}

	/// <summary>
	/// Restore a worn item onto its limb (mirrors WearWearable, Body.cs:1480:
	/// parented to the limb, physics off, identity pose). The limb comes from
	/// the captured negative SlotIndex — the restore path never had the item
	/// in a backpack, so the game's slot-driven wear flow cannot run.
	/// </summary>
	private void RestoreWearable(CharacterItemMsg itemData, Body body)
	{
		var limbIndex = -itemData.SlotIndex - 2;
		if (limbIndex < 0 || limbIndex >= body.limbs.Length)
		{
			_log.LogWarning("Restore: worn {ItemId} has limb index {Limb} out of range — skipped.", itemData.ItemId, limbIndex);
			return;
		}

		var go = UnityEngine.Object.Instantiate((GameObject)Resources.Load(itemData.ItemId),
			body.transform.position, Quaternion.identity);
		var item = go.GetComponent<Item>();
		if (item == null) // Unity object — ==
		{
			UnityEngine.Object.Destroy(go);
			_log.LogWarning("Restore: {ItemId} has no Item component — skipped.", itemData.ItemId);
			return;
		}

		if (itemData.InstanceId != 0)
		{
			// Identity restore — same rationale as ItemStateCodec.RestoreItem:
			// the reconnect-merge ids keep the restored item the SAME instance
			// the host knows (an id-less restore reads as a runtime spawn).
			item.gameObject.AddComponent<ItemInstanceId>().Id = itemData.InstanceId;
		}

		item.condition = itemData.Condition;
		item.favourited = itemData.Favourited;
		ItemStateCodec.RestoreLiquids(item, itemData.Liquids);
		ItemStateCodec.RestoreComponentStates(item, itemData.Components);
		ItemStateCodec.RestoreContents(item, itemData.Contents);

		var limb = body.limbs[limbIndex];
		item.rb.simulated = false;
		item.transform.SetParent(limb.transform);
		item.transform.localScale = Vector3.one;
		item.transform.localRotation = Quaternion.identity;
		item.transform.localPosition = Vector3.zero;
		var sr = item.GetComponent<SpriteRenderer>();
		if (sr != null) // Unity object — ==
		{
			sr.sortingOrder = limb.GetComponent<SpriteRenderer>().sortingOrder + item.Stats.wearableVisualOffset;
		}
	}
}
