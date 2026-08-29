using System.Collections.Generic;
using CasualtiesUnknownOnline.GameState.Domains.World;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// Pure mapper between the kernel <see cref="RunState"/> and the adapter-facing
/// <see cref="WorldStartParams"/> projection. The adapter still reads the
/// projection; the kernel is the authoritative source during Phase D.
/// </summary>
public static class WorldRunStateMapper
{
	public static RunState ToRunState(ulong runId, WorldStartParams parameters, int layerIndex = 0)
	{
		var sourceSettings = parameters.RunSettings;
		List<RunSetting>? settings = null;
		if (sourceSettings is not null)
		{
			settings = [with(sourceSettings.Count)];
			foreach (var pair in sourceSettings)
			{
				settings.Add(ToRunSetting(pair.Key, pair.Value));
			}
		}

		return new RunState(
			runId,
			parameters.RandomState,
			parameters.BiomeOverride,
			parameters.BiomeDepth,
			parameters.TotalTraveled,
			parameters.LoadedRun,
			settings,
			layerIndex);
	}

	public static WorldStartParams ToWorldStartParams(RunState run)
	{
		var sourceSettings = run.RunSettings;
		Dictionary<string, object>? settings = null;
		if (sourceSettings is not null)
		{
			settings = [with(sourceSettings.Count)];
			foreach (var setting in sourceSettings)
			{
				settings[setting.Key] = setting.Kind switch
				{
					RunSettingKind.Int => setting.IntValue,
					RunSettingKind.Float => setting.FloatValue,
					RunSettingKind.Bool => setting.BoolValue,
					RunSettingKind.String => setting.StringValue,
					_ => setting.StringValue,
				};
			}
		}

		return new WorldStartParams
		{
			RandomState = run.RandomState,
			BiomeOverride = run.BiomeOverride,
			BiomeDepth = run.BiomeDepth,
			TotalTraveled = run.TotalTraveled,
			LoadedRun = run.LoadedRun,
			RunSettings = settings,
		};
	}

	private static RunSetting ToRunSetting(string key, object value) =>
		value switch
		{
			int i => new RunSetting(key, RunSettingKind.Int, IntValue: i),
			float f => new RunSetting(key, RunSettingKind.Float, FloatValue: f),
			bool b => new RunSetting(key, RunSettingKind.Bool, BoolValue: b),
			string s => new RunSetting(key, RunSettingKind.String, StringValue: s),
			_ => new RunSetting(key, RunSettingKind.String, StringValue: value.ToString() ?? ""),
		};
}
