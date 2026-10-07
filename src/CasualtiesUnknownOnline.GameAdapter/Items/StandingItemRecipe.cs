using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// How a standing item object is BUILT (ticket <c>mod-cross-player-solid-food-semantics</c>, §6.2/§6.3):
/// the definition prefab, live, with its instance id and its <see cref="StandingItemObject"/> marker
/// attached in the same frame — before <c>Item.Start</c> runs — parented to the CUO holder, carrying the
/// row's state, and with every surface that could touch the world or a player switched off.
///
/// <para>
/// It is a seam of its own because it is the recipe the §7 acceptance batch reads: which switches are
/// thrown and which components are disabled is a decision, not an implementation detail, and keeping it
/// in one small class means the source-shape gate pins exactly that (<c>StandingItemGateTests</c>). The
/// materializer owns WHEN an object is built, this owns WHAT it is. See
/// <see cref="StandingItemMaterializer"/> for why the parent is a plain CUO holder and never a real
/// <c>Container</c> (§6 constraint 5).
/// </para>
/// </summary>
internal static class StandingItemRecipe
{
	/// <summary>Light2D lives in the URP runtime assembly, which this project does not reference — matched by name, the same convention the clone proxy renderer uses.</summary>
	private const string Light2DTypeName = "Light2D";

	/// <summary>
	/// Build the local incarnation of one carried row. Returns null with the reason when the local scene
	/// cannot serve the definition (no prefab, a failed instantiate, a prefab without an <c>Item</c>).
	/// </summary>
	internal static Item? Build(Transform holder, ulong itemId, CharacterItemMsg data, out string? failure)
	{
		failure = null;
		var prefab = ItemPrefabResolver.Load(data.ItemId);
		if (prefab == null) // Unity object — ==
		{
			failure = "the definition has no prefab on this side";
			return null;
		}

		var obj = Object.Instantiate(prefab, Vector3.zero, Quaternion.identity) as GameObject;
		if (obj == null) // Unity object — ==
		{
			failure = "instantiate returned null";
			return null;
		}

		obj.SetActive(true); // the cached custom template is inactive; every item instance must be live
		var item = obj.GetComponent<Item>();
		if (item == null) // Unity object — ==
		{
			Object.Destroy(obj);
			failure = "the prefab carries no Item";
			return null;
		}

		// The id AND the marker ride the same frame as the instantiation, so Item.Start's hook
		// (ItemWorldSync.OnItemInstantiated) sees an object that is already in the domain and already
		// classified — the §3 constraint, now belt and braces on top of the category.
		obj.AddComponent<ItemInstanceId>().Id = itemId;
		obj.AddComponent<StandingItemObject>();
		obj.transform.SetParent(holder, worldPositionStays: false);

		item.condition = data.Condition; // direct write, like the world materialization (SetCondition would drain water by ratio)
		item.favourited = data.Favourited;
		ItemStateCodec.RestoreLiquids(item, data.Liquids);
		ItemStateCodec.RestoreComponentStates(item, data.Components);
		MakeInert(obj);
		return item;
	}

	/// <summary>
	/// Turn the object's world-facing surface off (§6.2/§6.3's recipe). Every component here is DISABLED
	/// rather than removed, and disabled before its <c>Start</c> would have run: a disabled component
	/// neither starts nor ticks, while the data path keeps working — <c>ItemStateCodec</c> reads and writes
	/// those components' fields directly, so capture and restore never needed their <c>Update</c>.
	/// </summary>
	internal static void MakeInert(GameObject obj)
	{
		var rb = obj.GetComponent<Rigidbody2D>();
		if (rb != null) // Unity object — ==
		{
			rb.simulated = false; // the game's own "not in the world" switch (Item.Update sets it for a parentless item off-chunk)
		}

		// Every collider in the subtree: out of the hover probe, the drag start, the recipe sweep and every
		// unmasked explosion query at once (a disabled collider still feeds Body.DoPickupCheck's bounds read).
		foreach (var collider in obj.GetComponentsInChildren<Collider2D>(true))
		{
			collider.enabled = false;
		}

		// Every RENDERER in the subtree, not just the sprite: a parked copy must draw nothing, and an item
		// prefab may present itself through a LineRenderer, a trail or a particle renderer just as well.
		foreach (var renderer in obj.GetComponentsInChildren<Renderer>(true))
		{
			renderer.enabled = false;
		}

		// Disabling LightItem alone would only freeze the light at whatever the prefab authored, which for a
		// torch is ON — the light itself has to go off.
		foreach (var behaviour in obj.GetComponentsInChildren<Behaviour>(true))
		{
			if (behaviour.GetType().Name == Light2DTypeName)
			{
				behaviour.enabled = false;
			}
		}

		// The components that must not RUN on a copy of somebody else's item. Two sources decide the set:
		// §6.3's four writers of things outside the object, plus every component the census or the display
		// proxy's own path shows acting on OWNER-LOCAL state this side does not have
		// (`RemoteItemPresentation.Apply` disables three of these for a clone, and says why: "running it
		// would NRE the moment the restored fired flag is true").
		//
		//   WaterContainerItem  drinks the local fluid every frame (and deletes it)
		//   CustomItemBehaviour per-item behaviours that explode, spawn entities or destroy the item
		//   LightItem           a live light at the parked spot
		//   WatchScript         the item's own talker narrates the LOCAL player's readouts
		//   EPdaScript          re-enables its own glow renderer every frame
		//   GrapplingHook       its restored fired/pulling flags make Update dereference `this.hook`, which
		//                       only `Use` ever assigns — a copy NREs every frame
		//   GunScript           its restored trigger/rack flags make Update fire the real gun: a gunshot
		//                       sound, `Fire()` writing the LOCAL body, and a real casing/round item
		//                       instantiated into the world at the parked spot
		//   GeigerCounterAudio  its restored `active` flag plays the counter's clicks at the parked spot
		//   AutoPump            its worn flag drives the LOCAL body's blood pressure
		Disable<WaterContainerItem>(obj);
		Disable<CustomItemBehaviour>(obj);
		Disable<LightItem>(obj);
		Disable<WatchScript>(obj);
		Disable<EPdaScript>(obj);
		Disable<GrapplingHook>(obj);
		Disable<GunScript>(obj);
		Disable<GeigerCounterAudio>(obj);
		Disable<AutoPump>(obj);
	}

	private static void Disable<T>(GameObject obj)
		where T : Behaviour
	{
		foreach (var component in obj.GetComponentsInChildren<T>(true))
		{
			component.enabled = false;
		}
	}
}
