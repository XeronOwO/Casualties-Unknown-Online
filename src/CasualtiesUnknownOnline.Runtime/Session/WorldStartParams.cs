using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>
/// World-start parameters captured by the host at run start and applied by
/// guests before their own world generation. The game's world gen is fully
/// non-deterministic (no seed, Unity global Random everywhere) — restoring the
/// host's Random.state plus run settings is the only way to produce the same
/// world on both sides (see docs/game-internals.md).
/// </summary>
public sealed class WorldStartParams
{
	public byte[] RandomState { get; init; } = [];

	public byte BiomeOverride { get; init; }

	public byte BiomeDepth { get; init; }

	public int TotalTraveled { get; init; }

	public bool LoadedRun { get; init; }

	public Dictionary<string, object>? RunSettings { get; init; }

	/// <summary>Domain → wire; the reverse lives on <see cref="WorldStartParamsMsg"/>.</summary>
	public WorldStartParamsMsg ToWorldStartParamsMsg() => new()
	{
		RandomState = RandomState,
		BiomeOverride = BiomeOverride,
		BiomeDepth = BiomeDepth,
		TotalTraveled = TotalTraveled,
		LoadedRun = LoadedRun,
		RunSettings = [.. (RunSettings ?? []).Select(kv => new SettingEntryMsg
		{
			Key = kv.Key,
			Kind = kv.Value switch
			{
				int => 1,
				float => 2,
				bool => 3,
				string => 4,
				_ => 0,
			},
			IntValue = kv.Value is int i ? i : 0,
			FloatValue = kv.Value is float f ? f : 0f,
			BoolValue = kv.Value is bool b && b,
			StringValue = kv.Value as string ?? "",
		})],
	};
}
