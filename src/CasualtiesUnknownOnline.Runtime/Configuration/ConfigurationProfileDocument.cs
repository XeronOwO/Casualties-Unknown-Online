using System.Collections.Generic;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Configuration;

/// <summary>
/// The persisted shape of a named CUO configuration profile. A profile is a
/// full snapshot of every bound BepInEx entry; the store owns the version and
/// the file path, this type only carries data.
/// </summary>
[ProtoContract]
internal sealed class ConfigurationProfileDocument
{
	[ProtoMember(1)]
	public int Version { get; set; } = ConfigurationProfileStore.CurrentSchemaVersion;

	[ProtoMember(2)]
	public List<ConfigurationProfileEntry> Entries { get; } = [];
}
