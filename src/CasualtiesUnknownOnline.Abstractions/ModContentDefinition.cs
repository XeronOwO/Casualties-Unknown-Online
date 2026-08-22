namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// One content contribution registered by a mod through <see cref="IModContent"/>.
/// The framework treats the payload as opaque bytes: it never interprets,
/// migrates or serializes the mod's content format, so the mod owns its own
/// schema/versioning just like <see cref="IModState"/>.
///
/// Instances are immutable snapshots. The payload is copied on construction
/// and every read of <see cref="Data"/> returns a defensive copy, so a mod
/// cannot mutate the registry's stored bytes through a definition it received.
/// </summary>
public sealed class ModContentDefinition(string id, string kind, byte[] data)
{
	private readonly byte[] _data = (byte[])data.Clone();

	/// <summary>The mod-scoped content id (unique within the registering mod).</summary>
	public string Id { get; } = id;

	/// <summary>The content kind — a mod-defined type tag (for example "item", "recipe", "npc").</summary>
	public string Kind { get; } = kind;

	/// <summary>The opaque content payload. Each access returns a defensive copy.</summary>
	public byte[] Data => (byte[])_data.Clone();
}
