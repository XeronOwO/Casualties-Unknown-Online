using System;
using System.Collections.Generic;
using HarmonyLib;

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

		var preRun = Traverse.Create(typeof(PreRunScript)).Field("runSettings");
		if (preRun.FieldExists() && preRun.GetValue() is Dictionary<string, object> fromPreRun)
		{
			return fromPreRun;
		}

		return null;
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

	public static int ReadBiomeOverride() => Convert.ToInt32(FieldOfWorld("biomeOverride")?.GetValue());

	public static void WriteBiomeOverride(int value) =>
		FieldOfWorld("biomeOverride")?.SetValue(Enum.ToObject(typeof(WorldGeneration.OverrideSceneType), value));

	public static int ReadBiomeDepth() => FieldOfWorld("biomeDepth")?.GetValue<int>() ?? 0;

	public static void WriteBiomeDepth(int value) => FieldOfWorld("biomeDepth")?.SetValue(value);

	public static int ReadTotalTraveled() => FieldOfWorld("totalTraveled")?.GetValue<int>() ?? 0;

	public static void WriteTotalTraveled(int value) => FieldOfWorld("totalTraveled")?.SetValue(value);
}
