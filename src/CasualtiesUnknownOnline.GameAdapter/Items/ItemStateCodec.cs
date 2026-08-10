using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;
using UnityEngine;
using Log = Microsoft.Extensions.Logging.ILogger;

namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// Item ↔ wire-form state codec: the complete item state — SavedItem fields
/// (condition/favourited/slot), the WaterContainerItem liquid stacks, the
/// [Saveable] component states and the container contents — the wire form of
/// the official save's SavedItem + component dictionaries (SaveSystem.SaveGame),
/// so a capture round-trips a restore exactly. Shared by the character-data
/// domain (save/restore) and the world-item domain (spawn/drop reports).
/// Pure conversion, no state.
/// </summary>
internal static class ItemStateCodec
{
	private static Log? _log;

	/// <summary>Late-bound logger (the domains pass theirs in once at startup).</summary>
	internal static void BindLog(Log log) => _log = log;

	/// <summary>Recursively captures one item: the instance id, the SavedItem
	/// fields (condition/favourited/slot), the WaterContainerItem liquid stacks,
	/// the [Saveable] component states and the container contents.</summary>
	internal static CharacterItemMsg CaptureItem(Item item, int slotIndex)
	{
		var msg = new CharacterItemMsg
		{
			InstanceId = item.GetComponent<ItemInstanceId>()?.Id ?? 0, // Unity object — ?. (0 = unbound generation-time item)
			ItemId = item.id,
			Condition = item.condition,
			SlotIndex = slotIndex,
			Favourited = item.favourited,
			Liquids = CaptureLiquids(item),
			Components = CaptureSaveableComponents(item),
		};

		var container = item.GetComponent<Container>();
		if (container != null) // Unity object — ==
		{
			for (var i = 0; i < container.transform.childCount; i++)
			{
				var child = container.transform.GetChild(i).GetComponent<Item>();
				if (child != null) // Unity object — ==
				{
					msg.Contents.Add(CaptureItem(child, slotIndex));
				}
			}
		}

		return msg;
	}

	/// <summary>
	/// The digest form of an item — the action-report evidence: full top-level
	/// state (condition/liquids/components/favourited) but the contents as
	/// INSTANCE IDS ONLY (existence, never their state — that travels when the
	/// content itself is acted on, the recursive principle). Light on the wire
	/// and exactly the surface the host's evidence check compares. The slot
	/// rides the evidence so the host's transfer-table entry gets a real slot
	/// at pickup (a carried item's slot is its owner's local fact — the host
	/// adopts it, never corrects it).
	/// </summary>
	internal static CharacterItemMsg CaptureDigest(Item item, int slotIndex = -1)
	{
		var msg = new CharacterItemMsg
		{
			InstanceId = item.GetComponent<ItemInstanceId>()?.Id ?? 0,
			ItemId = item.id,
			Condition = item.condition,
			SlotIndex = slotIndex,
			Favourited = item.favourited,
			Liquids = CaptureLiquids(item),
			Components = CaptureSaveableComponents(item),
		};

		var container = item.GetComponent<Container>();
		if (container != null) // Unity object — ==
		{
			for (var i = 0; i < container.transform.childCount; i++)
			{
				var child = container.transform.GetChild(i).GetComponent<Item>();
				if (child != null) // Unity object — ==
				{
					msg.Contents.Add(new CharacterItemMsg
					{
						InstanceId = child.GetComponent<ItemInstanceId>()?.Id ?? 0,
					});
				}
			}
		}

		return msg;
	}

	/// <summary>The WaterContainerItem's liquid stacks — a public field
	/// (WaterContainerItem.cs:347), read directly for the round-trip symmetry
	/// with the restore (a game rename is a compile error, not a silent drop).</summary>
	private static List<LiquidStackMsg> CaptureLiquids(Item item)
	{
		var water = item.GetComponent<WaterContainerItem>();
		if (water == null) // Unity object — ==
		{
			return [];
		}

		return [.. water.stack.Select(s => new LiquidStackMsg
		{
			LiquidId = s.liquidId,
			Amount = s.amount,
		})];
	}

	/// <summary>Snapshots every [Saveable] component's simple-typed state —
	/// the wire form of the official save's per-item component dictionaries —
	/// plus the state whitelist: components the official save does not persist
	/// but multiplayer syncs (CustomItemBehaviour — the flashlight's on/off
	/// state). Unity-reference fields are never serialized; WaterContainerItem
	/// is skipped (its state travels as Liquids).</summary>
	private static List<ComponentStateMsg> CaptureSaveableComponents(Item item)
	{
		var states = new List<ComponentStateMsg>();
		foreach (var comp in item.GetComponents<Component>())
		{
			if (comp is WaterContainerItem) // Unity object — ==
			{
				continue; // handled by CaptureLiquids
			}

			// The state whitelist: components the official save does not persist
			// but multiplayer syncs (CustomItemBehaviour.state — flashlight
			// modes). The field rules below only admit public simple-typed
			// fields, so state (int) travels while the object[] data and the
			// Item reference stay out.
			if (comp.GetType().GetCustomAttribute<Saveable>(inherit: false) is null
				&& comp is not CustomItemBehaviour)
			{
				continue;
			}

			var fields = new List<ComponentFieldMsg>();
			foreach (var field in comp.GetType().GetFields(
				BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
			{
				if (field.IsStatic || field.IsInitOnly)
				{
					continue;
				}

				// Private state must be explicitly marked for serialization
				// (the Unity serializer's rule, which the game relies on).
				if (!field.IsPublic && field.GetCustomAttribute<SerializeField>() is null)
				{
					continue;
				}

				var kind = ComponentFieldKind(field.FieldType);
				if (kind == 0)
				{
					continue; // unsupported kind (Unity references, custom types)
				}

				var value = field.GetValue(comp);
				fields.Add(new ComponentFieldMsg
				{
					Name = field.Name,
					Kind = kind,
					FloatValue = kind == 1 ? (float)value! : 0f,
					IntValue = kind == 2 ? (int)value! : 0,
					BoolValue = kind == 3 && (bool)value!,
					StringValue = kind == 4 ? (string)value! : "",
					StringList = kind == 5 ? (List<string>)value! : [],
				});
			}

			states.Add(new ComponentStateMsg { TypeName = comp.GetType().Name, Fields = fields });
		}

		return states;
	}

	private static int ComponentFieldKind(Type type)
	{
		if (type == typeof(float))
		{
			return 1;
		}

		if (type == typeof(int))
		{
			return 2;
		}

		if (type == typeof(bool))
		{
			return 3;
		}

		if (type == typeof(string))
		{
			return 4;
		}

		if (type == typeof(List<string>))
		{
			return 5;
		}

		return 0;
	}

	/// <summary>Restores one item (recursively): instantiate by id, apply the
	/// SavedItem fields, the liquid stacks, the component states and the
	/// container contents, then hand it to the slot — with the game's own
	/// restore semantics (SaveSystem.cs:304-329): a non-empty slot takes the
	/// item into its container instead of failing.</summary>
	internal static void RestoreItem(CharacterItemMsg itemData, Body body)
	{
		if (itemData.SlotIndex < 0 || itemData.SlotIndex >= body.slots.Length)
		{
			return;
		}

		var go = UnityEngine.Object.Instantiate((GameObject)Resources.Load(itemData.ItemId),
			body.transform.position, Quaternion.identity);
		var item = go.GetComponent<Item>();
		if (item == null) // Unity object — ==
		{
			UnityEngine.Object.Destroy(go);
			_log?.LogWarning("Restore: {ItemId} has no Item component — skipped.", itemData.ItemId);
			return;
		}

		item.condition = itemData.Condition;
		item.favourited = itemData.Favourited;
		RestoreLiquids(item, itemData.Liquids);
		RestoreComponentStates(item, itemData.Components);
		RestoreContents(item, itemData.Contents);

		if (body.HoldingItem(itemData.SlotIndex))
		{
			// The slot already holds something (a restored container) — the
			// item goes inside it (SaveSystem semantics, Body.cs:1388 would
			// silently refuse the slot otherwise).
			body.GetItem(itemData.SlotIndex).GetComponent<Container>()?.LoadItem(item);
		}
		else
		{
			body.PickUpItem(item, itemData.SlotIndex, force: true);
		}
	}

	internal static void RestoreLiquids(Item item, List<LiquidStackMsg> liquids)
	{
		var water = item.GetComponent<WaterContainerItem>();
		if (water == null) // Unity object — ==
		{
			return;
		}

		// Rebuild the stack directly instead of AddLiquid-ing: the prefab's
		// Awake already filled the default contents (WaterContainerItem.Awake),
		// so an additive restore reads "full" again. The capture side reads the
		// same public field, so this round-trips exactly (including an empty
		// stack).
		water.stack = [.. liquids.Select(l => new LiquidStack(l.LiquidId, l.Amount))];
	}

	internal static void RestoreComponentStates(Item item, List<ComponentStateMsg> states)
	{
		foreach (var state in states)
		{
			// Matched by type name: the capture side stores the component's
			// simple name, restore finds the component with that name.
			var comp = item.GetComponents<Component>()
				.FirstOrDefault(c => c.GetType().Name == state.TypeName);
			if (comp == null) // Unity object — == (FirstOrDefault on destroyed)
			{
				_log?.LogWarning("Restore: component {Type} not found on {Item} — its state is skipped.", state.TypeName, item.id);
				continue;
			}

			foreach (var field in state.Fields)
			{
				var target = comp.GetType().GetField(field.Name,
					BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
				if (target is null || target.IsStatic || target.IsInitOnly)
				{
					continue;
				}

				switch (field.Kind)
				{
					case 1:
						target.SetValue(comp, field.FloatValue);
						break;
					case 2:
						target.SetValue(comp, field.IntValue);
						break;
					case 3:
						target.SetValue(comp, field.BoolValue);
						break;
					case 4:
						target.SetValue(comp, field.StringValue);
						break;
					case 5:
						target.SetValue(comp, field.StringList);
						break;
				}
			}
		}
	}

	internal static void RestoreContents(Item containerItem, List<CharacterItemMsg> contents)
	{
		if (contents.Count == 0)
		{
			return;
		}

		var container = containerItem.GetComponent<Container>();
		if (container == null) // Unity object — ==
		{
			return;
		}

		foreach (var childData in contents)
		{
			RestoreContent(containerItem, container, childData);
		}
	}

	/// <summary>
	/// Materializes ONE content item into a container (the shared step of the
	/// bulk restore and the correction apply): instantiate by definition,
	/// restore the state, attach the instance id (a correction-addressed item
	/// must be findable by id afterwards — "the corrected contents vanished on
	/// the next action") and load it with the game's own semantics.
	/// </summary>
	internal static void RestoreContent(Item containerItem, Container container, CharacterItemMsg childData)
	{
		var go = UnityEngine.Object.Instantiate((GameObject)Resources.Load(childData.ItemId),
			containerItem.transform.position, Quaternion.identity);
		var child = go.GetComponent<Item>();
		if (child == null) // Unity object — ==
		{
			UnityEngine.Object.Destroy(go);
			_log?.LogWarning("Restore: {ItemId} has no Item component — skipped.", childData.ItemId);
			return;
		}

		child.condition = childData.Condition;
		child.favourited = childData.Favourited;
		if (childData.InstanceId != 0)
		{
			child.gameObject.AddComponent<ItemInstanceId>().Id = childData.InstanceId;
		}

		RestoreLiquids(child, childData.Liquids);
		RestoreComponentStates(child, childData.Components);
		RestoreContents(child, childData.Contents);
		container.LoadItem(child);
	}
}
