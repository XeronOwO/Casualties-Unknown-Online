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
}
