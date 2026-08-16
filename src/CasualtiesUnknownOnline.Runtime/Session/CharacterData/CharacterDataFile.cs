using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Session.CharacterData;

/// <summary>
/// The on-disk shape of the host's character-data table. A versioned protobuf
/// wrapper — same serializer family as every wire message — so the file can
/// evolve later: an unknown version is refused explicitly (start empty + warn),
/// never guessed or silently half-loaded.
/// </summary>
[ProtoContract]
internal sealed class CharacterDataFile
{
	/// <summary>Disk schema version. Bump only with an explicit migration path.</summary>
	internal const int CurrentVersion = 1;

	[ProtoMember(1)]
	public int Version { get; set; } = CurrentVersion;

	[ProtoMember(2)]
	public List<Entry> Characters { get; set; } = [];

	/// <summary>One SteamID → its latest character snapshot.</summary>
	[ProtoContract]
	internal sealed class Entry
	{
		[ProtoMember(1)]
		public ulong SteamId { get; set; }

		[ProtoMember(2)]
		public CharacterDataMsg Data { get; set; } = new();
	}
}
