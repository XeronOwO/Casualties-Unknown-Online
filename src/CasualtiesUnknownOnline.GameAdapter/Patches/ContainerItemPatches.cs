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
		private static void Postfix(Item item) => PatchBridge.Impl?.OnItemLoadedIntoContainer(item);
	}

	[HarmonyPatch(typeof(Container), "UnloadItem")]
	internal static class ContainerUnloadItemPatch
	{
		private static void Postfix(Item item) => PatchBridge.Impl?.OnItemUnloadedFromContainer(item);
	}

	[HarmonyPatch(typeof(Container), "UnloadAllItems")]
	internal static class ContainerUnloadAllItemsPatch
	{
		private static void Postfix(Container __instance) => PatchBridge.Impl?.OnContainerUnloadedAll(__instance);
	}
}
