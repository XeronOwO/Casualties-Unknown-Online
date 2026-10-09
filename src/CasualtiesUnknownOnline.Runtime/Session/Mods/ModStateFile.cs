using System.Collections.Generic;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// The on-disk shape of the host's mod-state table. A versioned protobuf
/// wrapper (the same serializer family as every wire/file payload) so the file
/// can evolve: an unknown version is refused explicitly (start empty + warn),
/// never guessed or silently half-loaded. A <see cref="StateEntry.Value"/> is a
/// FILE's binary leaf — the canonical encoding of the mod's value, which the
/// framework produces and reads back and the mod never sees; each mod owns the
/// meaning of its own value and its schema/migration policy.
/// </summary>
[ProtoContract]
internal sealed class ModStateFile
{
	/// <summary>
	/// Disk schema version. Bump only with an explicit migration path — which is
	/// why version 1 became 2: a version-1 file stored whatever bytes the mod
	/// chose, so the same byte string could now decode as a different value
	/// (a lone <c>0x00</c> reads as <c>false</c>), and no migration can tell the
	/// two apart. A version-1 file is refused whole, which is the degradation
	/// this file already promises for an unknown version.
	/// </summary>
	internal const int CurrentVersion = 2;

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

		/// <summary>A file's binary leaf: the framework's own encoding of the mod's value (the mod never sees these bytes).</summary>
		[ProtoMember(2)]
		public byte[] Value { get; set; } = [];
	}
}
