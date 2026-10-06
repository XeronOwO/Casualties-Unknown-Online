using HarmonyLib;

using CasualtiesUnknownOnline.GameAdapter.Items;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Container ownership hooks: an item entering or leaving a world container
/// (ground crates, dropped backpacks). LoadItem/UnloadItem are the drag-UI
/// move operations; UnloadAllItems (the container-broke spill, Container.cs:
/// 46-66) sets parents directly without UnloadItem and needs its own hook.
/// The adapter reports the item's final home — a world container entry keeps
/// the item in the world-item table (its contents must be visible to others).
/// </summary>
internal static class ContainerItemPatches
{
	[HarmonyPatch(typeof(Container), "LoadItem")]
	internal static class ContainerLoadItemPatch
	{
		// Only a load that actually landed (LoadItem's CanHoldItem/distance guard
		// can fail and leave the item untouched). WasWorldItem is captured in the
		// prefix (carried to the postfix via Harmony __state — per-call state,
		// never a static field): dragging a GROUND item into a body-side
		// container (a bag in your inventory) loads it without PickUpItem, so
		// the world-item copy would stay on the peer unless the adapter knows it
		// left the world.
		private static void Prefix(Item item, out bool __state) => __state = ItemWorldSync.IsWorldItem(item);

		private static void Postfix(Container __instance, Item item, bool __state)
		{
			// A product loading into a surface container during a craft (the
			// AutoPickUpItem container path) rides the ONE craft report — the
			// coordinator's inventory diff sees it.
			if (CallContext.Current != CallContext.Origin.Craft
				&& !RemoteCloneContainerGuard.IsDisplayProxy(__instance)
				&& item.transform.parent == __instance.transform)
			{
				PatchBridge.Impl?.OnItemLoadedIntoContainer(item, __state);
			}
		}
	}

	[HarmonyPatch(typeof(Container), "UnloadItem")]
	internal static class ContainerUnloadItemPatch
	{
		// Only an unload that actually happened: UnloadItem is a no-op when the
		// item is not inside this container (the drag-drop path calls it on the
		// dragged item unconditionally — PlayerCamera.cs:1567 — before loading
		// it elsewhere), and the old Postfix reported the no-op as "unloaded
		// into the world", which materialized a phantom drop on the peer.
		// BOTH facts the postfix needs are captured HERE, before the mutation:
		// whether the item was inside this container (the landed check), and
		// whether it was part of the WORLD before the detach — the fact that
		// classifies the container-to-container move the rest of the bracket may
		// complete (PlayerCamera.cs:1589-1590), because after SetParent(null)
		// the scene can no longer answer where the item came from.
		private static void Prefix(Container __instance, Item item, out UnloadState __state) =>
			__state = new UnloadState(item.transform.parent == __instance.transform, ItemWorldSync.IsWorldItem(item));

		private static void Postfix(Container __instance, Item item, UnloadState __state)
		{
			if (__state.WasInside && !RemoteCloneContainerGuard.IsDisplayProxy(__instance) && item.transform.parent != __instance.transform)
			{
				PatchBridge.Impl?.OnItemUnloadedFromContainer(item, __state.WasWorldItem);
			}
		}

		/// <summary>Per-call state across the patch pair (Harmony __state — never a static field).</summary>
		private readonly record struct UnloadState(bool WasInside, bool WasWorldItem);
	}

	[HarmonyPatch(typeof(Container), "UnloadAllItems")]
	internal static class ContainerUnloadAllItemsPatch
	{
		private static void Postfix(Container __instance)
		{
			if (!RemoteCloneContainerGuard.IsDisplayProxy(__instance))
			{
				PatchBridge.Impl?.OnContainerUnloadedAll(__instance);
			}
		}
	}
}
