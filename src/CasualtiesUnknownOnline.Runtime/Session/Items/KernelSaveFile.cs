using System.Collections.Generic;
using CasualtiesUnknownOnline.Protocol.Wire;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// The on-disk container for a Phase C kernel checkpoint. The checkpoint is the
/// only authoritative gameplay payload; recent batches are intentionally not
/// persisted in this phase.
/// </summary>
[ProtoContract]
public sealed class KernelSaveFile
{
	[ProtoMember(1)]
	public SaveHeader Header { get; set; } = new();

	[ProtoMember(2)]
	public List<WireItem> Items { get; set; } = [];

	[ProtoMember(3)]
	public List<WireRandomStream> RandomStreams { get; set; } = [];

	[ProtoMember(4)]
	public WireRunState? Run { get; set; }

	[ProtoMember(5)]
	public WireWorldEntityState? WorldEntities { get; set; }

	[ProtoMember(6)]
	public List<WirePlayerState> Players { get; set; } = [];

	[ProtoMember(7)]
	public List<WireEnemyState> Enemies { get; set; } = [];
}
