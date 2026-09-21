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

	/// <summary>SemVer version (major.minor.patch[-prerelease][+build]) — discovery rejects non-SemVer strings, and state-bearing handshakes compare it by precedence.</summary>
	public string Version { get; } = version;

	/// <summary>
	/// The mod's content-id namespace (<c>namespace:path</c>). Optional: a mod
	/// that registers no content can omit it. When set, discovery validates the
	/// grammar, refuses the reserved built-in namespace
	/// <see cref="ContentId.BuiltInNamespace"/> and refuses a namespace another
	/// loaded mod already declared — the same fail-closed place as a duplicate
	/// mod id. Content registered by this mod is addressable as
	/// <c>&lt;namespace&gt;:&lt;content id&gt;</c>.
	/// </summary>
	public string? Namespace { get; set; }

	/// <summary>The mod's network contract — see <see cref="NetworkMode"/>.</summary>
	public NetworkMode NetworkMode { get; set; }

	/// <summary>
	/// The capabilities the mod declares. Defaults to
	/// <see cref="ModPermission.None"/>: nothing is granted implicitly, and
	/// unknown bits or host/state permissions on a local-only network mode are
	/// rejected at discovery.
	/// </summary>
	public ModPermission Permissions { get; set; }

	/// <summary>
	/// The mod ids this mod depends on (loaded after them, in topological
	/// order). Empty by default. Missing targets, self-dependencies,
	/// duplicated declarations and dependency cycles are rejected at discovery.
	/// </summary>
	public string[] Dependencies { get; set; } = [];

	/// <summary>
	/// The game's own code this mod binds — the DECLARED NATIVE BINDING of the
	/// tiered extension model (<c>docs/api/advanced-modification-policy.md</c>
	/// §1.1, decision 204): the patched game type or surface, named by the author.
	/// Optional — a mod that touches no game type omits it.
	///
	/// A declared FACT, not a permission: <see cref="ModPermission"/> is what CUO
	/// enforces, and CUO enforces nothing here — an undeclared binding is
	/// undetectable (the framework takes no anti-cheat stance), so this
	/// declaration is opt-in honesty whose only force is another peer's parity
	/// policy. It buys visibility, never stability: what it names belongs to the
	/// game, and a game update may break it with no CUO decision. A blank value
	/// (empty or whitespace-only) counts as no declaration.
	/// </summary>
	public string? NativeBinding { get; set; }

	public string? Description { get; set; }
}
