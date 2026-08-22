using System.Collections.Generic;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// The on-disk shape of the host's mod-state table. A versioned protobuf
/// wrapper (the same serializer family as every wire/file payload) so the file
/// can evolve: an unknown version is refused explicitly (start empty + warn),
/// never guessed or silently half-loaded. The framework stores mod bytes as
/// opaque <see cref="byte"/> arrays and never interprets them; each mod owns
/// its own schema/migration policy.
/// </summary>
[ProtoContract]
internal sealed class ModStateFile
{
	/// <summary>Disk schema version. Bump only with an explicit migration path.</summary>
	internal const int CurrentVersion = 1;

	[ProtoMember(1)]
	public int Version { get; set; } = CurrentVersion;

	[ProtoMember(2)]
	public List<Entry> Entries { get; set; } = [];

	/// <summary>One mod id → its persisted state.</summary>
	[ProtoContract]
	internal sealed class Entry
	{
		[ProtoMember(1)]
		public string ModId { get; set; } = "";

		/// <summary>The mod's manifest version at the time the state was last written (diagnostic/missing-mod bookkeeping).</summary>
		[ProtoMember(2)]
		public string ModVersion { get; set; } = "";

		/// <summary>The mod-declared schema version (metadata opaquely carried for the mod's own migration).</summary>
		[ProtoMember(3)]
		public int SchemaVersion { get; set; } = 1;

		[ProtoMember(4)]
		public List<StateEntry> States { get; set; } = [];
	}

	[ProtoContract]
	internal sealed class StateEntry
	{
		[ProtoMember(1)]
		public string Key { get; set; } = "";

		[ProtoMember(2)]
		public byte[] Value { get; set; } = [];
	}
}
