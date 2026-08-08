using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Session.World;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>World-start parameters (host's Random.state + run settings) so both sides generate the same world.</summary>
[ProtoContract]
public sealed class WorldStartParamsMsg
{
	[ProtoMember(1)]
	public byte[] RandomState { get; set; } = [];

	[ProtoMember(2)]
	public uint BiomeOverride { get; set; }

	[ProtoMember(3)]
	public uint BiomeDepth { get; set; }

	[ProtoMember(4)]
	public int TotalTraveled { get; set; }

	[ProtoMember(5)]
	public bool LoadedRun { get; set; }

	[ProtoMember(6)]
	public List<SettingEntryMsg> RunSettings { get; set; } = [];

	/// <summary>Wire → domain; the reverse lives in <see cref="WorldStartParamsMsgExtensions"/>.</summary>
	public WorldStartParams ToWorldStartParams()
	{
		var settings = new Dictionary<string, object>(RunSettings.Count);
		foreach (var entry in RunSettings)
		{
			switch (entry.Kind)
			{
				case 1:
					settings[entry.Key] = entry.IntValue;
					break;
				case 2:
					settings[entry.Key] = entry.FloatValue;
					break;
				case 3:
					settings[entry.Key] = entry.BoolValue;
					break;
				case 4:
					settings[entry.Key] = entry.StringValue;
					break;
			}
		}

		return new WorldStartParams
		{
			RandomState = RandomState,
			BiomeOverride = (byte)BiomeOverride,
			BiomeDepth = (byte)BiomeDepth,
			TotalTraveled = TotalTraveled,
			LoadedRun = LoadedRun,
			RunSettings = settings.Count > 0 ? settings : null,
		};
	}
}
