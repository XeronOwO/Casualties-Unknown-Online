using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.GameAdapter.Rendering;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using MapsterMapper;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter;

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
	ILogger<CharacterDataSync> log)
{
	private readonly SessionService _session = session;
	private readonly CharacterDataStore _characterData = characterData;
	private readonly IMapper _mapper = mapper;
	private readonly ILogger<CharacterDataSync> _log = log;

	/// <summary>SteamId → latest character snapshot: the remote clone's inventory rendering source.</summary>
	private readonly Dictionary<ulong, CharacterDataMsg> _cloneData = [];

	/// <summary>A clone's snapshot cache updated (SteamId) — the renderer re-renders that clone's carried items. Without this, the clone only rendered once at creation ("after the starting supplies, the peer never sees carried-item updates").</summary>
	public event Action<ulong>? CloneSnapshotUpdated;

	private CharacterDataMsg? _pendingRestore; // guest side: host-sent restore, applied once the body exists
	private bool _restoreWipePending; // first pass wiped the slots (Destroy is end-of-frame) — items go in on the next frame
	private const float CharacterReportInterval = 1f; // guest → host character snapshot (1 Hz)
	private long _nextCharacterReportMs;

	/// <summary>Read-only view for the clone renderer: latest snapshot per SteamId.</summary>
	internal IReadOnlyDictionary<ulong, CharacterDataMsg> CloneData => _cloneData;

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
			_cloneData[sender] = data;
			CloneSnapshotUpdated?.Invoke(sender);
			_log.LogInformation("[CloneRender] host: char data from {Sender} ({Count} items).", sender, data.Items.Count);
			return;
		}

		// May arrive before the local body exists (still loading the run) —
		// apply once the game has spawned it (TryApplyCharacterRestore).
		_pendingRestore = data;
		_log.LogInformation("Received character restore ({Items} items).", data.Items.Count);
	}

	/// <summary>Guest side: the host's own 1 Hz snapshot — render its clone's inventory (never applied to the local body).</summary>
	private void OnHostCharacterDataReceived(CharacterDataMsg data)
	{
		_cloneData[_session.HostSteamId] = data;
		CloneSnapshotUpdated?.Invoke(_session.HostSteamId);
		_log.LogInformation("[CloneRender] guest: host char data ({Count} items).", data.Items.Count);
	}

	/// <summary>
	/// Render a remote clone's carried state from its owner's character
	/// snapshot: each slot shows the carried item's prefab, and each worn item
	/// (negative SlotIndex — limb-encoded) renders on the matching limb
	/// (mouth/hat/back…, the game parents them there too, Body.cs:1508). Pure
	/// display — physics off, non-interactive, no instance id. Every render
	/// re-created from the snapshot: matching items stay, changed ones swap,
	/// the emptied disappear. Called by the clone renderer when a clone appears
	/// and when a snapshot updates.
	/// </summary>
	internal void ApplyCloneInventory(Body clone, CharacterDataMsg data)
	{
		_log.LogInformation("[CloneRender] apply {Count} items to clone slots ({Slots} slots).", data.Items.Count, clone.slots.Length);
		foreach (var slot in clone.slots)
		{
			if (slot == null) // Unity object — ==
			{
				continue;
			}

			var wanted = data.Items.FirstOrDefault(x => x.SlotIndex == slot.slot);
			RenderItemInto(slot.transform, wanted, slot.spriteSortOrder, wearLimb: null);
		}

		for (var i = 0; i < clone.limbs.Length; i++)
		{
			var limb = clone.limbs[i];
			if (limb == null) // Unity object — ==
			{
				continue;
			}

			var worn = data.Items.FirstOrDefault(x => x.SlotIndex == -(i + 2));
			RenderItemInto(limb.transform, worn, 0, wearLimb: limb);
		}
	}

	/// <summary>
	/// Materialize one snapshot item into a render parent. Slot parents are
	/// fully cleared (a slot only ever holds items); limb parents keep the
	/// game's own children (bones/decorations) and clear only our previous
	/// renders (RemoteCloneRender-marked).
	/// </summary>
	private static void RenderItemInto(Transform parent, CharacterItemMsg? wanted, int sortOrder, Limb? wearLimb)
	{
		if (wearLimb == null)
		{
			// Clear EVERY child, then materialize the wanted item: the diff
			// used to inspect only GetChild(0), so a slot that accumulated
			// more than one child (template leftover + render, or repeated
			// renders) kept the strays — peers saw duplicate carried items
			// appear after inventory shuffling.
			for (var c = parent.childCount - 1; c >= 0; c--)
			{
				UnityEngine.Object.Destroy(parent.GetChild(c).gameObject);
			}
		}
		else
		{
			for (var c = parent.childCount - 1; c >= 0; c--)
			{
				var child = parent.GetChild(c);
				if (child.GetComponent<RemoteCloneRender>() != null) // Unity object — ==
				{
					UnityEngine.Object.Destroy(child.gameObject);
				}
			}
		}

		if (wanted is null)
		{
			return;
		}

		var prefab = Resources.Load(wanted.ItemId);
		if (prefab == null) // Unity object — ==
		{
			return;
		}

		var obj = UnityEngine.Object.Instantiate(prefab, parent) as GameObject;
		obj!.transform.localPosition = Vector3.zero;
		var item = obj.GetComponent<Item>();
		obj.transform.localEulerAngles = wearLimb != null
			? Vector3.zero // the game wears with identity rotation (Body.cs:1510)
			: new Vector3(0f, 0f, item.Stats.slotRotation);
		if (item.rb != null) // Unity object — ==
		{
			item.rb.simulated = false; // pure display
		}

		var col = obj.GetComponent<Collider2D>();
		if (col != null) // Unity object — ==
		{
			col.enabled = false; // never pickable/blocking
		}

		var sr = obj.GetComponent<SpriteRenderer>();
		if (sr != null) // Unity object — ==
		{
			// Wear order mirrors the game (Body.cs:1507): limb sprite order +
			// the item's wearable visual offset.
			sr.sortingOrder = wearLimb != null
				? wearLimb.GetComponent<SpriteRenderer>().sortingOrder + item.Stats.wearableVisualOffset
				: sortOrder;
		}

		if (wearLimb != null)
		{
			obj.AddComponent<RemoteCloneRender>(); // the marker the next pass clears (never the game's own children)
		}
	}

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
			HandSlot = body.handSlot,
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

		if (data.HandSlot >= 0 && data.HandSlot < body.slots.Length)
		{
			body.handSlot = data.HandSlot;
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
