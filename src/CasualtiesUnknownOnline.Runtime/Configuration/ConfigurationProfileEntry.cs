using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Configuration;

/// <summary>
/// One stored configuration setting inside a <see cref="ConfigurationProfileDocument"/>.
/// The value is kept in BepInEx's serialized string form so the profile is
/// independent of the strongly typed runtime options and can be reapplied to a
/// newer build even when only the option shape changes.
/// </summary>
[ProtoContract]
internal sealed class ConfigurationProfileEntry
{
	[ProtoMember(1)]
	public string Section { get; set; } = "";

	[ProtoMember(2)]
	public string Key { get; set; } = "";

	[ProtoMember(3)]
	public string SerializedValue { get; set; } = "";
}
