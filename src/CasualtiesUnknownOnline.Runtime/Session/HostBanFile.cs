using System.Collections.Generic;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>
/// The host ban-list disk schema. Only the host writes this file; guests never
/// create or read it. Versioned so an unknown future format degrades to an
/// empty list instead of a guessed migration.
/// </summary>
[ProtoContract]
internal sealed class HostBanFile
{
	public const int CurrentVersion = 1;

	[ProtoMember(1)]
	public int Version { get; set; } = CurrentVersion;

	[ProtoMember(2)]
	public List<ulong> BannedSteamIds { get; set; } = [];
}
