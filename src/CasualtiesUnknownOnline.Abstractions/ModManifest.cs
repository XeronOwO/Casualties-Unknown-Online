using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The framework-built description of a discovered mod — a read-only value
/// object derived from its <see cref="CuoModAttribute"/> (the attribute is the
/// single declared source; the manifest is what the framework and the
/// handshake actually carry). Mods never construct this themselves.
/// </summary>
public sealed class ModManifest(string id, string displayName, string version, NetworkMode networkMode, string? description,
	ModPermission permissions = ModPermission.None, IReadOnlyList<string>? dependencies = null)
{
	public string Id { get; } = id;

	public string DisplayName { get; } = displayName;

	/// <summary>SemVer version — discovery validates it; state-bearing handshakes compare it by precedence.</summary>
	public string Version { get; } = version;

	public NetworkMode NetworkMode { get; } = networkMode;

	/// <summary>The capabilities the mod declared (nothing is implicit).</summary>
	public ModPermission Permissions { get; } = permissions;

	/// <summary>The mod ids this mod depends on — an owned copy, never the attribute array.</summary>
	public IReadOnlyList<string> Dependencies { get; } = dependencies is null ? [] : [.. dependencies];

	public string? Description { get; } = description;
}
