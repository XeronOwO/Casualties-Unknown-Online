using HarmonyLib;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// GameAdapter hooks for mod-bound liquid-tile world bytes. They are thin
/// adapters: custom bytes get local projection (render/color/water/name/drink)
/// while every vanilla byte keeps the original path. The authoritative fluid
/// grid still rides CUO's existing FluidRegion/FluidInteraction stream.
/// </summary>
internal static class FluidCustomLiquidPatches
{
	[HarmonyPatch(typeof(FluidManager), nameof(FluidManager.RenderFluids))]
	internal static class RenderCustomLiquidsPatch
	{
		private static bool Prefix(FluidManager __instance)
		{
			if (PatchBridge.Impl is not { } bridge)
			{
				return true;
			}

			return !bridge.TryRenderCustomLiquids(__instance);
		}
	}

	[HarmonyPatch(typeof(FluidManager), nameof(FluidManager.LiquidColor))]
	internal static class CustomLiquidColorPatch
	{
		private static bool Prefix(FluidManager __instance, Vector2Int pos, ref Color __result)
		{
			if (PatchBridge.Impl is not { } bridge)
			{
				return true;
			}

			var worldByte = __instance.GetLiquid(pos.x, pos.y);
			if (bridge.TryGetCustomLiquidColor(worldByte, out var color))
			{
				__result = color;
				return false;
			}

			return true;
		}
	}

	[HarmonyPatch(typeof(FluidManager), nameof(FluidManager.WaterInfo))]
	internal static class CustomLiquidWaterInfoPatch
	{
		private static void Postfix(FluidManager __instance, Vector2Int pos,
			ref (float buoyancy, float drag, int type) __result)
		{
			if (PatchBridge.Impl is not { } bridge)
			{
				return;
			}

			var worldByte = __instance.GetLiquid(pos.x, pos.y);
			if (bridge.TryGetCustomWaterInfo(worldByte, out var buoyancy, out var drag, out var type))
			{
				__result = (buoyancy, drag, type);
			}
		}
	}

	[HarmonyPatch(typeof(FluidManager), nameof(FluidManager.LiquidName))]
	internal static class CustomLiquidNamePatch
	{
		private static void Postfix(FluidManager __instance, Vector2Int pos,
			ref (string, string) __result)
		{
			if (PatchBridge.Impl is not { } bridge)
			{
				return;
			}

			var worldByte = __instance.GetLiquid(pos.x, pos.y);
			if (bridge.TryGetCustomLiquidName(worldByte, out var name, out var description))
			{
				__result = (name, description);
			}
		}
	}

	[HarmonyPatch(typeof(FluidManager), nameof(FluidManager.DrinkLiquid))]
	internal static class CustomLiquidDrinkPatch
	{
		private static bool Prefix(FluidManager __instance, Vector2Int pos, Body body)
		{
			if (PatchBridge.Impl is not { } bridge)
			{
				return true;
			}

			return !bridge.TryDrinkCustomLiquid(__instance, pos, body);
		}
	}

	[HarmonyPatch(typeof(Body), "HandleVariableUpdates")]
	internal static class LiquidTileBodyTouchPatch
	{
		private static void Postfix(Body __instance) =>
			PatchBridge.Impl?.ApplyLiquidTileBodyTouch(__instance);
	}
}
