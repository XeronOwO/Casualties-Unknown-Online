using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// Phase C save-file header. It carries metadata only; authoritative gameplay
/// state lives in the <see cref="GameCheckpoint"/> payload.
/// </summary>
[ProtoContract]
public sealed class SaveHeader
{
	[ProtoMember(1)]
	public int SchemaVersion { get; set; }

	[ProtoMember(2)]
	public string GameBuild { get; set; } = "";

	[ProtoMember(3)]
	public string ModBuild { get; set; } = "";

	[ProtoMember(4)]
	public ulong RunEpoch { get; set; }

	[ProtoMember(5)]
	public ulong GlobalRevision { get; set; }

	[ProtoMember(6)]
	public long CreatedAtTicks { get; set; }
}
