namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The framework-built description of a discovered mod — a read-only value
/// object derived from its <see cref="CuoModAttribute"/> (the attribute is the
/// single declared source; the manifest is what the framework and the
/// handshake actually carry). Mods never construct this themselves.
/// </summary>
public sealed class ModManifest(string id, string displayName, string version, NetworkMode networkMode, string? description)
{
	public string Id { get; } = id;

	public string DisplayName { get; } = displayName;

	/// <summary>Exact string version — the handshake compares it by equality.</summary>
	public string Version { get; } = version;

	public NetworkMode NetworkMode { get; } = networkMode;

	public string? Description { get; } = description;
}
