using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// Reflection-ish access to game internals without hardcoding visible signatures:
/// PreRunScript.runSettings (private instance), WorldGeneration.runSettings
/// (static) and the world-generation-in-progress flag.
/// </summary>
internal static class HarmonyTraverse
{
	public static Dictionary<string, object>? ReadRunSettings()
	{
		var worldGen = Traverse.Create(typeof(WorldGeneration)).Field("runSettings");
		if (worldGen.FieldExists() && worldGen.GetValue() is Dictionary<string, object> fromWorld)
		{
			return fromWorld;
		}

		return ReadPreRunRunSettings();
	}

	/// <summary>
	/// The menu's run settings — PreRunScript only (WorldGeneration.runSettings
	/// is assigned inside StartRun, after our entry hook). Correct at the
	/// run-start entry; the WorldGeneration field is the layer-switch source
	/// (<see cref="ReadRunSettings"/>).
	///
	/// The field is an INSTANCE member (PreRunScript.cs:429) — it must be
	/// traversed through the singleton. Traverse.Create(Type) reaches static
	/// members only, and FieldExists()/GetValue() on an instance field with no
	/// target silently fails (GetValue returns null), which made the captured
	/// run settings always null — the guest generated with its own default
	/// preset while the host played e.g. paradise (divergent worlds).
	/// </summary>
	public static Dictionary<string, object>? ReadPreRunRunSettings()
	{
		if (PreRunScript.instance == null) // Unity object — ==
		{
			return null;
		}

		var preRun = Traverse.Create(PreRunScript.instance).Field("runSettings");
		return preRun.FieldExists() && preRun.GetValue() is Dictionary<string, object> settings ? settings : null;
	}

	public static void WriteRunSettings(Dictionary<string, object> settings)
	{
		var worldGen = Traverse.Create(typeof(WorldGeneration)).Field("runSettings");
		if (worldGen.FieldExists())
		{
			worldGen.SetValue(settings);
		}
	}

	public static bool IsGenerating()
	{
		if (WorldGeneration.world == null) // Unity object — == (is null misses destroyed)
		{
			return false;
		}

		var generating = Traverse.Create(WorldGeneration.world).Field("generatingWorld");
		return generating.FieldExists() && generating.GetValue<bool>();
	}

	/// <summary>
	/// World-instance field access. The four world-defining fields were verified
	/// in the decompiled source (WorldGeneration.cs): totalTraveled (4162),
	/// biomeDepth (4165), biomeOverride (4237, OverrideSceneType enum). LoadedRun
	/// has no backing field (PreRunScript.LoadRun is the save flow — Phase 3
	/// saves scope), so it stays unset on the wire.
	/// </summary>
	private static Traverse? FieldOfWorld(string name)
	{
		if (WorldGeneration.world == null) // Unity object — ==
		{
			return null;
		}

		var field = Traverse.Create(WorldGeneration.world).Field(name);
		return field.FieldExists() ? field : null;
	}

	/// <summary>The world block table (private field, WorldGeneration.cs:4088).</summary>
	public static ushort[,]? ReadWorldBlocks(WorldGeneration world)
	{
		var field = Traverse.Create(world).Field("worldBlocks");
		return field.FieldExists() ? field.GetValue<ushort[,]>() : null;
	}

	/// <summary>The world-generation loading screen (WorldGeneration.cs:4168) — kept visible while the start gate holds a guest, so the wait reads as "still loading" instead of a black screen.</summary>
	public static GameObject? ReadLoadingObject()
	{
		if (WorldGeneration.world == null) // Unity object — ==
		{
			return null;
		}

		var field = Traverse.Create(WorldGeneration.world).Field("loadingObject");
		return field.FieldExists() ? field.GetValue<GameObject>() : null;
	}

	/// <summary>True while the generation loading screen is up — generation in progress or just finished (the finish fade, WorldGeneration.cs:3620, fires while it is still visible).</summary>
	public static bool IsLoadingVisible()
	{
		var loading = ReadLoadingObject();
		return loading != null && loading.activeSelf; // Unity object — ==
	}

	/// <summary>The loading-screen jitter figures (WorldGeneration.cs:4243) — the game only animates them while generatingWorld is true (WorldGeneration.cs:943-947), so the start-gate wait mirrors that jitter to keep the kept screen alive.</summary>
	public static RectTransform[]? ReadGenRects()
	{
		var field = Traverse.Create(WorldGeneration.world).Field("genRects");
		return field.FieldExists() ? field.GetValue<RectTransform[]>() : null;
	}

	public static int ReadBiomeOverride() => Convert.ToInt32(FieldOfWorld("biomeOverride")?.GetValue());

	public static void WriteBiomeOverride(int value) =>
		FieldOfWorld("biomeOverride")?.SetValue(Enum.ToObject(typeof(WorldGeneration.OverrideSceneType), value));

	public static int ReadBiomeDepth() => FieldOfWorld("biomeDepth")?.GetValue<int>() ?? 0;

	public static void WriteBiomeDepth(int value) => FieldOfWorld("biomeDepth")?.SetValue(value);

	public static int ReadTotalTraveled() => FieldOfWorld("totalTraveled")?.GetValue<int>() ?? 0;

	public static void WriteTotalTraveled(int value) => FieldOfWorld("totalTraveled")?.SetValue(value);
}
