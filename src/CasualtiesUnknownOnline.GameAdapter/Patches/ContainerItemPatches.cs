using HarmonyLib;

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
		// can fail and leave the item untouched).
		private static void Postfix(Container __instance, Item item)
		{
			if (item.transform.parent == __instance.transform)
			{
				PatchBridge.Impl?.OnItemLoadedIntoContainer(item);
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
		private static void Prefix(Container __instance, Item item, ref bool wasInside) =>
			wasInside = item.transform.parent == __instance.transform;

		private static void Postfix(Container __instance, Item item, bool wasInside)
		{
			if (wasInside && item.transform.parent != __instance.transform)
			{
				PatchBridge.Impl?.OnItemUnloadedFromContainer(item);
			}
		}
	}

	[HarmonyPatch(typeof(Container), "UnloadAllItems")]
	internal static class ContainerUnloadAllItemsPatch
	{
		private static void Postfix(Container __instance) => PatchBridge.Impl?.OnContainerUnloadedAll(__instance);
	}
}
