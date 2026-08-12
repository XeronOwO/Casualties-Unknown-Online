using System;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// Declares a class as a CUO mod. The framework discovers it by scanning the
/// loaded assemblies (the first update frame — BepInEx loads plugins one by
/// one, load-then-Awake, so a scan in the framework's own Awake would miss
/// plugins loaded after it), instantiates it (it must implement
/// <see cref="ICuoMod"/> and have a public parameterless constructor), and
/// drives its lifecycle.
///
/// The attribute IS the manifest source — the framework builds the
/// <see cref="ModManifest"/> from it, the mod never declares its metadata
/// twice. <see cref="NetworkMode"/> defaults to <see cref="NetworkMode.Unspecified"/>
/// and is REJECTED at discovery: a mod that does not state its network mode
/// does not load.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class CuoModAttribute(string id, string displayName, string version) : Attribute
{
	/// <summary>Unique mod id — duplicated ids are rejected at discovery.</summary>
	public string Id { get; } = id;

	public string DisplayName { get; } = displayName;

	/// <summary>Exact string version, compared by equality in the handshake.</summary>
	public string Version { get; } = version;

	/// <summary>The mod's network contract — see <see cref="NetworkMode"/>.</summary>
	public NetworkMode NetworkMode { get; set; }

	public string? Description { get; set; }
}
