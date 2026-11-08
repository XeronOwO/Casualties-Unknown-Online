using System;
using BepInEx;
using CasualtiesUnknownOnline.PinyinSearch.Core;
using HarmonyLib;
using UnityEngine;

namespace CasualtiesUnknownOnline.PinyinSearch;

/// <summary>
/// The plug-in shell: it exists so BepInEx loads this assembly, and with CUO
/// uninstalled it is the whole mod — the crafting search box is extended from
/// here. Its Awake touches BepInEx, Unity and the game only, never a CUO API
/// (the CUO-facing types live in the Core assembly and are driven by CUO's own
/// lifecycle, "How a mod is loaded" in <c>docs/en/reference/mod-api.md</c>), so it may run before or after the
/// CUO plug-in's Awake.
/// </summary>
[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public sealed class PinyinSearchPlugin : BaseUnityPlugin
{
	/// <summary>The Harmony owner id, so this mod's patch set is identifiable in the log.</summary>
	internal const string HarmonyId = "CasualtiesUnknownOnline.PinyinSearch";

	/// <summary>
	/// The default for a player who has never touched the setting: a Simplified
	/// Chinese desktop gets pinyin search, every other system language keeps the
	/// native search untouched. BepInEx writes the default into the config file
	/// the first time it is created, so an explicit choice always wins.
	/// </summary>
	private static bool DefaultEnabled => Application.systemLanguage == SystemLanguage.ChineseSimplified;

	private void Awake()
	{
		PinyinSearchConfig.Bind(Config, DefaultEnabled);
		PinyinSearchGate.Bind(Logger);

		try
		{
			// PatchAll(Type) patches only that type's own methods; the patch
			// classes are NESTED containers with [HarmonyPatch] attributes, so
			// the whole assembly is what has to be scanned. Measured 2026-11-08:
			// the Type overload reported success while applying no patch at all,
			// and the search box kept the native filter.
			new Harmony(HarmonyId).PatchAll(typeof(PinyinSearchPlugin).Assembly);
			Logger.LogInfo($"[Pinyin] crafting search patches applied (enabled: {PinyinSearchConfig.IsEnabled}).");
		}
		catch (Exception ex)
		{
			Logger.LogError($"[Pinyin] failed to apply the crafting search patches: {ex.GetType().Name}: {ex.Message}");
			Logger.LogError($"Stack: {ex.StackTrace}");
		}
	}
}
