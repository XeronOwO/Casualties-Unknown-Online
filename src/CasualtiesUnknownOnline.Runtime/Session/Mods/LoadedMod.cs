using CasualtiesUnknownOnline.Abstractions;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>One loaded mod: manifest + instance + its context (the snapshot is taken at bind time).</summary>
internal sealed record LoadedMod(ModManifest Manifest, ICuoMod Instance, ModContext Context);
