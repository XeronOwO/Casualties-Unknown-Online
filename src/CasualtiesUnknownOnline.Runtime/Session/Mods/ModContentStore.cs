using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Abstractions;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// The framework-wide content registry: it OWNS the definitions every mod
/// registered, and <see cref="ModContentAdapter"/> is one mod's write guard over
/// its own slice (the permission gate, the id/kind/schema rails, the count cap).
///
/// It is a leaf on purpose. Two of its readers cannot resolve the mod domain's
/// facade: the session's handshake list, because the facade reads the session
/// (a cycle), and the save layer, whose cut records the registry's fingerprint in
/// a manifest. The status and building-runtime tables already split this way
/// (<see cref="ModStatusStore"/>), so the content set follows them instead of
/// growing a second registry.
///
/// Single-threaded by construction: mods register during discovery, <c>Bind</c>
/// and the mod pump, and both readers run on that same main thread.
///
/// Public while its WRITE half stays internal, because <see cref="ModService"/>'s
/// own constructor is public and hands the store to the mod domain's internal half;
/// what it exposes publicly is <see cref="IModContentFingerprints"/>.
/// </summary>
public sealed class ModContentStore : IModContentFingerprints
{
	private readonly List<ModContentRegistration> _entries = [];

	/// <inheritdoc />
	public string Fingerprint => ModContentFingerprint.Digest(ModContentFingerprint.Lines(_entries));

	/// <inheritdoc />
	public IReadOnlyDictionary<string, string> ByMod
	{
		get
		{
			var byMod = new Dictionary<string, string>(StringComparer.Ordinal);
			foreach (var group in _entries.GroupBy(entry => entry.ModId, StringComparer.Ordinal))
			{
				byMod[group.Key] = ModContentFingerprint.Digest(ModContentFingerprint.Lines(group));
			}

			return byMod;
		}
	}

	/// <summary>A snapshot of every mod's registered definitions (a copy — safe to hold): the read view mods and providers use.</summary>
	internal IReadOnlyList<ModContentRegistration> Entries => [.. _entries];

	/// <summary>The definitions ONE mod registered, in registration order.</summary>
	internal IReadOnlyList<IModContentDefinition> DefinitionsOf(string modId) =>
		[.. _entries.Where(entry => string.Equals(entry.ModId, modId, StringComparison.Ordinal)).Select(entry => entry.Definition)];

	internal int CountOf(string modId) =>
		_entries.Count(entry => string.Equals(entry.ModId, modId, StringComparison.Ordinal));

	internal bool IsRegistered(string modId, string id) =>
		_entries.Any(entry => string.Equals(entry.ModId, modId, StringComparison.Ordinal)
			&& string.Equals(entry.Definition.Id, id, StringComparison.Ordinal));

	/// <summary>Files one accepted definition under its owner. The rails that decide acceptance stay in the mod's own adapter.</summary>
	internal void Add(string modId, string? @namespace, IModContentDefinition definition) =>
		_entries.Add(new ModContentRegistration(modId, definition, @namespace));

	/// <summary>Retires one entry; false = this mod has no such id.</summary>
	internal bool Remove(string modId, string id)
	{
		var index = _entries.FindIndex(entry => string.Equals(entry.ModId, modId, StringComparison.Ordinal)
			&& string.Equals(entry.Definition.Id, id, StringComparison.Ordinal));
		if (index < 0)
		{
			return false;
		}

		_entries.RemoveAt(index);
		return true;
	}

	/// <summary>
	/// Retires every entry one mod registered, and returns how many were withdrawn. A mod
	/// registers its content BEFORE it can fail — a declaration is scanned before <c>Bind</c>
	/// runs and a code registration happens inside it — so a load that fails has to take its
	/// slice back: the framework-wide view is what the console, the ownership query and both
	/// content fingerprints read, and an entry left behind would claim content the process
	/// never materialized (a peer whose own <c>Bind</c> succeeded would then be judged against
	/// a set that only exists because this one failed).
	/// </summary>
	internal int RemoveMod(string modId) =>
		_entries.RemoveAll(entry => string.Equals(entry.ModId, modId, StringComparison.Ordinal));
}
