using System.Collections.Generic;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Protocol.Wire;

/// <summary>
/// Wire form of the authoritative World/Run baseline. It carries the host's
/// Random.state capture and typed run settings so guests replay the same run's
/// world-generation stream.
/// </summary>
[ProtoContract]
public sealed class WireRunState
{
	[ProtoMember(1)]
	public ulong RunId { get; set; }

	[ProtoMember(2)]
	public byte[] RandomState { get; set; } = [];

	[ProtoMember(3)]
	public uint BiomeOverride { get; set; }

	[ProtoMember(4)]
	public uint BiomeDepth { get; set; }

	[ProtoMember(5)]
	public int TotalTraveled { get; set; }

	[ProtoMember(6)]
	public bool LoadedRun { get; set; }

	[ProtoMember(7)]
	public List<WireRunSetting> RunSettings { get; set; } = [];

	[ProtoMember(8)]
	public int LayerIndex { get; set; }
}
