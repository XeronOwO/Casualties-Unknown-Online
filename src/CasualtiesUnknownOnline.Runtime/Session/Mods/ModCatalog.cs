using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Abstractions;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// The loaded-mod table. This is internal state owned by the mod domain, not a
/// DI service: it is created by <see cref="ModService"/> and shared by the
/// lifecycle pump and the host-command processor so neither needs a cycle back
/// to the facade.
/// </summary>
internal sealed class ModCatalog
{
	private readonly List<LoadedMod> _mods = [];

	public IReadOnlyList<LoadedMod> Mods => _mods;

	public IReadOnlyList<ModManifest> CurrentManifests => [.. _mods.Select(m => m.Manifest)];

	public IReadOnlyList<ICuoMod> LoadedInstances => [.. _mods.Select(m => m.Instance)];

	public LoadedMod? Find(string id) => _mods.FirstOrDefault(m => m.Manifest.Id == id);

	public void Add(LoadedMod mod) => _mods.Add(mod);
}
